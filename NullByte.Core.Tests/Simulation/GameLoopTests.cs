using NullByte.Core.Simulation;
using NullByte.Core.Threading;
using NullByte.Core.Time;

namespace NullByte.Core.Tests.Simulation;

public sealed class GameLoopTests
{
    [Fact]
    public async Task Start_InitializesDedicatedGameThread()
    {
        await using var gameLoop = CreateGameLoop();

        var callingThreadId = Environment.CurrentManagedThreadId;

        gameLoop.Start();

        await gameLoop.Started.WaitAsync(TimeSpan.FromSeconds(5));

        var gameThreadId = await GetGameThreadIdAsync(gameLoop);

        Assert.True(gameLoop.HasStarted);

        Assert.NotEqual(callingThreadId, gameThreadId);

        Assert.True(gameLoop.Clock.Elapsed >= TimeSpan.Zero);

        gameLoop.RequestStop();

        await gameLoop.Completion.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Start_CannotBeCalledTwice()
    {
        await using var gameLoop = CreateGameLoop();

        gameLoop.Start();

        await gameLoop.Started.WaitAsync(TimeSpan.FromSeconds(5));

        var exception = Assert.Throws<InvalidOperationException>(gameLoop.Start);

        Assert.Contains(
            "only be started once",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);

        gameLoop.RequestStop();

        await gameLoop.Completion.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Post_BeforeStartThrows()
    {
        await using var gameLoop = CreateGameLoop();

        var exception =
            Assert.Throws<InvalidOperationException>(
                () => gameLoop.Post(_ => { }, state: null));

        Assert.Contains(
            "not been started",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_ExecutesCallbackOnGameThread()
    {
        await using var gameLoop = CreateGameLoop();

        gameLoop.Start();

        await gameLoop.Started.WaitAsync(TimeSpan.FromSeconds(5));

        var firstGameThreadId = await GetGameThreadIdAsync(gameLoop);

        var callbackThreadSource =
            new TaskCompletionSource<int>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        gameLoop.Post(
            _ => callbackThreadSource.TrySetResult(Environment.CurrentManagedThreadId));

        var callbackThreadId = await callbackThreadSource.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(firstGameThreadId, callbackThreadId);

        gameLoop.RequestStop();

        await gameLoop.Completion.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Post_MultipleCallbacksUseSameGameThread()
    {
        await using var gameLoop = CreateGameLoop();

        gameLoop.Start();

        await gameLoop.Started.WaitAsync(TimeSpan.FromSeconds(5));

        var callbackSources =
            Enumerable.Range(0, 3)
                .Select(_ => new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously))
                .ToArray();

        foreach (var callbackSource in callbackSources)
        {
            gameLoop.Post(_ => callbackSource.TrySetResult(Environment.CurrentManagedThreadId));
        }

        var callbackThreadIds =
            await Task.WhenAll(callbackSources.Select(source => source.Task))
                .WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Single(callbackThreadIds.Distinct());
        Assert.All(callbackThreadIds, threadId => Assert.True(threadId > 0));

        gameLoop.RequestStop();

        await gameLoop.Completion.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task PostedCallbacksExecuteInPostingOrder()
    {
        await using var gameLoop = CreateGameLoop();

        gameLoop.Start();

        await gameLoop.Started.WaitAsync(TimeSpan.FromSeconds(5));

        var executionOrder = new List<string>();

        var completedSource =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        gameLoop.Post(_ => executionOrder.Add("first"));
        gameLoop.Post(_ => executionOrder.Add("second"));
        gameLoop.Post(
            _ =>
            {
                executionOrder.Add("third");
                completedSource.TrySetResult();
            });

        await completedSource.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(["first", "second", "third"], executionOrder);

        gameLoop.RequestStop();

        await gameLoop.Completion.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ScheduledContinuationResumesOnGameThread()
    {
        await using var gameLoop = CreateGameLoop();

        gameLoop.Start();

        await gameLoop.Started.WaitAsync(TimeSpan.FromSeconds(5));

        var observationSource =
            new TaskCompletionSource<ThreadObservation>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        gameLoop.Post(
            _ =>
            {
                _ = ObserveScheduledContinuationAsync(gameLoop, observationSource);
            });

        var observation =
            await observationSource.Task.WaitAsync(
                TimeSpan.FromSeconds(5));

        Assert.Equal(
            observation.ThreadBeforeDelay,
            observation.ThreadAfterDelay);

        Assert.True(observation.WasGameThreadBeforeDelay);
        Assert.True(observation.WasGameThreadAfterDelay);

        gameLoop.RequestStop();

        await gameLoop.Completion.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ConcurrentScheduledOperationsResumeOnGameThread()
    {
        await using var gameLoop = CreateGameLoop();

        gameLoop.Start();

        await gameLoop.Started.WaitAsync(TimeSpan.FromSeconds(5));

        var completionSource =
            new TaskCompletionSource<
                IReadOnlyList<OperationObservation>>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

        gameLoop.Post(
            _ =>
            {
                _ = ObserveConcurrentOperationsAsync(gameLoop,completionSource);
            });

        var observations = await completionSource.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(2, observations.Count);
        Assert.Equal("short", observations[0].Name);
        Assert.Equal("long", observations[1].Name);

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

        await gameLoop.Completion.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RequestStop_CompletesLoopCleanly()
    {
        await using var gameLoop = CreateGameLoop();

        gameLoop.Start();

        await gameLoop.Started.WaitAsync(TimeSpan.FromSeconds(5));

        gameLoop.RequestStop();

        await gameLoop.Completion.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(gameLoop.Completion.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Post_AfterStopIsRequestedThrows()
    {
        await using var gameLoop = CreateGameLoop();

        gameLoop.Start();

        await gameLoop.Started.WaitAsync(TimeSpan.FromSeconds(5));

        gameLoop.RequestStop();

        var exception =
            Assert.Throws<InvalidOperationException>(
                () => gameLoop.Post(_ => { }));

        Assert.Contains(
            "asked to stop",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);

        await gameLoop.Completion.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CallbackFailure_FaultsLoopCompletion()
    {
        await using var gameLoop = CreateGameLoop();

        gameLoop.Start();

        await gameLoop.Started.WaitAsync(TimeSpan.FromSeconds(5));

        gameLoop.Post(
            _ => throw new InvalidOperationException(
                "Expected game-loop failure."));

        var exception =
            await Assert.ThrowsAsync<AggregateException>(
                async () =>
                    await gameLoop.Completion.WaitAsync(
                        TimeSpan.FromSeconds(5)));

        var callbackException =
            Assert.IsType<InvalidOperationException>(
                Assert.Single(
                    exception.InnerExceptions));

        Assert.Equal(
            "Expected game-loop failure.",
            callbackException.Message);
    }

    private static GameLoop CreateGameLoop() 
        => new(new RealGameClock(), new GameSynchronizationContext());

    private static async Task<int> GetGameThreadIdAsync(GameLoop gameLoop)
    {
        var threadIdSource =
            new TaskCompletionSource<int>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        gameLoop.Post(_ => threadIdSource.TrySetResult(Environment.CurrentManagedThreadId));

        return await threadIdSource.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static async Task ObserveScheduledContinuationAsync(
            GameLoop gameLoop,
            TaskCompletionSource<ThreadObservation> observationSource)
    {
        try
        {
            var threadBeforeDelay = Environment.CurrentManagedThreadId;
            var wasGameThreadBeforeDelay = gameLoop.IsGameThread;

            await gameLoop.Scheduler.DelayAsync(TimeSpan.FromMilliseconds(25));

            var observation = new ThreadObservation(
                ThreadBeforeDelay: threadBeforeDelay,
                ThreadAfterDelay: Environment.CurrentManagedThreadId,
                WasGameThreadBeforeDelay: wasGameThreadBeforeDelay,
                WasGameThreadAfterDelay: gameLoop.IsGameThread);

            observationSource.TrySetResult( observation);
        }
        catch (Exception exception)
        {
            observationSource.TrySetException(exception);
        }
    }

    private static async Task ObserveConcurrentOperationsAsync(
            GameLoop gameLoop,
            TaskCompletionSource<IReadOnlyList<OperationObservation>> completionSource)
    {
        try
        {
            var completionOrder = new List<OperationObservation>();

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

            await Task.WhenAll(longOperation, shortOperation);

            completionSource.TrySetResult( completionOrder);
        }
        catch (Exception exception)
        {
            completionSource.TrySetException(exception);
        }
    }

    private static async Task ObserveOperationAsync(
        GameLoop gameLoop,
        string name,
        TimeSpan duration,
        ICollection<OperationObservation> completionOrder)
    {
        var threadBeforeDelay = Environment.CurrentManagedThreadId;

        await gameLoop.Scheduler.DelayAsync( duration);

        completionOrder.Add(
            new OperationObservation(
                Name: name,
                ThreadBeforeDelay: threadBeforeDelay,
                ThreadAfterDelay: Environment.CurrentManagedThreadId,
                WasGameThreadAfterDelay: gameLoop.IsGameThread));
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