using NullByte.Core.Tests.Infrastructure;

namespace NullByte.Core.Tests.Simulation;

/// <summary>
/// Covers what scripting relies on: a failing script must not stop the game,
/// and a misbehaving script must not starve it.
/// </summary>
public sealed class GameLoopResilienceTests
{
    [Fact]
    public async Task CallbackFailure_WithHandler_ReportsTheOriginalExceptionAndKeepsRunning()
    {
        await using var host = new GameLoopHost();
        var loop = host.Loop;

        var reported = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        loop.UnhandledException += exception => reported.TrySetResult(exception);

        await host.StartAsync();

        loop.Post(_ => throw new InvalidOperationException("Script crashed."));

        var exception = await GameLoopHost.WithinTimeout(reported.Task);

        // Reported as the original exception, not wrapped in an AggregateException.
        Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal("Script crashed.", exception.Message);
        Assert.True(loop.IsRunning);
        Assert.False(loop.Completion.IsCompleted);

        var result = await GameLoopHost.WithinTimeout(loop.InvokeAsync(() => Task.FromResult(42)));

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task CallbackFailure_WithHandler_ReportsEveryFailure()
    {
        await using var host = new GameLoopHost();
        var loop = host.Loop;

        var messages = new List<string>();
        var allReported = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        loop.UnhandledException += exception =>
        {
            // Raised on the game thread, so no locking is needed here.
            messages.Add(exception.Message);

            if (messages.Count == 2)
                allReported.TrySetResult();
        };

        await host.StartAsync();

        loop.Post(_ => throw new InvalidOperationException("first"));
        loop.Post(_ => throw new InvalidOperationException("second"));

        await GameLoopHost.WithinTimeout(allReported.Task);

        Assert.Equal(["first", "second"], messages);
        Assert.True(loop.IsRunning);
    }

    [Fact]
    public async Task CallbackFailure_WhenTheHandlerThrows_DoesNotFaultTheLoop()
    {
        await using var host = new GameLoopHost();
        var loop = host.Loop;

        loop.UnhandledException += _ => throw new InvalidOperationException("Bad handler.");

        await host.StartAsync();

        loop.Post(_ => throw new InvalidOperationException("Script crashed."));

        var result = await GameLoopHost.WithinTimeout(loop.InvokeAsync(() => Task.FromResult(7)));

        Assert.Equal(7, result);
        Assert.True(loop.IsRunning);
    }

    [Fact]
    public async Task AsyncVoidFailure_WithHandler_IsReportedAndTheLoopSurvives()
    {
        await using var host = new GameLoopHost();
        var loop = host.Loop;

        var reported = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        loop.UnhandledException += exception => reported.TrySetResult(exception);

        await host.StartAsync();

        // async void rethrows on the captured context, i.e. on the game thread.
        await GameLoopHost.WithinTimeout(loop.InvokeAsync(() =>
        {
            ThrowFromAsyncVoid();
            return Task.CompletedTask;
        }));

        var exception = await GameLoopHost.WithinTimeout(reported.Task);

        Assert.Equal("async void crash", exception.Message);
        Assert.True(loop.IsRunning);
    }

    [Fact]
    public async Task SelfPostingCallback_DoesNotStarveOtherWork()
    {
        await using var host = new GameLoopHost();
        var loop = host.Loop;
        var context = host.Context;

        var stop = false;
        var runs = 0;

        void Spin(object? _)
        {
            runs++;

            if (!Volatile.Read(ref stop))
                context.Post(Spin, state: null);
        }

        await host.StartAsync();

        loop.Post(Spin);

        try
        {
            // Would never return if the loop drained the spinning callback to exhaustion.
            var result = await GameLoopHost.WithinTimeout(loop.InvokeAsync(() => Task.FromResult(1)));

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
        await using var host = new GameLoopHost();
        var loop = host.Loop;
        var scheduler = host.Scheduler;

        await host.StartAsync();

        using var cancellation = new CancellationTokenSource();
        var iterations = 0;

        // A script that sleeps for zero time in a loop.
        _ = loop.InvokeAsync(async () =>
        {
            while (!cancellation.IsCancellationRequested)
            {
                await scheduler.DelayAsync(TimeSpan.Zero);
                Interlocked.Increment(ref iterations);
            }
        });

        try
        {
            var result = await GameLoopHost.WithinTimeout(loop.InvokeAsync(() => Task.FromResult(3)));

            Assert.Equal(3, result);
            Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref iterations) >= 3, GameLoopHost.Timeout));
        }
        finally
        {
            await cancellation.CancelAsync();
        }
    }

    private static async void ThrowFromAsyncVoid()
    {
        await Task.Yield();

        throw new InvalidOperationException("async void crash");
    }
}
