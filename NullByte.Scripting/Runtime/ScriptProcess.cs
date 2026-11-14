namespace NullByte.Scripting.Runtime;

/// <summary>
/// Represents one running or completed player script process.
/// </summary>
public sealed class ScriptProcess
{
    internal ScriptProcess(
        int pid,
        string fileName,
        string hostname,
        IReadOnlyList<string> args,
        int threads,
        double ramCost,
        CancellationTokenSource cancellation)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pid, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(threads, 1);
        
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostname);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(cancellation);

        if (!double.IsFinite(ramCost) || ramCost < 0)
            throw new ArgumentOutOfRangeException(nameof(ramCost));

        Pid = pid;
        FileName = fileName;
        Hostname = hostname;
        Args = args.ToArray();
        Threads = threads;
        RamCost = ramCost;
        Cancellation = cancellation;
    }

    public int Pid { get; }
    public string FileName { get; }
    public string Hostname { get; }
    public IReadOnlyList<string> Args { get; }
    public int Threads { get; }

    /// <summary>
    /// Total RAM reserved by this process, across all threads.
    /// </summary>
    public double RamCost { get; }
    public ScriptProcessState State { get; internal set; } = ScriptProcessState.Running;
    public Exception? Failure { get; internal set; }
    internal CancellationTokenSource Cancellation { get; }
    internal Task? Execution { get; set; }
}