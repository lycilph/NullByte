using NullByte.Core.Threading;

namespace NullByte.Core.Tests.Helpers;

/// <summary>
/// Runs a test body on a dedicated thread that is bound to a
/// <see cref="GameSynchronizationContext"/>, so awaits inside the body resume
/// through the context exactly as they do in the real game: queued by the
/// scheduler, executed when the test drains the context.
/// </summary>
internal static class GameThreadHarness
{
    public static void Run(Action<GameSynchronizationContext> testBody)
    {
        ArgumentNullException.ThrowIfNull(testBody);

        Exception? capturedException = null;

        var thread = new Thread(
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
            Name = "Game Thread Test Harness"
        };

        thread.Start();

        Assert.True(
            thread.Join(TimeSpan.FromSeconds(10)),
            "The test thread did not finish within ten seconds.");

        if (capturedException is not null)
            throw new AggregateException("The test thread failed.", capturedException);
    }
}