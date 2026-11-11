using NullByte.Core.Simulation;
using NullByte.Core.Threading;
using NullByte.Core.Time;

namespace NullByte.Core.Tests.Simulation;

public sealed class GameLoopTests
{
    [Fact]
    public async Task RequestStop_CompletesSynchronizationContext()
    {
        var synchronizationContext = new GameSynchronizationContext();

        await using var gameLoop =
            new GameLoop(new RealGameClock(), synchronizationContext);

        gameLoop.Start();

        await gameLoop.Started.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(synchronizationContext.IsCompleted);

        gameLoop.RequestStop();

        await gameLoop.Completion.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(synchronizationContext.IsCompleted);
        Assert.False(gameLoop.IsRunning);
    }

    [Fact]
    public async Task Stop_CompletesSynchronizationContext()
    {
        var synchronizationContext = new GameSynchronizationContext();

        await using var gameLoop = new GameLoop(new RealGameClock(), synchronizationContext);

        gameLoop.Start();
        await gameLoop.Started.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(synchronizationContext.IsCompleted);

        gameLoop.RequestStop();
        await gameLoop.Completion.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(synchronizationContext.IsCompleted);
        Assert.False(gameLoop.IsRunning);
    }

    [Fact]
    public async Task InfrastructureFailure_StillCompletesSynchronizationContext()
    {
        // Bind the context to another thread first, so the game thread cannot bind it and fails on startup.
        var synchronizationContext = new GameSynchronizationContext();
        var binder = new Thread(synchronizationContext.BindToCurrentThread);
        binder.Start();
        binder.Join();

        await using var gameLoop = new GameLoop(new RealGameClock(), synchronizationContext);

        gameLoop.Start();

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await gameLoop.Completion.WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.True(synchronizationContext.IsCompleted);
        Assert.False(gameLoop.IsRunning);
    }

    [Fact]
    public async Task NewLoop_IsNotRunningAndLifecycleTasksAreIncomplete()
    {
        await using var gameLoop = CreateGameLoop();

        Assert.False(gameLoop.IsRunning);
        Assert.False(gameLoop.Started.IsCompleted);
        Assert.False(gameLoop.Completion.IsCompleted);
    }

    [Fact]
    public async Task Start_InitializesDedicatedGameThread()
    {
        await using var gameLoop = CreateGameLoop();

        var callingThreadId =
            Environment.CurrentManagedThreadId;

        gameLoop.Start();

        await gameLoop.Started.WaitAsync(
            TimeSpan.FromSeconds(5));

        var gameThreadId =
            await GetGameThreadIdAsync(gameLoop);

        Assert.True(gameLoop.IsRunning);

        Assert.NotEqual(
            callingThreadId,
            gameThreadId);

        gameLoop.RequestStop();

        await gameLoop.Completion.WaitAsync(
            TimeSpan.FromSeconds(5));

        Assert.False(gameLoop.IsRunning);
    }

    [Fact]
    public async Task Start_CannotBeCalledTwice()
    {
        await using var gameLoop = CreateGameLoop();

        gameLoop.Start();

        await gameLoop.Started.WaitAsync(
            TimeSpan.FromSeconds(5));

        var exception =
            Assert.Throws<InvalidOperationException>(
                gameLoop.Start);

        Assert.Contains(
            "only be started once",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);

        gameLoop.RequestStop();

        await gameLoop.Completion.WaitAsync(
            TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RequestStop_BeforeStartStopsWithoutCreatingGameThread()
    {
        await using var gameLoop = CreateGameLoop();

        gameLoop.RequestStop();

        Assert.False(gameLoop.IsRunning);

        await gameLoop.Completion.WaitAsync(
            TimeSpan.FromSeconds(5));

        Assert.True(
            gameLoop.Completion.IsCompletedSuccessfully);

        Assert.True(
            gameLoop.Started.IsCanceled);
    }

    [Fact]
    public async Task RequestStop_BeforeStartPreventsLaterStart()
    {
        await using var gameLoop = CreateGameLoop();

        gameLoop.RequestStop();

        Assert.Throws<InvalidOperationException>(
            gameLoop.Start);
    }

    [Fact]
    public async Task Post_BeforeStartThrows()
    {
        await using var gameLoop = CreateGameLoop();

        var exception =
            Assert.Throws<InvalidOperationException>(
                () => gameLoop.Post(
                    _ => { }));

        Assert.Contains(
            "only be posted",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_ExecutesCallbackOnGameThread()
    {
        await using var gameLoop = CreateGameLoop();

        gameLoop.Start();

        await gameLoop.Started.WaitAsync(
            TimeSpan.FromSeconds(5));

        var gameThreadId =
            await GetGameThreadIdAsync(gameLoop);

        var callbackThreadSource =
            new TaskCompletionSource<int>(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        gameLoop.Post(
            _ =>
            {
                callbackThreadSource.TrySetResult(
                    Environment.CurrentManagedThreadId);
            });

        var callbackThreadId =
            await callbackThreadSource.Task.WaitAsync(
                TimeSpan.FromSeconds(5));

        Assert.Equal(
            gameThreadId,
            callbackThreadId);

        gameLoop.RequestStop();

        await gameLoop.Completion.WaitAsync(
            TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Post_MultipleCallbacksUseSameGameThread()
    {
        await using var gameLoop = CreateGameLoop();

        gameLoop.Start();

        await gameLoop.Started.WaitAsync(
            TimeSpan.FromSeconds(5));

        var callbackSources =
            Enumerable.Range(0, 3)
                .Select(
                    _ => new TaskCompletionSource<int>(
                        TaskCreationOptions
                            .RunContinuationsAsynchronously))
                .ToArray();

        foreach (var callbackSource in callbackSources)
        {
            gameLoop.Post(
                _ =>
                {
                    callbackSource.TrySetResult(
                        Environment.CurrentManagedThreadId);
                });
        }

        var callbackThreadIds =
            await Task.WhenAll(
                    callbackSources.Select(
                        source => source.Task))
                .WaitAsync(
                    TimeSpan.FromSeconds(5));

        Assert.Single(
            callbackThreadIds.Distinct());

        gameLoop.RequestStop();

        await gameLoop.Completion.WaitAsync(
            TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task InvokeAsync_ExecutesCallbackOnGameThread()
    {
        await using var gameLoop = CreateGameLoop();

        gameLoop.Start();

        await gameLoop.Started.WaitAsync(TimeSpan.FromSeconds(5));

        var gameThreadId = await GetGameThreadIdAsync(gameLoop);

        var callbackThreadId = 0;

        await gameLoop.InvokeAsync(
            () =>
            {
                callbackThreadId = Environment.CurrentManagedThreadId;

                return Task.CompletedTask;
            }).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(gameThreadId, callbackThreadId);

        gameLoop.RequestStop();

        await gameLoop.Completion.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task InvokeAsync_DoesNotCompleteUntilAsyncCallbackCompletes()
    {
        await using var gameLoop = CreateGameLoop();

        gameLoop.Start();

        await gameLoop.Started.WaitAsync(TimeSpan.FromSeconds(5));

        var callbackStartedSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCallbackSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var invocation =
            gameLoop.InvokeAsync(
                async () =>
                {
                    callbackStartedSource.TrySetResult();

                    await releaseCallbackSource.Task;
                });

        await callbackStartedSource.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(invocation.IsCompleted);

        releaseCallbackSource.TrySetResult();

        await invocation.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(invocation.IsCompletedSuccessfully);

        gameLoop.RequestStop();

        await gameLoop.Completion.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task InvokeAsync_PropagatesCallbackException()
    {
        await using var gameLoop = CreateGameLoop();

        gameLoop.Start();

        await gameLoop.Started.WaitAsync(TimeSpan.FromSeconds(5));

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () =>
                    await gameLoop.InvokeAsync(
                        () =>
                            Task.FromException(
                                new InvalidOperationException(
                                    "Expected invocation failure."))));

        Assert.Equal("Expected invocation failure.", exception.Message);
        Assert.True(gameLoop.IsRunning);

        gameLoop.RequestStop();

        await gameLoop.Completion.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task InvokeAsync_GenericReturnsResult()
    {
        await using var gameLoop = CreateGameLoop();

        gameLoop.Start();

        await gameLoop.Started.WaitAsync(TimeSpan.FromSeconds(5));

        var result =
            await gameLoop.InvokeAsync(
                () => Task.FromResult(42))
                .WaitAsync(
                    TimeSpan.FromSeconds(5));

        Assert.Equal(42, result);

        gameLoop.RequestStop();

        await gameLoop.Completion.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task InvokeAsync_AfterStopThrows()
    {
        await using var gameLoop = CreateGameLoop();

        gameLoop.Start();

        await gameLoop.Started.WaitAsync(
            TimeSpan.FromSeconds(5));

        gameLoop.RequestStop();

        await gameLoop.Completion.WaitAsync(
            TimeSpan.FromSeconds(5));

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => gameLoop.InvokeAsync(() => Task.CompletedTask));

        Assert.Contains(
            "only be posted",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvokeAsync_CanceledCallbackReturnsCanceledTask()
    {
        await using var gameLoop = CreateGameLoop();

        gameLoop.Start();

        await gameLoop.Started.WaitAsync(TimeSpan.FromSeconds(5));

        using var cancellationSource = new CancellationTokenSource();

        var callbackStartedSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var invocation =
            gameLoop.InvokeAsync(
                async () =>
                {
                    callbackStartedSource.TrySetResult();

                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationSource.Token);
                });

        await callbackStartedSource.Task.WaitAsync(TimeSpan.FromSeconds(5));

        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await invocation.WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.True(invocation.IsCanceled);
        Assert.True(gameLoop.IsRunning);

        gameLoop.RequestStop();

        await gameLoop.Completion.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task PostedCallbacksExecuteInPostingOrder()
    {
        await using var gameLoop = CreateGameLoop();

        gameLoop.Start();

        await gameLoop.Started.WaitAsync(
            TimeSpan.FromSeconds(5));

        var executionOrder =
            new List<string>();

        var completedSource =
            new TaskCompletionSource(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        gameLoop.Post(
            _ => executionOrder.Add("first"));

        gameLoop.Post(
            _ => executionOrder.Add("second"));

        gameLoop.Post(
            _ =>
            {
                executionOrder.Add("third");
                completedSource.TrySetResult();
            });

        await completedSource.Task.WaitAsync(
            TimeSpan.FromSeconds(5));

        Assert.Equal(
            ["first", "second", "third"],
            executionOrder);

        gameLoop.RequestStop();

        await gameLoop.Completion.WaitAsync(
            TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ScheduledContinuationResumesOnGameThread()
    {
        await using var gameLoop = CreateGameLoop();

        gameLoop.Start();

        await gameLoop.Started.WaitAsync(
            TimeSpan.FromSeconds(5));

        var observationSource =
            new TaskCompletionSource<ThreadObservation>(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        gameLoop.Post(
            _ =>
            {
                _ = ObserveScheduledContinuationAsync(
                    gameLoop,
                    observationSource);
            });

        var observation =
            await observationSource.Task.WaitAsync(
                TimeSpan.FromSeconds(5));

        Assert.Equal(
            observation.ThreadBeforeDelay,
            observation.ThreadAfterDelay);

        Assert.True(
            observation.WasGameThreadBeforeDelay);

        Assert.True(
            observation.WasGameThreadAfterDelay);

        gameLoop.RequestStop();

        await gameLoop.Completion.WaitAsync(
            TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ConcurrentScheduledOperationsResumeOnGameThread()
    {
        await using var gameLoop = CreateGameLoop();

        gameLoop.Start();

        await gameLoop.Started.WaitAsync(
            TimeSpan.FromSeconds(5));

        var completionSource =
            new TaskCompletionSource<
                IReadOnlyList<OperationObservation>>(
                    TaskCreationOptions
                        .RunContinuationsAsynchronously);

        gameLoop.Post(
            _ =>
            {
                _ = ObserveConcurrentOperationsAsync(
                    gameLoop,
                    completionSource);
            });

        var observations =
            await completionSource.Task.WaitAsync(
                TimeSpan.FromSeconds(5));

        Assert.Equal(2, observations.Count);

        Assert.Equal(
            "short",
            observations[0].Name);

        Assert.Equal(
            "long",
            observations[1].Name);

        Assert.All(
            observations,
            observation =>
            {
                Assert.Equal(
                    observation.ThreadBeforeDelay,
                    observation.ThreadAfterDelay);

                Assert.True(
                    observation.WasGameThreadAfterDelay);
            });

        gameLoop.RequestStop();

        await gameLoop.Completion.WaitAsync(
            TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RequestStop_CompletesLoopCleanly()
    {
        await using var gameLoop = CreateGameLoop();

        gameLoop.Start();

        await gameLoop.Started.WaitAsync(
            TimeSpan.FromSeconds(5));

        Assert.True(gameLoop.IsRunning);

        gameLoop.RequestStop();

        await gameLoop.Completion.WaitAsync(
            TimeSpan.FromSeconds(5));

        Assert.True(
            gameLoop.Completion.IsCompletedSuccessfully);

        Assert.False(gameLoop.IsRunning);
    }

    [Fact]
    public async Task RequestStop_CanBeCalledMoreThanOnce()
    {
        await using var gameLoop = CreateGameLoop();

        gameLoop.Start();

        await gameLoop.Started.WaitAsync(
            TimeSpan.FromSeconds(5));

        gameLoop.RequestStop();
        gameLoop.RequestStop();
        gameLoop.RequestStop();

        await gameLoop.Completion.WaitAsync(
            TimeSpan.FromSeconds(5));

        Assert.True(
            gameLoop.Completion.IsCompletedSuccessfully);

        Assert.False(gameLoop.IsRunning);
    }

    [Fact]
    public async Task Post_AfterStopIsRequestedThrows()
    {
        await using var gameLoop = CreateGameLoop();

        gameLoop.Start();

        await gameLoop.Started.WaitAsync(
            TimeSpan.FromSeconds(5));

        gameLoop.RequestStop();

        var exception =
            Assert.Throws<InvalidOperationException>(
                () => gameLoop.Post(
                    _ => { }));

        Assert.Contains(
            "only be posted",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);

        await gameLoop.Completion.WaitAsync(
            TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Post_AfterLoopHasStoppedThrows()
    {
        await using var gameLoop = CreateGameLoop();

        gameLoop.Start();

        await gameLoop.Started.WaitAsync(
            TimeSpan.FromSeconds(5));

        gameLoop.RequestStop();

        await gameLoop.Completion.WaitAsync(
            TimeSpan.FromSeconds(5));

        Assert.False(gameLoop.IsRunning);

        Assert.Throws<InvalidOperationException>(
            () => gameLoop.Post(
                _ => { }));
    }

    [Fact]
    public async Task CallbackFailure_WithoutSubscriber_KeepsTheLoopRunning()
    {
        await using var gameLoop = CreateGameLoop();

        gameLoop.Start();
        await gameLoop.Started.WaitAsync(TimeSpan.FromSeconds(5));

        gameLoop.Post(_ => throw new InvalidOperationException("Expected game-loop failure."));

        // Work posted afterwards still runs, so the loop survived.
        var result = await gameLoop
            .InvokeAsync(() => Task.FromResult(42))
            .WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(42, result);
        Assert.True(gameLoop.IsRunning);
        Assert.False(gameLoop.Completion.IsCompleted);
    }

    [Fact]
    public async Task CallbackFailure_IsReportedToUnhandledException_AndTheLoopKeepsRunning()
    {
        await using var gameLoop = CreateGameLoop();

        var reported = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        gameLoop.UnhandledException += exception => reported.TrySetResult(exception);

        gameLoop.Start();
        await gameLoop.Started.WaitAsync(TimeSpan.FromSeconds(5));

        gameLoop.Post(_ => throw new InvalidOperationException("Expected game-loop failure."));

        var exception = await reported.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Reported as the original exception, not wrapped in an AggregateException.
        var callbackException = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal("Expected game-loop failure.", callbackException.Message);
        Assert.True(gameLoop.IsRunning);
    }

    [Fact]
    public async Task LoopInfrastructureFailure_FaultsStartedAndCompletion()
    {
        // A completed context cannot be bound, so the game thread fails on startup.
        var context = new GameSynchronizationContext();
        context.Complete();

        await using var gameLoop = new GameLoop(new RealGameClock(), context); // use whatever CreateGameLoop uses for the clock

        gameLoop.Start();

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await gameLoop.Started.WaitAsync(TimeSpan.FromSeconds(5)));

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await gameLoop.Completion.WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.False(gameLoop.IsRunning);
    }

    private static GameLoop CreateGameLoop()
    {
        return new GameLoop(
            new RealGameClock(),
            new GameSynchronizationContext());
    }

    private static async Task<int> GetGameThreadIdAsync(
        GameLoop gameLoop)
    {
        var threadIdSource =
            new TaskCompletionSource<int>(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        gameLoop.Post(
            _ =>
            {
                threadIdSource.TrySetResult(
                    Environment.CurrentManagedThreadId);
            });

        return await threadIdSource.Task.WaitAsync(
            TimeSpan.FromSeconds(5));
    }

    private static async Task ObserveScheduledContinuationAsync(
        GameLoop gameLoop,
        TaskCompletionSource<ThreadObservation>
            observationSource)
    {
        try
        {
            var threadBeforeDelay =
                Environment.CurrentManagedThreadId;

            var wasGameThreadBeforeDelay =
                gameLoop.IsGameThread;

            await gameLoop.Scheduler.DelayAsync(
                TimeSpan.FromMilliseconds(25));

            observationSource.TrySetResult(
                new ThreadObservation(
                    ThreadBeforeDelay:
                        threadBeforeDelay,
                    ThreadAfterDelay:
                        Environment.CurrentManagedThreadId,
                    WasGameThreadBeforeDelay:
                        wasGameThreadBeforeDelay,
                    WasGameThreadAfterDelay:
                        gameLoop.IsGameThread));
        }
        catch (Exception exception)
        {
            observationSource.TrySetException(
                exception);
        }
    }

    private static async Task ObserveConcurrentOperationsAsync(
        GameLoop gameLoop,
        TaskCompletionSource<
            IReadOnlyList<OperationObservation>>
            completionSource)
    {
        try
        {
            var completionOrder =
                new List<OperationObservation>();

            var longOperation =
                ObserveOperationAsync(
                    gameLoop,
                    "long",
                    TimeSpan.FromMilliseconds(40),
                    completionOrder);

            var shortOperation =
                ObserveOperationAsync(
                    gameLoop,
                    "short",
                    TimeSpan.FromMilliseconds(15),
                    completionOrder);

            await Task.WhenAll(
                longOperation,
                shortOperation);

            completionSource.TrySetResult(
                completionOrder);
        }
        catch (Exception exception)
        {
            completionSource.TrySetException(
                exception);
        }
    }

    private static async Task ObserveOperationAsync(
        GameLoop gameLoop,
        string name,
        TimeSpan duration,
        ICollection<OperationObservation> completionOrder)
    {
        var threadBeforeDelay = Environment.CurrentManagedThreadId;

        await gameLoop.Scheduler.DelayAsync(duration);

        completionOrder.Add(
            new OperationObservation(
                Name: name,
                ThreadBeforeDelay:
                    threadBeforeDelay,
                ThreadAfterDelay:
                    Environment.CurrentManagedThreadId,
                WasGameThreadAfterDelay:
                    gameLoop.IsGameThread));
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