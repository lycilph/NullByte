using NullByte.Core.Scripting;
using NullByte.Core.Domain;

namespace NullByte.Scripting.Runtime;

/// <summary>
/// Represents one running or completed player script process.
/// </summary>
public sealed class ScriptProcess
{
    internal ScriptProcess(
        int pid,
        string filename,
        ServerId hostServerId,
        IReadOnlyList<string> args,
        int threads,
        double ramCost,
        CancellationTokenSource cancellation)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pid, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(threads, 1);

        ArgumentException.ThrowIfNullOrWhiteSpace(filename);

        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(cancellation);

        if (!double.IsFinite(ramCost) || ramCost < 0)
            throw new ArgumentOutOfRangeException(nameof(ramCost));

        Pid = pid;
        Filename = filename;
        HostServerId = hostServerId;
        Args = args.ToArray();
        Threads = threads;
        RamCost = ramCost;
        Cancellation = cancellation;
    }

    public int Pid { get; }
    public string Filename { get; }
    public ServerId HostServerId { get; }
    public IReadOnlyList<string> Args { get; }
    public int Threads { get; }

    /// <summary>
    /// Total RAM reserved by this process, across all threads.
    /// </summary>
    public double RamCost { get; }
    public bool RamReleased { get; set; } = false;
    public ScriptProcessState State { get; internal set; } = ScriptProcessState.Running;
    public Exception? Failure { get; internal set; }
    internal CancellationTokenSource Cancellation { get; }
    internal Task? Execution { get; set; }
}