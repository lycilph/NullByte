
using NullByte.Core.Domain;
using NullByte.Core.Persistence;
using NullByte.Core.Scripting;
using NullByte.Scripting.Compilation;

namespace NullByte.Scripting.Runtime;

/// <summary>
/// Coordinates compiling, starting and managing player-written scripts.
/// Each running script is represented by a ScriptProcess and receives
/// its own NullByte API instance.
/// </summary>
public sealed class ScriptService : IScriptOperations
{
    private readonly GameState gameState;
    private readonly IScriptCompiler compiler;
    private readonly ScriptRunner runner;
    private readonly INullByteApiFactory apiFactory;
    private readonly ScriptProcessManager processManager;

    public ScriptService(
        GameState gameState,
        IScriptCompiler compiler,
        ScriptRunner runner,
        INullByteApiFactory apiFactory,
        ScriptProcessManager processManager)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(apiFactory);
        ArgumentNullException.ThrowIfNull(processManager);

        this.gameState = gameState;
        this.compiler = compiler;
        this.runner = runner;
        this.apiFactory = apiFactory;
        this.processManager = processManager;
    }

    /// <summary>
    /// All processes currently retained by the process manager.
    /// </summary>
    public IReadOnlyList<ScriptProcess> Processes => processManager.Processes;

    /// <summary>
    /// Gets whether at least one process is currently running.
    /// </summary>
    public bool HasRunningProcesses =>
        processManager.Processes.Any(process => process.State == ScriptProcessState.Running);

    /// <summary>
    /// Raised when a process terminates because of an exception.
    /// </summary>
    public event Action<ScriptProcess, Exception>? ProcessFailed;

    /// <summary>
    /// Raised when a process completes normally.
    /// </summary>
    public event Action<ScriptProcess>? ProcessCompleted;

    /// <summary>
    /// Compiles and starts a script stored in the virtual filesystem.
    /// This method returns immediately after starting the process.
    /// </summary>
    public async Task<int> StartSimple(string hostname, string filename, IReadOnlyList<string> args, int threads = 1)
    {
        var result = await Start(hostname, filename, args, threads);
        return result.Succeeded ? result.Pid : 0;
    }

    public async Task<ScriptStartResult> Start(string hostname, string filename, IReadOnlyList<string> args, int threads = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostname);
        ArgumentException.ThrowIfNullOrWhiteSpace(filename);
        ArgumentNullException.ThrowIfNull(args);

        if (threads < 1)
            return ScriptStartResult.Failure("Thread count must be at least 1.");

        // Remove old, finished processes before starting another one.
        processManager.RemoveCompleted();

        // Resolve the target server.
        if (!gameState.Network.TryGetServer(hostname, out var server))
            return ScriptStartResult.Failure($"Unknown server: {hostname}");

        // Check if player has root access to server
        if (!server.HasRootAccess)
            return ScriptStartResult.Failure($"You do not have root access on {hostname}");

        // Read the script from the target server.
        if (!gameState.FileSystem.TryReadFile(hostname, filename, out var source))
            return ScriptStartResult.Failure($"File not found: {filename}");

        // Resolve all #includes
        var resolution = new ScriptIncludeResolver(gameState.FileSystem, hostname).Resolve(filename, source!);
        if (!resolution.Succeeded)
            return ScriptStartResult.Failure($"Failed to resolve includes for '{filename}'.", resolution.Diagnostics);

        // Compile the script.
        // Pure CPU work: source string in, immutable result out. No game state in here.
        // No ConfigureAwait(false): we must resume on the game thread.
        var compilation = await Task.Run(() => compiler.Compile(resolution.Units, filename));

        if (!compilation.Succeeded)
            return ScriptStartResult.Failure($"Compilation failed for '{filename}'.", compilation.Diagnostics);

        var compiledScript = compilation.Script
            ?? throw new InvalidOperationException("Successful compilation did not produce a script.");

        // Static RAM cost is multiplied by the requested thread count.
        var ramCost = compiledScript.RamCost * threads;

        if (!double.IsFinite(ramCost) || ramCost < 0)
            return ScriptStartResult.Failure($"Invalid RAM cost calculated for '{filename}'.");

        // Check available RAM before creating a process.
        if (ramCost > server!.AvailableRam)
        {
            return ScriptStartResult.Failure(
                $"Not enough RAM on {hostname}. " +
                $"Required: {ramCost:F2} GB. " +
                $"Available: {server.AvailableRam:F2} GB.");
        }

        // Reserve RAM before creating or starting the process.
        try
        {
            server.AllocateRam(ramCost);
        }
        catch (InvalidOperationException)
        {
            // The allocation check is repeated by Server itself.
            // This also protects against an allocation failure between
            // the availability check and reservation.
            return ScriptStartResult.Failure($"Not enough RAM on {hostname}.");
        }

        ScriptProcess? process = null;
        try
        {
            // Create the process with its own cancellation source,
            // thread count and RAM allocation.
            process = processManager.Create(filename, hostname, args.ToArray(), threads, ramCost);

            // Create a distinct API instance for this process.
            var api = apiFactory.Create(this, process.Hostname, process.Threads, process.Cancellation.Token);

            // Start execution without waiting for the script to finish.
            process.Execution = RunAndObserveAsync(process, compiledScript, api);
        }
        catch (Exception exception)
        {
            if (process is not null)
            {
                process.State = ScriptProcessState.Failed;
                process.Failure = exception;
            }

            server.ReleaseRam(ramCost);

            return ScriptStartResult.Failure($"Could not start '{filename}': {exception.Message}");
        }

        return ScriptStartResult.Success(process.Pid, compilation.Diagnostics);
    }

    /// <summary>
    /// Attempts to terminate a process.
    /// The process will stop when its next cancellable await is reached.
    /// </summary>
    public bool Kill(int pid) => processManager.Kill(pid);

    /// <summary>
    /// Terminates all currently running processes.
    /// </summary>
    public int KillAll() => processManager.KillAll();

    /// <summary>
    /// Removes processes that have finished execution.
    /// </summary>
    public void RemoveCompleted() => processManager.RemoveCompleted();

    /// <summary>Returns all process on the given server.</summary>
    public IReadOnlyList<ScriptProcessInfo> ProcessesRunning(string hostname)
    {
        var server = ResolveTarget(hostname);

        return processManager
            .GetForHost(server.Hostname)
            .Select(p => new ScriptProcessInfo(
                p.Pid,
                p.Filename,
                p.Args,
                p.Threads,
                p.Hostname,
                p.State))
            .ToList();
    }

    /// Describes the running processes so they can be started again after a load. A script's own
    /// state (its local variables, how far through a hack it was) is not saved: it starts from the top.
    /// Game thread only.
    public IReadOnlyList<ProcessSave> CaptureRunningProcesses()
        => processManager.Processes                  // already in PID order, i.e. the order they were started
            .Where(process => process.State == ScriptProcessState.Running)
            .Select(process => new ProcessSave
            {
                Filename = process.Filename,
                Hostname = process.Hostname,
                Args = process.Args.ToArray(),
                Threads = process.Threads
            })
            .ToArray();

    /// Starts saved processes again, one after the other, in the order given. They get new PIDs. A process that
    /// cannot start (the script no longer compiles, or its RAM no longer fits) is reported in the
    /// result and does not stop the others.
    public async Task<IReadOnlyList<ScriptRestoreResult>> RestoreProcessesAsync(IEnumerable<ProcessSave> saved)
    {
        ArgumentNullException.ThrowIfNull(saved);

        var results = new List<ScriptRestoreResult>();
        foreach (var process in saved)
        {
            var result = await Start(process.Hostname, process.Filename, process.Args, process.Threads);
            results.Add(new ScriptRestoreResult(process, result));
        }

        return results;
    }

    public bool ScriptRunning(string filename, string hostname)
    {
        var server = ResolveTarget(hostname);

        return processManager
            .GetForHost(server.Hostname)
            .Any(p => string.Equals(filename, p.Filename, StringComparison.OrdinalIgnoreCase));
    }

    public double GetScriptRam(string filename, string hostname)
    {
        var target = ResolveTarget(hostname);

        // Resolve the target server.
        if (!gameState.Network.TryGetServer(target.Hostname, out var server))
            return 0.0;

        // Read the script from the target server.
        if (!gameState.FileSystem.TryReadFile(target.Hostname, filename, out var source))
            return 0.0;

        // Resolve all #includes
        var resolution = new ScriptIncludeResolver(gameState.FileSystem, target.Hostname).Resolve(filename, source!);
        if (!resolution.Succeeded)
            return 0.0;

        // Compile the script.
        var compilation = compiler.Compile(resolution.Units, filename);

        if (!compilation.Succeeded || compilation.Script == null)
            return 0.0;

        return compilation.Script.RamCost;
    }
    
    private Server ResolveTarget(string hostname)
    {
        if (string.IsNullOrWhiteSpace(hostname))
            throw new ArgumentException("The target hostname cannot be empty.", nameof(hostname));

        return gameState.Network.GetServer(hostname);
    }

    private async Task RunAndObserveAsync(ScriptProcess process, CompiledScript script, INullByteApi api)
    {
        try
        {
            await runner.RunAsync(script, api, process.Args);

            // A killed process must remain Killed, even if its
            // execution happens to finish normally after cancellation.
            if (process.State == ScriptProcessState.Running)
            {
                process.State = ScriptProcessState.Completed;
                ProcessCompleted?.Invoke(process);
            }
        }
        catch (OperationCanceledException)
        {
            if (process.State == ScriptProcessState.Running)
                process.State = ScriptProcessState.Killed;
        }
        catch (Exception exception)
        {
            process.Failure = exception;
            process.State = ScriptProcessState.Failed;

            ProcessFailed?.Invoke(process, exception);
        }
        finally
        {
            ReleaseProcessRam(process);
        }
    }

    private void ReleaseProcessRam(ScriptProcess process)
    {
        var server = gameState.Network.GetServer(process.Hostname);

        // The process owns the amount reserved when it was started.
        // Release it exactly once.
        if (process.RamReleased)
            return;

        server.ReleaseRam(process.RamCost);

        // Clear the process's allocation so it cannot be released twice.
        process.RamReleased = true;
    }
}