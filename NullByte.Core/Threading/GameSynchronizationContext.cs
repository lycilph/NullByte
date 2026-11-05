using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;

namespace NullByte.Core.Threading;

/// <summary>
/// A synchronization context that queues callbacks for execution
/// on one designated game thread.
/// </summary>
public sealed class GameSynchronizationContext : SynchronizationContext
{
    private readonly ConcurrentQueue<WorkItem> workItems = new();

    private readonly Lock lifecycleLock = new();

    private int? gameThreadId;
    private bool isCompleted;

    /// <summary>
    /// Gets whether the context has been bound to a game thread.
    /// </summary>
    public bool IsBound => gameThreadId.HasValue;

    /// <summary>
    /// Gets whether the calling thread is the game thread.
    /// </summary>
    public bool IsGameThread =>
        gameThreadId == Environment.CurrentManagedThreadId;

    /// <summary>
    /// Gets whether the context has stopped accepting work.
    /// </summary>
    public bool IsCompleted
    {
        get
        {
            lock (lifecycleLock)
                return isCompleted;
        }
    }

    /// <summary>
    /// Gets the number of callbacks currently waiting to execute.
    /// </summary>
    public int PendingCount => workItems.Count;

    /// <summary>
    /// Binds this synchronization context to the calling thread.
    /// </summary>
    public void BindToCurrentThread()
    {
        lock (lifecycleLock)
        {
            if (isCompleted)
            {
                throw new InvalidOperationException(
                    "A completed synchronization context " +
                    "cannot be bound.");
            }

            var currentThreadId =
                Environment.CurrentManagedThreadId;

            if (gameThreadId.HasValue)
            {
                throw new InvalidOperationException(
                    gameThreadId.Value == currentThreadId
                        ? "The synchronization context is already " +
                          "bound to the current thread."
                        : "The synchronization context is already " +
                          "bound to another thread.");
            }

            gameThreadId = currentThreadId;
        }

        SetSynchronizationContext(this);
    }

    /// <summary>
    /// Queues a callback for asynchronous execution on the game thread.
    /// </summary>
    public override void Post(
        SendOrPostCallback callback,
        object? state)
    {
        ArgumentNullException.ThrowIfNull(callback);

        lock (lifecycleLock)
        {
            ThrowIfCompleted();

            workItems.Enqueue(
                new WorkItem(
                    callback,
                    state,
                    completion: null));
        }
    }

    /// <summary>
    /// Executes a callback synchronously on the game thread.
    /// </summary>
    public override void Send(
        SendOrPostCallback callback,
        object? state)
    {
        ArgumentNullException.ThrowIfNull(callback);

        EnsureBound();

        if (IsGameThread)
        {
            lock (lifecycleLock)
            {
                ThrowIfCompleted();
            }

            callback(state);
            return;
        }

        using var completion =
            new ManualResetEventSlim(initialState: false);

        var workItem = new WorkItem(
            callback,
            state,
            completion);

        lock (lifecycleLock)
        {
            ThrowIfCompleted();

            workItems.Enqueue(workItem);
        }

        completion.Wait();

        workItem.Exception?.Throw();
    }

    /// <summary>
    /// Executes all currently queued callbacks.
    /// </summary>
    public int Drain()
    {
        EnsureGameThread();

        var processedCount = 0;
        List<Exception>? exceptions = null;

        while (workItems.TryDequeue(out var workItem))
        {
            /*
             * Complete() may race with a drain and mark an item
             * as abandoned after it has been dequeued.
             */
            if (workItem.IsAbandoned)
            {
                workItem.Completion?.Set();
                processedCount++;
                continue;
            }

            try
            {
                workItem.Callback(workItem.State);
            }
            catch (Exception exception)
            {
                workItem.Exception =
                    ExceptionDispatchInfo.Capture(exception);

                exceptions ??= [];
                exceptions.Add(exception);
            }
            finally
            {
                processedCount++;
                workItem.Completion?.Set();
            }
        }

        if (exceptions is not null)
        {
            throw new AggregateException(
                "One or more callbacks posted to the " +
                "game thread failed.",
                exceptions);
        }

        return processedCount;
    }

    /// <summary>
    /// Stops accepting new work and releases synchronous callers
    /// whose queued callbacks will no longer execute.
    /// </summary>
    public void Complete()
    {
        lock (lifecycleLock)
        {
            if (isCompleted)
                return;

            isCompleted = true;

            while (workItems.TryDequeue(out var workItem))
            {
                /*
                 * Asynchronous Post callers are not waiting for a result,
                 * so those callbacks can simply be discarded.
                 *
                 * Send callers must be released with an exception.
                 */
                if (workItem.Completion is null)
                    continue;

                workItem.IsAbandoned = true;

                workItem.Exception =
                    ExceptionDispatchInfo.Capture(
                        new InvalidOperationException(
                            "The synchronization context completed " +
                            "before the callback could execute."));

                workItem.Completion.Set();
            }
        }
    }

    /// <summary>
    /// Throws if the calling thread is not the game thread.
    /// </summary>
    public void VerifyAccess()
    {
        EnsureGameThread();
    }

    public override SynchronizationContext CreateCopy()
    {
        return this;
    }

    private void ThrowIfCompleted()
    {
        if (isCompleted)
        {
            throw new InvalidOperationException(
                "The synchronization context has completed " +
                "and no longer accepts work.");
        }
    }

    private void EnsureBound()
    {
        if (!gameThreadId.HasValue)
        {
            throw new InvalidOperationException(
                "The synchronization context has not been " +
                "bound to a game thread.");
        }
    }

    private void EnsureGameThread()
    {
        EnsureBound();

        if (!IsGameThread)
        {
            throw new InvalidOperationException(
                "This operation must be performed on " +
                "the game thread.");
        }
    }

    private sealed class WorkItem
    {
        public WorkItem(
            SendOrPostCallback callback,
            object? state,
            ManualResetEventSlim? completion)
        {
            Callback = callback;
            State = state;
            Completion = completion;
        }

        public SendOrPostCallback Callback { get; }

        public object? State { get; }

        public ManualResetEventSlim? Completion { get; }

        public ExceptionDispatchInfo? Exception { get; set; }

        public bool IsAbandoned { get; set; }
    }
}