namespace NullByte.Scripting;

// ---------------------------------------------------------------------------
// ScriptInstance: a compiled script and its live state. The instance object
// is the script's "body" — output, error, data. Execution is a running Task
// suspended on awaits; it is never serialized (source restart model).
// ---------------------------------------------------------------------------

public sealed class ScriptInstance
{
    public string Name { get; init; } = "";
    public string Source { get; init; } = "";
    public ScriptData Data { get; internal set; } = new();

    public List<string> Output { get; } = [];
    public long OutputTotal { get; internal set; }
    public Exception? Error { get; internal set; }
    public bool IsStopRequested { get; internal set; }

    internal Task? Execution { get; set; }
    internal TaskCompletionSource Stopped { get; }
        = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal int ApiCallsThisTick { get; set; }

    public bool IsRunning
        => !IsStopRequested && Error is null && Execution is { IsCompleted: false };

    internal void AddOutput(string message)
    {
        Output.Add($"[{Name}] {message}");
        OutputTotal++;
        if (Output.Count > 50)
            Output.RemoveAt(0);
    }
}