using System.Collections.Concurrent;
using System.Diagnostics;

namespace NullByte.Core.Time;

/// <summary>
/// Queues callbacks for execution on the thread that created this context.
/// </summary>
public sealed class GameSynchronizationContext : SynchronizationContext
{
    private sealed record Callback(
        SendOrPostCallback Action,
        object? State);

    private readonly ConcurrentQueue<Callback> callbacks = new();
    private readonly int gameThreadId;

    public GameSynchronizationContext()
    {
        gameThreadId = Environment.CurrentManagedThreadId;
    }
    
    /// <summary>
     /// Gets whether the current thread is the game thread.
     /// </summary>
    public bool IsOnGameThread =>
        Environment.CurrentManagedThreadId == gameThreadId;


    /// <summary>
    /// Throws if called from a thread other than the game thread.
    /// </summary>
    public void VerifyAccess()
    {
        if (!IsOnGameThread)
        {
            throw new InvalidOperationException(
                "This operation must be performed on the game thread.");
        }
    }

    /// <summary>
    /// Enqueues a callback for execution on the game thread.
    /// May be called from any thread.
    /// </summary>
    public override void Post(SendOrPostCallback callback, object? state)
    {
        ArgumentNullException.ThrowIfNull(callback);

        callbacks.Enqueue(new Callback(callback, state));
    }

    /// <summary>
    /// Executes queued callbacks on the game thread.
    /// </summary>
    /// <returns>The number of callbacks executed.</returns>
    public int ProcessPendingCallbacks(int maxCallbacks = int.MaxValue)
    {
        VerifyAccess();

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCallbacks);

        int processed = 0;

        while (processed < maxCallbacks &&
               callbacks.TryDequeue(out Callback? callback))
        {
            callback.Action(callback.State);
            processed++;
        }

        return processed;
    }

    public override void Send(SendOrPostCallback callback, object? state)
    {
        ArgumentNullException.ThrowIfNull(callback);
        VerifyAccess();

        callback(state);
    }

    public override SynchronizationContext CreateCopy() => this;

    /// <summary>
    /// Starts an asynchronous operation on the game thread, ensuring that
    /// its captured continuations return to this synchronization context.
    /// </summary>
    public Task RunAsync(Func<Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        VerifyAccess();

        SynchronizationContext? previousContext = Current;

        try
        {
            SetSynchronizationContext(this);

            return operation()
                ?? throw new InvalidOperationException(
                    "The asynchronous operation returned a null task.");
        }
        finally
        {
            SetSynchronizationContext(previousContext);
        }
    }
}