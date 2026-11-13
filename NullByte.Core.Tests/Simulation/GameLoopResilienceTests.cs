using NullByte.Core.Simulation;
using NullByte.Core.Threading;
using NullByte.Core.Time;

namespace NullByte.Core.Tests.Simulation;

/// <summary>
/// Covers what scripting relies on: a failing script must not stop the game,
/// and a misbehaving script must not starve it.
/// </summary>
public sealed class GameLoopResilienceTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task CallbackFailure_WithHandler_ReportsExceptionAndKeepsRunning()
    {
        await using var gameLoop = CreateGameLoop(out _, out _);

        var reported = new TaskCompletionSource<Exception>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        gameLoop.UnhandledException += exception => reported.TrySetResult(exception);

        gameLoop.Start();
        await gameLoop.Started.WaitAsync(Timeout);

        gameLoop.Post(_ => throw new InvalidOperationException("Script crashed."));

        var exception = await reported.Task.WaitAsync(Timeout);

        Assert.Equal("Script crashed.", exception.Message);
        Assert.True(gameLoop.IsRunning);
        Assert.False(gameLoop.Completion.IsCompleted);

        var result = await gameLoop.InvokeAsync(() => Task.FromResult(42)).WaitAsync(Timeout);

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task CallbackFailure_WithHandler_ReportsEveryFailure()
    {
        await using var gameLoop = CreateGameLoop(out _, out _);

        var messages = new List<string>();
        var allReported = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        gameLoop.UnhandledException += exception =>
        {
            // Raised on the game thread, so no locking is needed here.
            messages.Add(exception.Message);

            if (messages.Count == 2)
                allReported.TrySetResult();
        };

        gameLoop.Start();
        await gameLoop.Started.WaitAsync(Timeout);

        gameLoop.Post(_ => throw new InvalidOperationException("first"));
        gameLoop.Post(_ => throw new InvalidOperationException("second"));

        await allReported.Task.WaitAsync(Timeout);

        Assert.Equal(["first", "second"], messages);
        Assert.True(gameLoop.IsRunning);
    }

    [Fact]
    public async Task CallbackFailure_WhenHandlerThrows_DoesNotFaultLoop()
    {
        await using var gameLoop = CreateGameLoop(out _, out _);

        gameLoop.UnhandledException += _ => throw new InvalidOperationException("Bad handler.");

        gameLoop.Start();
        await gameLoop.Started.WaitAsync(Timeout);

        gameLoop.Post(_ => throw new InvalidOperationException("Script crashed."));

        var result = await gameLoop.InvokeAsync(() => Task.FromResult(7)).WaitAsync(Timeout);

        Assert.Equal(7, result);
        Assert.True(gameLoop.IsRunning);
    }

    [Fact]
    public async Task AsyncVoidFailure_WithHandler_IsReportedAndLoopSurvives()
    {
        await using var gameLoop = CreateGameLoop(out _, out _);

        var reported = new TaskCompletionSource<Exception>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        gameLoop.UnhandledException += exception => reported.TrySetResult(exception);

        gameLoop.Start();
        await gameLoop.Started.WaitAsync(Timeout);

        // async void rethrows on the captured context, i.e. on the game thread.
        await gameLoop.InvokeAsync(() =>
        {
            ThrowFromAsyncVoid();
            return Task.CompletedTask;
        }).WaitAsync(Timeout);

        var exception = await reported.Task.WaitAsync(Timeout);

        Assert.Equal("async void crash", exception.Message);
        Assert.True(gameLoop.IsRunning);
    }

    [Fact]
    public async Task SelfPostingCallback_DoesNotStarveOtherWork()
    {
        await using var gameLoop = CreateGameLoop(out var context, out _);

        var stop = false;
        var runs = 0;

        void Spin(object? _)
        {
            runs++;

            if (!Volatile.Read(ref stop))
                context.Post(Spin, state: null);
        }

        gameLoop.Start();
        await gameLoop.Started.WaitAsync(Timeout);

        gameLoop.Post(Spin);

        try
        {
            // Would never return if the loop drained the spinning callback to exhaustion.
            var result = await gameLoop.InvokeAsync(() => Task.FromResult(1)).WaitAsync(Timeout);

            Assert.Equal(1, result);
            Assert.True(Volatile.Read(ref runs) > 0);
        }
        finally
        {
            Volatile.Write(ref stop, true);
        }
    }

    [Fact]
    public async Task ZeroDelayLoop_DoesNotStarveOtherWork()
    {
        await using var gameLoop = CreateGameLoop(out _, out GameScheduler scheduler);

        gameLoop.Start();
        await gameLoop.Started.WaitAsync(Timeout);

        using var cancellation = new CancellationTokenSource();
        var iterations = 0;

        // A script that sleeps for zero time in a loop.
        _ = gameLoop.InvokeAsync(async () =>
        {
            while (!cancellation.IsCancellationRequested)
            {
                await scheduler.DelayAsync(TimeSpan.Zero);
                Interlocked.Increment(ref iterations);
            }
        });

        try
        {
            var result = await gameLoop.InvokeAsync(() => Task.FromResult(3)).WaitAsync(Timeout);

            Assert.Equal(3, result);
            Assert.True(
                SpinWait.SpinUntil(
                    () => Volatile.Read(ref iterations) >= 3,
                    Timeout));
        }
        finally
        {
            await cancellation.CancelAsync();
        }
    }

    private static GameLoop CreateGameLoop(out GameSynchronizationContext context, out GameScheduler scheduler)
    {
        var clock = new RealGameClock();
        context = new GameSynchronizationContext();
        scheduler = new GameScheduler(clock);

        return new GameLoop(clock, context, scheduler);
    }

    private static async void ThrowFromAsyncVoid()
    {
        await Task.Yield();

        throw new InvalidOperationException("async void crash");
    }
}