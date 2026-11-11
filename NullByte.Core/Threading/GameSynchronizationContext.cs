using System.Collections.Concurrent;

namespace NullByte.Core.Threading;

/// <summary>
/// A synchronization context that queues callbacks for execution on one
/// designated game thread. Any thread may <see cref="Post"/>; only the game
/// thread may drain.
/// </summary>
public sealed class GameSynchronizationContext : SynchronizationContext
{
    private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> queue = new();

    // Managed thread ids start at 1, so 0 means "not bound yet".
    private int gameThreadId;
    private volatile bool isCompleted;

    public bool IsBound => gameThreadId != 0;

    public bool IsGameThread => gameThreadId == Environment.CurrentManagedThreadId;

    public bool IsCompleted => isCompleted;

    public int PendingCount => queue.Count;

    /// <summary>
    /// Binds this context to the calling thread and installs it as that
    /// thread's current <see cref="SynchronizationContext"/>.
    /// </summary>
    public void BindToCurrentThread()
    {
        if (isCompleted)
            throw new InvalidOperationException("A completed synchronization context cannot be bound.");

        if (Interlocked.CompareExchange(ref gameThreadId, Environment.CurrentManagedThreadId, 0) != 0)
            throw new InvalidOperationException("The synchronization context is already bound to a thread.");

        SetSynchronizationContext(this);
    }

    public void Release() => SetSynchronizationContext(null);

    /// <summary>
    /// Queues a callback for execution on the game thread. Safe to call from any thread.
    /// </summary>
    public override void Post(SendOrPostCallback callback, object? state)
    {
        ArgumentNullException.ThrowIfNull(callback);

        if (isCompleted)
            throw new InvalidOperationException("The synchronization context has completed and no longer accepts work.");

        // A Post that races with Complete() may enqueue after the queue was
        // cleared. That callback is simply never run, which is what Complete()
        // promises anyway, so no lock is needed.
        queue.Enqueue((callback, state));
    }

    /// <summary>
    /// Blocking cross-thread calls are not supported: they invite deadlocks with
    /// the game loop. Use <see cref="Post"/> or <c>GameLoop.InvokeAsync</c>.
    /// </summary>
    public override void Send(SendOrPostCallback callback, object? state)
        => throw new NotSupportedException("Send is not supported. Use Post, or GameLoop.InvokeAsync to await a result.");

    // The default implementation would return a plain SynchronizationContext
    // that posts to the thread pool, silently leaving the game thread.
    public override SynchronizationContext CreateCopy() => this;

    /// <summary>
    /// Runs queued callbacks until the queue is empty, including callbacks posted
    /// while draining. A callback that keeps re-posting itself never returns;
    /// the game loop uses <see cref="DrainBatch"/> instead.
    /// </summary>
    public int Drain() => DrainCore(int.MaxValue);

    /// <summary>
    /// Runs only the callbacks that were queued when the call started. Callbacks
    /// posted meanwhile wait for the next batch, so a script that awaits
    /// Task.Yield() in a loop cannot starve the scheduler or the rest of the game.
    /// </summary>
    public int DrainBatch() => DrainCore(queue.Count);

    /// <summary>
    /// Stops accepting work and discards whatever is still queued.
    /// </summary>
    public void Complete()
    {
        isCompleted = true;
        queue.Clear();
    }

    /// <summary>
    /// Throws if the calling thread is not the game thread.
    /// </summary>
    public void VerifyAccess()
    {
        if (!IsGameThread)
            throw new InvalidOperationException("This operation must be performed on the game thread.");
    }

    private int DrainCore(int maxItems)
    {
        VerifyAccess();

        var processed = 0;
        List<Exception>? exceptions = null;

        while (processed < maxItems && queue.TryDequeue(out var item))
        {
            processed++;

            try
            {
                item.Callback(item.State);
            }
            catch (Exception exception)
            {
                // One failing callback must not stop the others in the batch.
                (exceptions ??= []).Add(exception);
            }
        }

        if (exceptions is not null)
            throw new AggregateException("One or more callbacks posted to the game thread failed.", exceptions);

        return processed;
    }
}