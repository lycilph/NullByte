using NullByte.Core.Services;

namespace NullByte.Shell.Tests.Infrastructure;

/// <summary>
/// Game output that remembers every line, and lets a test wait for a line that another thread is about to write.
/// </summary>
internal sealed class RecordingOutput : IGameOutput
{
    private readonly List<string> lines = [];
    private readonly List<(string Line, TaskCompletionSource Signal)> waiters = [];

    public IReadOnlyList<string> Lines
    {
        get
        {
            lock (lines)
                return lines.ToArray();
        }
    }

    public void WriteLine(string message)
    {
        lock (lines)
        {
            lines.Add(message);

            foreach (var waiter in waiters.Where(w => w.Line == message))
                waiter.Signal.TrySetResult();
        }
    }

    public void WriteLine() => WriteLine(string.Empty);

    /// <summary>Completes when the line has been written (it may already have been), or fails after the timeout.</summary>
    public Task WaitForLineAsync(string line, TimeSpan timeout)
    {
        var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (lines)
        {
            if (lines.Contains(line))
                return Task.CompletedTask;

            waiters.Add((line, signal));
        }

        return signal.Task.WaitAsync(timeout);
    }
}
