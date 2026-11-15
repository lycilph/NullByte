using System.Diagnostics;

namespace NullByte.Scripting.Runtime;

/// <summary>
/// Owns the player script processes and their PIDs.
/// </summary>
public sealed class ScriptProcessManager
{
    private readonly Dictionary<int, ScriptProcess> processes = [];

    private int nextPid = 1;

    /// <summary>
    /// Gets all processes currently known to the manager.
    /// </summary>
    public IReadOnlyList<ScriptProcess> Processes =>
        processes.Values
            .OrderBy(process => process.Pid)
            .ToArray();

    /// <summary>
    /// Creates and registers a new process.
    /// </summary>
    public ScriptProcess Create(string filename, string hostname, IReadOnlyList<string> args, int threads, double ramCost)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filename);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostname);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentOutOfRangeException.ThrowIfLessThan(threads, 1);

        if (!double.IsFinite(ramCost) || ramCost < 0)
            throw new ArgumentOutOfRangeException(nameof(ramCost));

        var pid = AllocatePid();

        var process = new ScriptProcess(
            pid,
            filename,
            hostname,
            args,
            threads,
            ramCost,
            new CancellationTokenSource());

        processes.Add(pid, process);

        return process;
    }

    /// <summary>
    /// Attempts to find a process by PID.
    /// </summary>
    public bool TryGet(int pid, out ScriptProcess? process) => processes.TryGetValue(pid, out process);

    public IReadOnlyList<ScriptProcess> GetForHost(string hostname)
        => processes.Values
        .Where(p => string.Equals(p.Hostname, hostname, StringComparison.OrdinalIgnoreCase))
        .OrderBy(p => p.Pid)
        .ToList();

    /// <summary>
    /// Requests cancellation of a process.
    /// </summary>
    public bool Kill(int pid)
    {
        if (!processes.TryGetValue(pid, out var process))
            return false;

        if (process.State != ScriptProcessState.Running)
            return false;

        process.State = ScriptProcessState.Killed;
        process.Cancellation.Cancel();

        return true;
    }

    /// <summary>
    /// Requests cancellation of every running process.
    /// </summary>
    public int KillAll()
    {
        var killed = 0;

        foreach (var process in processes.Values)
        {
            if (process.State != ScriptProcessState.Running)
                continue;

            process.State = ScriptProcessState.Killed;
            process.Cancellation.Cancel();
            killed++;
        }

        return killed;
    }

    /// <summary>
    /// Removes processes that have finished execution.
    /// </summary>
    public void RemoveCompleted()
    {
        var completed = processes.Values
            .Where(process =>
                process.State != ScriptProcessState.Running &&
                process.Execution is { IsCompleted: true })
            .Select(process => process.Pid)
            .ToArray();

        foreach (var pid in completed)
        {
            var process = processes[pid];

            processes.Remove(pid);
            process.Cancellation.Dispose();
        }
    }

    private int AllocatePid()
    {
        // PIDs are deliberately simple for now. We will make this more robust
        // when persistence/save-game concerns arrive later.
        while (processes.ContainsKey(nextPid))
        {
            nextPid++;

            if (nextPid == int.MaxValue)
                nextPid = 1;
        }

        return nextPid++;
    }
}