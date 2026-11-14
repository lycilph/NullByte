using NullByte.Core.Domain;
using NullByte.Core.Scripting;
using NullByte.Scripting.Compilation;

namespace NullByte.Scripting.Runtime;

/// <summary>
/// Coordinates compiling and starting player-written scripts.
/// </summary>
/// <remarks>
/// M5 supports one running script at a time. Full process management,
/// cancellation, PIDs, threads, and RAM accounting are introduced in M6.
/// </remarks>
public sealed class ScriptService
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

    public IReadOnlyList<ScriptProcess> Processes => processManager.Processes;

    public bool Kill(int pid) => processManager.Kill(pid);

    public int KillAll() => processManager.KillAll();

    public bool HasRunningProcesses =>
        processManager.Processes.Any(process => process.State == ScriptProcessState.Running);

    /// <summary>
    /// Compiles and starts a script stored in the game's virtual filesystem.
    /// </summary>
    /// <remarks>
    /// This method starts execution but deliberately does not await the entire
    /// script. Player scripts may contain infinite asynchronous loops, so the
    /// terminal must remain available after the script starts.
    /// </remarks>
    public ScriptStartResult Start(string hostname, string fileName, IReadOnlyList<string> args)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostname);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(args);

        ClearCompletedScript();

        //if (IsScriptRunning)
        //    return ScriptStartResult.Failure("A script is already running.");

        if (!gameState.FileSystem.TryReadFile(hostname, fileName, out var source))
            return ScriptStartResult.Failure($"File not found: {fileName}");

        var compilation = compiler.Compile(source!, fileName);

        if (!compilation.Succeeded)
            return ScriptStartResult.Failure($"Compilation failed for '{fileName}'.", compilation.Diagnostics);

        var compiledScript = compilation.Script
            ?? throw new InvalidOperationException("Successful compilation did not produce a script.");

        //runningScript = 
            RunAndObserveAsync(compiledScript, api, args.ToArray());

        return ScriptStartResult.Success(compilation.Diagnostics);
    }

    /// <summary>
    /// Raised when a running script terminates because of an exception.
    /// </summary>
    public event Action<Exception>? ScriptFailed;

    /// <summary>
    /// Raised when a running script finishes normally.
    /// </summary>
    public event Action? ScriptCompleted;

    private async Task RunAndObserveAsync(CompiledScript script, INullByteApi api, IReadOnlyList<string> args)
    {
        try
        {
            await runner.RunAsync(script, api, args);

            ScriptCompleted?.Invoke();
        }
        catch (OperationCanceledException)
        {
            // Cancellation becomes part of process management in M6.
        }
        catch (Exception exception)
        {
            ScriptFailed?.Invoke(exception);
        }
    }

    private void ClearCompletedScript() => processManager.RemoveCompleted();
    //{
    //    if (runningScript?.IsCompleted == true)
    //        runningScript = null;
    //}
}