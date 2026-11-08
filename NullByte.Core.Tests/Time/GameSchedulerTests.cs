using NullByte.Core.Tests.Helpers;
using NullByte.Core.Time;

namespace NullByte.Core.Tests.Time;

public sealed class GameSchedulerTests
{
    [Fact]
    public async Task DelayAsync_CompletesWhenDueTimeIsReached()
    {
        var clock = new FakeGameClock();
        var scheduler = new GameScheduler(clock);

        var delay = scheduler.DelayAsync(TimeSpan.FromSeconds(10));

        Assert.False(delay.IsCompleted);

        clock.Advance(TimeSpan.FromSeconds(9));
        scheduler.Update();

        Assert.False(delay.IsCompleted);

        clock.Advance(TimeSpan.FromSeconds(1));
        scheduler.Update();

        await delay;

        Assert.True(delay.IsCompletedSuccessfully);
    }

    [Fact]
    public void DelayAsync_DoesNotCompleteUntilSchedulerIsUpdated()
    {
        var clock = new FakeGameClock();
        var scheduler = new GameScheduler(clock);

        var delay = scheduler.DelayAsync(TimeSpan.FromSeconds(5));

        clock.Advance(TimeSpan.FromSeconds(5));

        Assert.False(delay.IsCompleted);

        scheduler.Update();

        Assert.True(delay.IsCompletedSuccessfully);
    }

    [Fact]
    public void DelayAsync_ZeroDuration_CompletesOnNextUpdate()
    {
        var clock = new FakeGameClock();
        var scheduler = new GameScheduler(clock);

        var delay = scheduler.DelayAsync(TimeSpan.Zero);

        // A zero delay is scheduled like any other, not completed on the spot.
        Assert.False(delay.IsCompleted);
        Assert.Equal(1, scheduler.PendingCount);

        // It completes on the next Update, even though the clock has not moved.
        scheduler.Update();

        Assert.True(delay.IsCompletedSuccessfully);
        Assert.Equal(0, scheduler.PendingCount);
    }

    [Fact]
    public async Task DelayAsync_NegativeDurationThrows()
    {
        var clock = new FakeGameClock();
        var scheduler = new GameScheduler(clock);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => scheduler.DelayAsync(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void Update_CompletesDelaysInDueTimeOrder()
    {
        GameThreadHarness.Run(context =>
        {
            var clock = new FakeGameClock();
            var scheduler = new GameScheduler(clock);
            var completionOrder = new List<string>();

            var slow = ObserveCompletionAsync(
                scheduler.DelayAsync(TimeSpan.FromSeconds(10)),
                "slow",
                completionOrder);

            var fast = ObserveCompletionAsync(
                scheduler.DelayAsync(TimeSpan.FromSeconds(5)),
                "fast",
                completionOrder);

            clock.Advance(TimeSpan.FromSeconds(10));
            scheduler.Update();
            context.Drain();

            Assert.True(Task.WhenAll(slow, fast).IsCompleted);
            Assert.Equal(["fast", "slow"], completionOrder);
        });
    }

    [Fact]
    public void Update_CompletesEqualDeadlinesInCreationOrder()
    {
        GameThreadHarness.Run(context =>
        {
            var clock = new FakeGameClock();
            var scheduler = new GameScheduler(clock);
            var completionOrder = new List<string>();

            var first = ObserveCompletionAsync(
                scheduler.DelayAsync(TimeSpan.FromSeconds(5)),
                "first",
                completionOrder);

            var second = ObserveCompletionAsync(
                scheduler.DelayAsync(TimeSpan.FromSeconds(5)),
                "second",
                completionOrder);

            var third = ObserveCompletionAsync(
                scheduler.DelayAsync(TimeSpan.FromSeconds(5)),
                "third",
                completionOrder);

            clock.Advance(TimeSpan.FromSeconds(5));
            scheduler.Update();
            context.Drain();

            Assert.True(Task.WhenAll(first, second, third).IsCompleted);
            Assert.Equal(["first", "second", "third"], completionOrder);
        });
    }

    [Fact]
    public void Update_QueuesContinuationsInsteadOfRunningThemInline()
    {
        GameThreadHarness.Run(context =>
        {
            var clock = new FakeGameClock();
            var scheduler = new GameScheduler(clock);
            var completionOrder = new List<string>();

            _ = ObserveCompletionAsync(
                scheduler.DelayAsync(TimeSpan.FromSeconds(1)),
                "script",
                completionOrder);

            clock.Advance(TimeSpan.FromSeconds(1));
            scheduler.Update();

            Assert.Empty(completionOrder);
            Assert.Equal(1, context.PendingCount);

            context.Drain();

            Assert.Equal(["script"], completionOrder);
        });
    }

    [Fact]
    public void Cancel_QueuesContinuationsInsteadOfRunningThemInCallersStack()
    {
        GameThreadHarness.Run(context =>
        {
            var scheduler = new GameScheduler(new FakeGameClock());
            using var cancellation = new CancellationTokenSource();
            var events = new List<string>();

            async Task ScriptAsync()
            {
                try
                {
                    await scheduler.DelayAsync(TimeSpan.FromHours(1), cancellation.Token);
                }
                catch (OperationCanceledException)
                {
                    events.Add("script cleanup");
                }
            }

            _ = ScriptAsync();

            cancellation.Cancel();
            events.Add("kill returned");

            Assert.Equal(["kill returned"], events);

            context.Drain();

            Assert.Equal(["kill returned", "script cleanup"], events);
        });
    }

    [Fact]
    public async Task DelayAsync_CanceledBeforeSchedulingReturnsCanceledTask()
    {
        var clock = new FakeGameClock();
        var scheduler = new GameScheduler(clock);

        using var cancellationSource = new CancellationTokenSource();

        cancellationSource.Cancel();

        var delay = scheduler.DelayAsync(
            TimeSpan.FromSeconds(5),
            cancellationSource.Token);

        Assert.True(delay.IsCanceled);
        Assert.Equal(0, scheduler.PendingCount);

        await Assert.ThrowsAsync<TaskCanceledException>(async () => await delay);
    }

    [Fact]
    public async Task DelayAsync_CanBeCanceledWhilePending()
    {
        var clock = new FakeGameClock();
        var scheduler = new GameScheduler(clock);

        using var cancellationSource = new CancellationTokenSource();

        var delay = scheduler.DelayAsync(
            TimeSpan.FromSeconds(10),
            cancellationSource.Token);

        Assert.Equal(1, scheduler.PendingCount);

        cancellationSource.Cancel();

        Assert.True(delay.IsCanceled);

        await Assert.ThrowsAsync<TaskCanceledException>(async () => await delay);
    }

    [Fact]
    public void Update_RemovesCanceledDelayAtFrontOfQueue()
    {
        var clock = new FakeGameClock();
        var scheduler = new GameScheduler(clock);

        using var cancellationSource = new CancellationTokenSource();

        scheduler.DelayAsync(
            TimeSpan.FromSeconds(10),
            cancellationSource.Token);

        cancellationSource.Cancel();

        Assert.Equal(1, scheduler.PendingCount);

        var processed = scheduler.Update();

        Assert.Equal(1, processed);
        Assert.Equal(0, scheduler.PendingCount);
    }

    [Fact]
    public void Scheduler_UsesCurrentClockTimeWhenCalculatingDeadline()
    {
        var clock = new FakeGameClock(TimeSpan.FromSeconds(100));
        var scheduler = new GameScheduler(clock);
        var delay = scheduler.DelayAsync(TimeSpan.FromSeconds(5));

        clock.Advance(TimeSpan.FromSeconds(4));
        scheduler.Update();

        Assert.False(delay.IsCompleted);

        clock.Advance(TimeSpan.FromSeconds(1));
        scheduler.Update();

        Assert.True(delay.IsCompletedSuccessfully);
    }

    private static async Task ObserveCompletionAsync(
        Task task,
        string name,
        ICollection<string> completionOrder)
    {
        await task;

        completionOrder.Add(name);
    }
}