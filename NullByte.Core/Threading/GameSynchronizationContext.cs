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

    private int? gameThreadId;

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
    /// Gets the number of callbacks currently waiting to execute.
    /// </summary>
    public int PendingCount => workItems.Count;

    /// <summary>
    /// Binds this synchronization context to the calling thread.
    /// </summary>
    /// <remarks>
    /// The context can only be bound once.
    /// </remarks>
    public void BindToCurrentThread()
    {
        var currentThreadId = Environment.CurrentManagedThreadId;

        if (gameThreadId.HasValue)
        {
            throw new InvalidOperationException(
                gameThreadId.Value == currentThreadId
                    ? "The synchronization context is already bound " +
                      "to the current thread."
                    : "The synchronization context is already bound " +
                      "to another thread.");
        }

        gameThreadId = currentThreadId;

        SetSynchronizationContext(this);
    }

    /// <summary>
    /// Queues a callback for asynchronous execution on the game thread.
    /// </summary>
    public override void Post(SendOrPostCallback callback, object? state)
    {
        ArgumentNullException.ThrowIfNull(callback);

        workItems.Enqueue(
            new WorkItem(
                callback,
                state,
                completion: null));
    }

    /// <summary>
    /// Executes a callback synchronously on the game thread.
    /// </summary>
    /// <remarks>
    /// If called from the game thread, the callback executes immediately.
    /// Otherwise, it is queued and the calling thread blocks until the
    /// game thread processes it.
    /// </remarks>
    public override void Send(SendOrPostCallback callback, object? state)
    {
        ArgumentNullException.ThrowIfNull(callback);
        EnsureBound();

        if (IsGameThread)
        {
            callback(state);
            return;
        }

        using var completion = new ManualResetEventSlim(initialState: false);

        var workItem = new WorkItem(
            callback,
            state,
            completion);

        workItems.Enqueue(workItem);

        completion.Wait();

        workItem.Exception?.Throw();
    }

    /// <summary>
    /// Executes all callbacks that were queued before or during
    /// this drain operation.
    /// </summary>
    /// <returns>The number of callbacks executed.</returns>
    /// <exception cref="AggregateException">
    /// One or more callbacks failed. All queued callbacks are still
    /// given an opportunity to execute before the exception is thrown.
    /// </exception>
    public int Drain()
    {
        EnsureGameThread();

        var processedCount = 0;
        List<Exception>? exceptions = null;

        while (workItems.TryDequeue(out var workItem))
        {
            try
            {
                workItem.Callback(workItem.State);
            }
            catch (Exception exception)
            {
                workItem.Exception = ExceptionDispatchInfo.Capture(exception);

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
                "One or more callbacks posted to the game thread failed.",
                exceptions);
        }

        return processedCount;
    }

    /// <summary>
    /// Throws if the calling thread is not the game thread.
    /// </summary>
    public void VerifyAccess()
    {
        EnsureGameThread();
    }

    /// <summary>
    /// Creates a copy of this synchronization context.
    /// </summary>
    public override SynchronizationContext CreateCopy() => this;

    private void EnsureBound()
    {
        if (!gameThreadId.HasValue)
        {
            throw new InvalidOperationException(
                "The synchronization context has not been bound " +
                "to a game thread.");
        }
    }

    private void EnsureGameThread()
    {
        EnsureBound();

        if (!IsGameThread)
        {
            throw new InvalidOperationException("This operation must be performed on the game thread.");
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
    }
}