using NullByte.Core.Threading;

namespace NullByte.Core.Tests.Threading;

public sealed class GameSynchronizationContextTests
{
    [Fact]
    public void Complete_PreventsNewPosts()
    {
        RunOnDedicatedThread(
            context =>
            {
                context.Complete();

                Assert.True(context.IsCompleted);

                var exception =
                    Assert.Throws<InvalidOperationException>(
                        () => context.Post(
                            _ => { },
                            state: null));

                Assert.Contains(
                    "completed",
                    exception.Message,
                    StringComparison.OrdinalIgnoreCase);

                Assert.Equal(
                    0,
                    context.PendingCount);
            });
    }

    [Fact]
    public void Complete_CanBeCalledMultipleTimes()
    {
        RunOnDedicatedThread(
            context =>
            {
                context.Complete();
                context.Complete();
                context.Complete();

                Assert.True(context.IsCompleted);
                Assert.Equal(0, context.PendingCount);
            });
    }

    [Fact]
    public void Complete_DiscardsPendingPosts()
    {
        RunOnDedicatedThread(
            context =>
            {
                var callbackExecuted = false;

                context.Post(
                    _ => callbackExecuted = true,
                    state: null);

                Assert.Equal(
                    1,
                    context.PendingCount);

                context.Complete();

                Assert.True(context.IsCompleted);
                Assert.Equal(0, context.PendingCount);
                Assert.False(callbackExecuted);

                context.Drain();

                Assert.False(callbackExecuted);
            });
    }

    [Fact]
    public void BindToCurrentThread_BindsContextAndInstallsIt()
    {
        var previousContext = SynchronizationContext.Current;

        try
        {
            var context = new GameSynchronizationContext();

            context.BindToCurrentThread();

            Assert.True(context.IsBound);
            Assert.True(context.IsGameThread);

            Assert.Same(context, SynchronizationContext.Current);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }

    [Fact]
    public void BindToCurrentThread_CannotBeCalledTwice()
    {
        RunOnDedicatedThread(
            context =>
            {
                var exception =
                    Assert.Throws<InvalidOperationException>(
                        context.BindToCurrentThread);

                Assert.Contains(
                    "already bound",
                    exception.Message,
                    StringComparison.OrdinalIgnoreCase);
            });
    }

    [Fact]
    public void Post_DoesNotExecuteCallbackImmediately()
    {
        RunOnDedicatedThread(
            context =>
            {
                var wasExecuted = false;

                context.Post(
                    _ => wasExecuted = true,
                    state: null);

                Assert.False(wasExecuted);
                Assert.Equal(1, context.PendingCount);

                context.Drain();

                Assert.True(wasExecuted);
                Assert.Equal(0, context.PendingCount);
            });
    }

    [Fact]
    public void Post_FromWorkerThread_ExecutesOnGameThread()
    {
        var gameThreadId = 0;
        var workerThreadId = 0;
        var callbackThreadId = 0;

        RunOnDedicatedThread(
            context =>
            {
                gameThreadId = Environment.CurrentManagedThreadId;

                Task.Run(
                        () =>
                        {
                            workerThreadId = Environment.CurrentManagedThreadId;

                            context.Post(
                                _ => callbackThreadId = Environment.CurrentManagedThreadId,
                                state: null);
                        })
                    .GetAwaiter()
                    .GetResult();

                Assert.Equal(1, context.PendingCount);

                context.Drain();
            });

        Assert.NotEqual(gameThreadId, workerThreadId);
        Assert.Equal(gameThreadId, callbackThreadId);
    }

    [Fact]
    public void Drain_ExecutesCallbacksInPostingOrder()
    {
        RunOnDedicatedThread(
            context =>
            {
                var executionOrder = new List<string>();

                context.Post(_ => executionOrder.Add("first"), state: null);
                context.Post(_ => executionOrder.Add("second"), state: null);
                context.Post(_ => executionOrder.Add("third"), state: null);

                var processedCount = context.Drain();

                Assert.Equal(3, processedCount);
                Assert.Equal(["first", "second", "third"], executionOrder);
            });
    }

    [Fact]
    public void Drain_ProcessesCallbacksPostedDuringDrain()
    {
        RunOnDedicatedThread(
            context =>
            {
                var executionOrder =
                    new List<string>();

                context.Post(
                    _ =>
                    {
                        executionOrder.Add("outer");

                        context.Post(
                            _ => executionOrder.Add("inner"),
                            state: null);
                    },
                    state: null);

                var processedCount = context.Drain();

                Assert.Equal(2, processedCount);
                Assert.Equal(["outer", "inner"], executionOrder);
                Assert.Equal(0, context.PendingCount);
            });
    }

    [Fact]
    public void Drain_ContinuesAfterCallbackThrows()
    {
        RunOnDedicatedThread(
            context =>
            {
                var successfulCallbackExecuted = false;

                context.Post(
                    _ => throw new InvalidOperationException(
                        "Expected failure."),
                    state: null);

                context.Post(
                    _ => successfulCallbackExecuted = true,
                    state: null);

                var exception = Assert.Throws<AggregateException>(() => context.Drain());

                Assert.True(successfulCallbackExecuted);
                Assert.Single(exception.InnerExceptions);
                Assert.IsType<InvalidOperationException>(exception.InnerExceptions[0]);
                Assert.Equal("Expected failure.", exception.InnerExceptions[0].Message);
                Assert.Equal(0, context.PendingCount);
            });
    }

    [Fact]
    public void Drain_AggregatesMultipleCallbackExceptions()
    {
        RunOnDedicatedThread(
            context =>
            {
                context.Post(
                    _ => throw new InvalidOperationException(
                        "First failure."),
                    state: null);

                context.Post(
                    _ => throw new ArgumentException(
                        "Second failure."),
                    state: null);

                var exception = Assert.Throws<AggregateException>(() => context.Drain());

                Assert.Collection(
                    exception.InnerExceptions,
                    first =>
                    {
                        Assert.IsType<InvalidOperationException>(first);
                        Assert.Equal("First failure.", first.Message);
                    },
                    second =>
                    {
                        Assert.IsType<ArgumentException>(second);
                        Assert.Equal("Second failure.", second.Message);
                    });
            });
    }

    [Fact]
    public void Drain_FromNonGameThreadThrows()
    {
        var context = new GameSynchronizationContext();

        using var contextReady = new ManualResetEventSlim(false);
        using var releaseGameThread = new ManualResetEventSlim(false);

        Exception? gameThreadException = null;

        var gameThread =
            new Thread(
                () =>
                {
                    try
                    {
                        context.BindToCurrentThread();
                        contextReady.Set();

                        releaseGameThread.Wait();
                    }
                    catch (Exception exception)
                    {
                        gameThreadException = exception;
                    }
                    finally
                    {
                        SynchronizationContext.SetSynchronizationContext(null);
                    }
                })
            {
                IsBackground = true,
                Name = "Synchronization Context Test Thread"
            };

        gameThread.Start();

        Assert.True(
            contextReady.Wait(TimeSpan.FromSeconds(5)),
            "The game thread did not become ready.");

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() => context.Drain());

            Assert.Contains(
                "game thread",
                exception.Message,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            releaseGameThread.Set();

            Assert.True(gameThread.Join(TimeSpan.FromSeconds(5)), "The game thread did not stop.");
        }

        Assert.Null(gameThreadException);
    }

    [Fact]
    public void VerifyAccess_SucceedsOnGameThread()
    {
        RunOnDedicatedThread(
            context =>
            {
                context.VerifyAccess();
            });
    }

    [Fact]
    public void VerifyAccess_ThrowsOnAnotherThread()
    {
        var context = new GameSynchronizationContext();

        using var contextReady = new ManualResetEventSlim(false);
        using var releaseGameThread = new ManualResetEventSlim(false);

        var gameThread =
            new Thread(
                () =>
                {
                    context.BindToCurrentThread();
                    contextReady.Set();

                    releaseGameThread.Wait();

                    SynchronizationContext.SetSynchronizationContext(null);
                })
            {
                IsBackground = true
            };

        gameThread.Start();

        Assert.True(contextReady.Wait(TimeSpan.FromSeconds(5)));

        try
        {
            Assert.Throws<InvalidOperationException>(context.VerifyAccess);
        }
        finally
        {
            releaseGameThread.Set();

            Assert.True(gameThread.Join(TimeSpan.FromSeconds(5)));
        }
    }

    [Fact]
    public void DrainBatch_DoesNotRunCallbacksPostedDuringTheBatch()
    {
        RunOnDedicatedThread(
            context =>
            {
                var executionOrder = new List<string>();

                context.Post(
                    _ =>
                    {
                        executionOrder.Add("outer");

                        context.Post(
                            _ => executionOrder.Add("inner"),
                            state: null);
                    },
                    state: null);

                Assert.Equal(1, context.DrainBatch());
                Assert.Equal(["outer"], executionOrder);
                Assert.Equal(1, context.PendingCount);

                Assert.Equal(1, context.DrainBatch());
                Assert.Equal(["outer", "inner"], executionOrder);
            });
    }

    [Fact]
    public void DrainBatch_WithSelfPostingCallback_ReturnsEveryBatch()
    {
        RunOnDedicatedThread(
            context =>
            {
                var runs = 0;

                void Callback(object? _)
                {
                    runs++;
                    context.Post(Callback, state: null);
                }

                context.Post(Callback, state: null);

                for (var batch = 1; batch <= 5; batch++)
                {
                    Assert.Equal(1, context.DrainBatch());
                    Assert.Equal(batch, runs);
                }
            });
    }

    [Fact]
    public void DrainBatch_WithNothingQueued_ReturnsZero()
    {
        RunOnDedicatedThread(context => Assert.Equal(0, context.DrainBatch()));
    }

    [Fact]
    public void DrainBatch_ContinuesAfterCallbackThrows()
    {
        RunOnDedicatedThread(
            context =>
            {
                var successfulCallbackExecuted = false;

                context.Post(
                    _ => throw new InvalidOperationException("Expected failure."),
                    state: null);

                context.Post(
                    _ => successfulCallbackExecuted = true,
                    state: null);

                var exception = Assert.Throws<AggregateException>(() => context.DrainBatch());

                Assert.True(successfulCallbackExecuted);
                Assert.Single(exception.InnerExceptions);
            });
    }

    [Fact]
    public void CreateCopy_ReturnsSameContext()
    {
        var context = new GameSynchronizationContext();
        var copy = context.CreateCopy();

        Assert.Same(context, copy);
    }

    private static void RunOnDedicatedThread(Action<GameSynchronizationContext> testBody)
    {
        ArgumentNullException.ThrowIfNull(testBody);

        Exception? capturedException = null;

        var thread =
            new Thread(
                () =>
                {
                    var previousContext = SynchronizationContext.Current;

                    try
                    {
                        var context = new GameSynchronizationContext();

                        context.BindToCurrentThread();

                        testBody(context);
                    }
                    catch (Exception exception)
                    {
                        capturedException = exception;
                    }
                    finally
                    {
                        SynchronizationContext.SetSynchronizationContext(previousContext);
                    }
                })
            {
                IsBackground = true,
                Name = "Synchronization Context Test Thread"
            };

        thread.Start();

        Assert.True(
            thread.Join(TimeSpan.FromSeconds(10)),
            "The synchronization-context test thread " +
            "did not finish within ten seconds.");

        if (capturedException is not null)
        {
            throw new AggregateException(
                "The dedicated test thread failed.",
                capturedException);
        }
    }
}