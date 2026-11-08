namespace NullByte.Core.Time;

/// <summary>
/// Schedules tasks for completion at a future point in game time.
/// </summary>
/// <remarks>
/// <para>
/// The scheduler is intended to be accessed from the game thread.
/// Cancellation may be requested from any thread.
/// </para>
/// <para>
/// Delays never run their continuations inline. Completing or cancelling a
/// delay only marks its task as finished; code awaiting it resumes through the
/// awaiter's captured context (on the game thread, the game synchronization
/// context). This keeps script code from running inside
/// <see cref="Update"/> or inside whoever called Cancel.
/// </para>
/// </remarks>
public sealed class GameScheduler
{
    private readonly IGameClock clock;

    private readonly PriorityQueue<
        ScheduledDelay,
        (long DueTicks, long Sequence)> scheduledDelays = new();

    private long nextSequence;

    public GameScheduler(IGameClock clock)
    {
        this.clock = clock
            ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>
    /// Gets the number of delays currently waiting in the scheduler.
    /// </summary>
    public int PendingCount => scheduledDelays.Count;

    /// <summary>
    /// Returns a task that completes after the specified amount
    /// of game time has elapsed.
    /// </summary>
    public Task DelayAsync(
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(duration),
                "Delay duration cannot be negative.");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        var dueTime = AddWithSaturation(
            clock.Elapsed,
            duration);

        var scheduledDelay = new ScheduledDelay(
            dueTime,
            cancellationToken);

        var sequence = nextSequence++;

        scheduledDelays.Enqueue(
            scheduledDelay,
            (dueTime.Ticks, sequence));

        return scheduledDelay.Task;
    }

    /// <summary>
    /// Completes all delays that are due at the clock's current time.
    /// </summary>
    /// <returns>
    /// The number of scheduled entries removed from the queue.
    /// </returns>
    public int Update()
    {
        var processedCount = 0;
        var currentTime = clock.Elapsed;

        while (scheduledDelays.TryPeek(
                   out var scheduledDelay,
                   out var priority))
        {
            /*
             * Canceled delays may be removed immediately when they
             * reach the front of the queue, even if their original
             * due time has not yet arrived.
             */
            if (scheduledDelay.IsCompleted)
            {
                scheduledDelays.Dequeue();
                scheduledDelay.Dispose();
                processedCount++;
                continue;
            }

            if (priority.DueTicks > currentTime.Ticks)
                break;

            scheduledDelays.Dequeue();

            scheduledDelay.TryComplete();
            scheduledDelay.Dispose();

            processedCount++;
        }

        return processedCount;
    }

    private static TimeSpan AddWithSaturation(
        TimeSpan left,
        TimeSpan right)
    {
        if (right.Ticks > TimeSpan.MaxValue.Ticks - left.Ticks)
            return TimeSpan.MaxValue;

        return left + right;
    }

    private sealed class ScheduledDelay : IDisposable
    {
        private readonly TaskCompletionSource completionSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationToken cancellationToken;
        private CancellationTokenRegistration cancellationRegistration;

        public ScheduledDelay(
            TimeSpan dueTime,
            CancellationToken cancellationToken)
        {
            DueTime = dueTime;
            this.cancellationToken = cancellationToken;

            if (!cancellationToken.CanBeCanceled)
                return;

            cancellationRegistration = cancellationToken.Register(
                static state =>
                {
                    var delay = (ScheduledDelay)state!;
                    delay.TryCancel();
                },
                this);

            /*
             * Cancellation can occur while Register is running.
             * If that happened, dispose the newly created registration.
             */
            if (IsCompleted)
                cancellationRegistration.Dispose();
        }

        public TimeSpan DueTime { get; }

        public Task Task => completionSource.Task;
        public bool IsCompleted => completionSource.Task.IsCompleted;
        public void TryComplete() => completionSource.TrySetResult();
        private void TryCancel() => completionSource.TrySetCanceled(cancellationToken);
        public void Dispose() => cancellationRegistration.Dispose();
    }
}