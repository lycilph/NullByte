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
    public void DelayAsync_ZeroDurationCompletesImmediately()
    {
        var clock = new FakeGameClock();
        var scheduler = new GameScheduler(clock);

        var delay = scheduler.DelayAsync(TimeSpan.Zero);

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

        Assert.True(Task.WhenAll(slow, fast).IsCompleted);
        Assert.Equal(["fast", "slow"], completionOrder);
    }

    [Fact]
    public void Update_CompletesEqualDeadlinesInCreationOrder()
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

        Assert.True(Task.WhenAll(first, second, third).IsCompleted);
        Assert.Equal(["first", "second", "third"], completionOrder);
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