using NSubstitute;
using NullByte.Core.Scripting;

namespace NullByte.Scripting.Tests.Infrastructure;

/// <summary>
/// A stand-in for the player API that records what a script prints and lets the test decide when a
/// <c>nb.Sleep</c> finishes. Everything else on the API is an NSubstitute default.
/// </summary>
internal sealed class ScriptApiProbe
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly TaskCompletionSource sleepRequested = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource sleepCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ScriptApiProbe()
    {
        Api = Substitute.For<INullByteApi>();

        Api.When(api => api.Print(Arg.Any<object?>()))
            .Do(call => Output.Add(call.Arg<object?>()?.ToString() ?? string.Empty));

        Api.Sleep(Arg.Any<TimeSpan>()).Returns(call =>
        {
            LastSleepDuration = call.Arg<TimeSpan>();
            sleepRequested.TrySetResult();

            return sleepCompletion.Task;
        });
    }

    public INullByteApi Api { get; }

    public List<string> Output { get; } = [];

    public TimeSpan? LastSleepDuration { get; private set; }

    /// <summary>Completes when a script has called <c>nb.Sleep</c> (and is therefore waiting).</summary>
    public Task WaitUntilSleepRequestedAsync() => sleepRequested.Task.WaitAsync(Timeout);

    public void CompleteSleep() => sleepCompletion.TrySetResult();

    public void CancelSleep() => sleepCompletion.TrySetCanceled();
}
