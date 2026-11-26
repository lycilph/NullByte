using NullByte.Core.Simulation;
using NullByte.Core.Tests.Infrastructure;
using NullByte.Core.Threading;

namespace NullByte.Core.Tests.Simulation;

/// <summary>
/// Covers the game loop's lifecycle and how it runs callbacks. These tests run the real game thread and clock
/// because that is the behavior under test; <see cref="GameLoopHost"/> keeps the start/stop/timeout plumbing out of
/// the tests.
/// </summary>
public sealed class GameLoopTests
{
    [Fact]
    public async Task NewLoop_IsNotRunningAndLifecycleTasksAreIncomplete()
    {
        await using var host = new GameLoopHost();

        Assert.False(host.Loop.IsRunning);
        Assert.False(host.Loop.Started.IsCompleted);
        Assert.False(host.Loop.Completion.IsCompleted);
    }

    [Fact]
    public async Task Start_RunsTheLoopOnADedicatedGameThread()
    {
        await using var host = new GameLoopHost();

        await host.StartAsync();
        var gameThreadId = await GetGameThreadIdAsync(host.Loop);

        Assert.True(host.Loop.IsRunning);
        Assert.NotEqual(Environment.CurrentManagedThreadId, gameThreadId);

        await host.StopAsync();

        Assert.False(host.Loop.IsRunning);
    }

    [Fact]
    public async Task Start_CalledTwice_Throws()
    {
        await using var host = new GameLoopHost();
        await host.StartAsync();

        var exception = Assert.Throws<InvalidOperationException>(host.Loop.Start);

        Assert.Contains("only be started once", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RequestStop_WhileRunning_CompletesTheLoopAndItsSynchronizationContext()
    {
        await using var host = new GameLoopHost();
        await host.StartAsync();

        Assert.True(host.Loop.IsRunning);
        Assert.False(host.Context.IsCompleted);

        await host.StopAsync();

        Assert.True(host.Loop.Completion.IsCompletedSuccessfully);
        Assert.True(host.Context.IsCompleted);
        Assert.False(host.Loop.IsRunning);
    }

    [Fact]
    public async Task RequestStop_CalledRepeatedly_StopsTheLoopOnce()
    {
        await using var host = new GameLoopHost();
        await host.StartAsync();

        host.Loop.RequestStop();
        host.Loop.RequestStop();
        host.Loop.RequestStop();
        await GameLoopHost.WithinTimeout(host.Loop.Completion);

        Assert.True(host.Loop.Completion.IsCompletedSuccessfully);
        Assert.False(host.Loop.IsRunning);
    }

    [Fact]
    public async Task RequestStop_BeforeStart_CompletesWithoutCreatingAGameThread()
    {
        await using var host = new GameLoopHost();

        host.Loop.RequestStop();

        Assert.False(host.Loop.IsRunning);
        await GameLoopHost.WithinTimeout(host.Loop.Completion);
        Assert.True(host.Loop.Completion.IsCompletedSuccessfully);
        Assert.True(host.Loop.Started.IsCanceled);
    }

    [Fact]
    public async Task RequestStop_BeforeStart_PreventsALaterStart()
    {
        await using var host = new GameLoopHost();

        host.Loop.RequestStop();

        Assert.Throws<InvalidOperationException>(host.Loop.Start);
    }

    [Fact]
    public async Task Start_WhenTheContextIsBoundToAnotherThread_StillCompletesTheContext()
    {
        // The game thread cannot bind a context that is already bound elsewhere, so it fails on startup.
        var context = new GameSynchronizationContext();
        var binder = new Thread(context.BindToCurrentThread);
        binder.Start();
        binder.Join();

        await using var host = new GameLoopHost(context: context);

        host.Loop.Start();

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await GameLoopHost.WithinTimeout(host.Loop.Completion));

        Assert.True(context.IsCompleted);
        Assert.False(host.Loop.IsRunning);
    }

    [Fact]
    public async Task Start_WhenTheContextIsAlreadyCompleted_FaultsStartedAndCompletion()
    {
        // A completed context cannot be bound either.
        var context = new GameSynchronizationContext();
        context.Complete();

        await using var host = new GameLoopHost(context: context);

        host.Loop.Start();

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await GameLoopHost.WithinTimeout(host.Loop.Started));
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await GameLoopHost.WithinTimeout(host.Loop.Completion));

        Assert.False(host.Loop.IsRunning);
    }

    [Fact]
    public async Task Post_BeforeStart_Throws()
    {
        await using var host = new GameLoopHost();

        var exception = Assert.Throws<InvalidOperationException>(() => host.Loop.Post(_ => { }));

        Assert.Contains("only be posted", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_AfterStopWasRequested_Throws()
    {
        await using var host = new GameLoopHost();
        await host.StartAsync();

        host.Loop.RequestStop();

        var exception = Assert.Throws<InvalidOperationException>(() => host.Loop.Post(_ => { }));

        Assert.Contains("only be posted", exception.Message, StringComparison.OrdinalIgnoreCase);
        await GameLoopHost.WithinTimeout(host.Loop.Completion);
    }

    [Fact]
    public async Task Post_AfterTheLoopStopped_Throws()
    {
        await using var host = new GameLoopHost();
        await host.StartAsync();
        await host.StopAsync();

        Assert.False(host.Loop.IsRunning);
        Assert.Throws<InvalidOperationException>(() => host.Loop.Post(_ => { }));
    }

    [Fact]
    public async Task Post_RunsCallbacksOnTheGameThread()
    {
        await using var host = new GameLoopHost();
        await host.StartAsync();
        var gameThreadId = await GetGameThreadIdAsync(host.Loop);

        var callbackThreadIds = Enumerable.Range(0, 3)
            .Select(_ => new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously))
            .ToArray();

        foreach (var source in callbackThreadIds)
            host.Loop.Post(_ => source.TrySetResult(Environment.CurrentManagedThreadId));

        var observed = await GameLoopHost.WithinTimeout(Task.WhenAll(callbackThreadIds.Select(source => source.Task)));

        Assert.All(observed, threadId => Assert.Equal(gameThreadId, threadId));
    }

    [Fact]
    public async Task Post_RunsCallbacksInPostingOrder()
    {
        await using var host = new GameLoopHost();
        await host.StartAsync();

        var executionOrder = new List<string>();
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        host.Loop.Post(_ => executionOrder.Add("first"));
        host.Loop.Post(_ => executionOrder.Add("second"));
        host.Loop.Post(_ =>
        {
            executionOrder.Add("third");
            completed.TrySetResult();
        });

        await GameLoopHost.WithinTimeout(completed.Task);

        Assert.Equal(["first", "second", "third"], executionOrder);
    }

    [Fact]
    public async Task Post_WhenACallbackFailsWithoutASubscriber_KeepsTheLoopRunning()
    {
        await using var host = new GameLoopHost();
        await host.StartAsync();

        host.Loop.Post(_ => throw new InvalidOperationException("Expected game-loop failure."));

        // Work posted afterwards still runs, so the loop survived.
        var result = await GameLoopHost.WithinTimeout(host.Loop.InvokeAsync(() => Task.FromResult(42)));

        Assert.Equal(42, result);
        Assert.True(host.Loop.IsRunning);
        Assert.False(host.Loop.Completion.IsCompleted);
    }

    [Fact]
    public async Task InvokeAsync_RunsTheCallbackOnTheGameThread()
    {
        await using var host = new GameLoopHost();
        await host.StartAsync();
        var gameThreadId = await GetGameThreadIdAsync(host.Loop);

        var callbackThreadId = 0;

        await GameLoopHost.WithinTimeout(host.Loop.InvokeAsync(() =>
        {
            callbackThreadId = Environment.CurrentManagedThreadId;

            return Task.CompletedTask;
        }));

        Assert.Equal(gameThreadId, callbackThreadId);
    }

    [Fact]
    public async Task InvokeAsync_ReturnsTheCallbackResult()
    {
        await using var host = new GameLoopHost();
        await host.StartAsync();

        var result = await GameLoopHost.WithinTimeout(host.Loop.InvokeAsync(() => Task.FromResult(42)));

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task InvokeAsync_DoesNotCompleteUntilTheAsyncCallbackDoes()
    {
        await using var host = new GameLoopHost();
        await host.StartAsync();

        var callbackStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCallback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var invocation = host.Loop.InvokeAsync(async () =>
        {
            callbackStarted.TrySetResult();

            await releaseCallback.Task;
        });

        await GameLoopHost.WithinTimeout(callbackStarted.Task);

        Assert.False(invocation.IsCompleted);

        releaseCallback.TrySetResult();
        await GameLoopHost.WithinTimeout(invocation);

        Assert.True(invocation.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task InvokeAsync_WhenTheCallbackFails_PropagatesTheExceptionAndKeepsRunning()
    {
        await using var host = new GameLoopHost();
        await host.StartAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await host.Loop.InvokeAsync(
                () => Task.FromException(new InvalidOperationException("Expected invocation failure."))));

        Assert.Equal("Expected invocation failure.", exception.Message);
        Assert.True(host.Loop.IsRunning);
    }

    [Fact]
    public async Task InvokeAsync_WhenTheCallbackIsCanceled_ReturnsACanceledTaskAndKeepsRunning()
    {
        await using var host = new GameLoopHost();
        await host.StartAsync();

        using var cancellation = new CancellationTokenSource();
        var callbackStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var invocation = host.Loop.InvokeAsync(async () =>
        {
            callbackStarted.TrySetResult();

            await Task.Delay(Timeout.InfiniteTimeSpan, cancellation.Token);
        });

        await GameLoopHost.WithinTimeout(callbackStarted.Task);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await GameLoopHost.WithinTimeout(invocation));

        Assert.True(invocation.IsCanceled);
        Assert.True(host.Loop.IsRunning);
    }

    [Fact]
    public async Task InvokeAsync_AfterTheLoopStopped_Throws()
    {
        await using var host = new GameLoopHost();
        await host.StartAsync();
        await host.StopAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.Loop.InvokeAsync(() => Task.CompletedTask));

        Assert.Contains("only be posted", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ScheduledDelay_ResumesOnTheGameThread()
    {
        await using var host = new GameLoopHost();
        await host.StartAsync();

        var observationSource = new TaskCompletionSource<ThreadObservation>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        host.Loop.Post(_ => { _ = ObserveScheduledDelayAsync(host, observationSource); });

        var observation = await GameLoopHost.WithinTimeout(observationSource.Task);

        Assert.Equal(observation.ThreadBeforeDelay, observation.ThreadAfterDelay);
        Assert.True(observation.WasGameThreadBeforeDelay);
        Assert.True(observation.WasGameThreadAfterDelay);
    }

    [Fact]
    public async Task ConcurrentScheduledDelays_ResumeOnTheGameThreadInDueOrder()
    {
        await using var host = new GameLoopHost();
        await host.StartAsync();

        var completionSource = new TaskCompletionSource<IReadOnlyList<OperationObservation>>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        host.Loop.Post(_ => { _ = ObserveConcurrentDelaysAsync(host, completionSource); });

        var observations = await GameLoopHost.WithinTimeout(completionSource.Task);

        Assert.Equal(["short", "long"], observations.Select(observation => observation.Name).ToArray());
        Assert.All(observations, observation =>
        {
            Assert.Equal(observation.ThreadBeforeDelay, observation.ThreadAfterDelay);
            Assert.True(observation.WasGameThreadAfterDelay);
        });
    }

    private static async Task<int> GetGameThreadIdAsync(GameLoop gameLoop)
    {
        var threadIdSource = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        gameLoop.Post(_ => threadIdSource.TrySetResult(Environment.CurrentManagedThreadId));

        return await GameLoopHost.WithinTimeout(threadIdSource.Task);
    }

    private static async Task ObserveScheduledDelayAsync(
        GameLoopHost host,
        TaskCompletionSource<ThreadObservation> observationSource)
    {
        try
        {
            var threadBeforeDelay = Environment.CurrentManagedThreadId;
            var wasGameThreadBeforeDelay = host.Loop.IsGameThread;

            await host.Scheduler.DelayAsync(TimeSpan.FromMilliseconds(25));

            observationSource.TrySetResult(new ThreadObservation(
                threadBeforeDelay,
                Environment.CurrentManagedThreadId,
                wasGameThreadBeforeDelay,
                host.Loop.IsGameThread));
        }
        catch (Exception exception)
        {
            observationSource.TrySetException(exception);
        }
    }

    private static async Task ObserveConcurrentDelaysAsync(
        GameLoopHost host,
        TaskCompletionSource<IReadOnlyList<OperationObservation>> completionSource)
    {
        try
        {
            var completionOrder = new List<OperationObservation>();

            var longOperation = ObserveDelayAsync(host, "long", TimeSpan.FromMilliseconds(40), completionOrder);
            var shortOperation = ObserveDelayAsync(host, "short", TimeSpan.FromMilliseconds(15), completionOrder);

            await Task.WhenAll(longOperation, shortOperation);

            completionSource.TrySetResult(completionOrder);
        }
        catch (Exception exception)
        {
            completionSource.TrySetException(exception);
        }
    }

    private static async Task ObserveDelayAsync(
        GameLoopHost host,
        string name,
        TimeSpan duration,
        ICollection<OperationObservation> completionOrder)
    {
        var threadBeforeDelay = Environment.CurrentManagedThreadId;

        await host.Scheduler.DelayAsync(duration);

        completionOrder.Add(new OperationObservation(
            name,
            threadBeforeDelay,
            Environment.CurrentManagedThreadId,
            host.Loop.IsGameThread));
    }

    private sealed record ThreadObservation(
        int ThreadBeforeDelay,
        int ThreadAfterDelay,
        bool WasGameThreadBeforeDelay,
        bool WasGameThreadAfterDelay);

    private sealed record OperationObservation(
        string Name,
        int ThreadBeforeDelay,
        int ThreadAfterDelay,
        bool WasGameThreadAfterDelay);
}
