using NullByte.ConsoleUI.Startup;

namespace NullByte.ConsoleUI.Tests.Startup;

public sealed class BootSequenceTests
{
    [Fact]
    public async Task RunAsync_WritesBootMessagesInOrderAndFinishesReady()
    {
        using var output = new StringWriter();
        var delays = new List<TimeSpan>();
        var sequence = new BootSequence(
            output,
            (duration, _) =>
            {
                delays.Add(duration);
                return Task.CompletedTask;
            },
            new Random(42));

        await sequence.RunAsync(TestContext.Current.CancellationToken);

        var text = output.ToString();
        var memoryMapIndex = text.IndexOf("Memory map verified", StringComparison.Ordinal);
        var runtimeIndex = text.IndexOf("Runtime environment linked", StringComparison.Ordinal);
        var terminalIndex = text.IndexOf("Terminal session prepared", StringComparison.Ordinal);
        var readyIndex = text.IndexOf("SYSTEM READY", StringComparison.Ordinal);
        var statusLines = text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.Contains("[ OK ]", StringComparison.Ordinal))
            .ToArray();
        var dotCounts = statusLines
            .Select(line => line.Count(character => character == '.'))
            .ToArray();

        Assert.Contains("NULLBYTE SYSTEMS", text);
        Assert.Equal(7, statusLines.Length);
        Assert.True(memoryMapIndex >= 0);
        Assert.True(runtimeIndex > memoryMapIndex);
        Assert.True(terminalIndex > runtimeIndex);
        Assert.True(readyIndex > terminalIndex);
        Assert.All(statusLines, line => Assert.Equal(
            statusLines[0].IndexOf("[ OK ]", StringComparison.Ordinal),
            line.IndexOf("[ OK ]", StringComparison.Ordinal)));
        Assert.All(dotCounts, count => Assert.True(count > 6));
        Assert.True(dotCounts.Distinct().Count() > 1);
        Assert.Equal(dotCounts.Sum()+1, delays.Count);

        var totalDelays = new List<double>();
        var delayIndex = 0;
        foreach (var dotCount in dotCounts)
        {
            var stepDelays = delays.Skip(delayIndex).Take(dotCount).ToArray();
            Assert.Equal(dotCount, stepDelays.Length);
            totalDelays.Add(stepDelays.Sum(delay => delay.TotalMilliseconds));
            delayIndex += dotCount;
        }

        Assert.Equal(7, totalDelays.Count);
        Assert.All(totalDelays, total => Assert.InRange(total, 500, 1_000));
        Assert.True(totalDelays.Distinct().Count() > 1);
        Assert.InRange(totalDelays.Sum(), 3_500, 7_000);
    }

    [Fact]
    public async Task RunAsync_WhenCancelled_StopsBeforeReady()
    {
        using var output = new StringWriter();
        using var cancellation = new CancellationTokenSource();

        var sequence = new BootSequence(
            output,
            (_, token) =>
            {
                cancellation.Cancel();
                return Task.FromCanceled(token);
            });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sequence.RunAsync(cancellation.Token));

        Assert.DoesNotContain("SYSTEM READY", output.ToString());
    }

    [Fact]
    public async Task RunAsync_WhenWarmUpIsStillRunning_BlinksReadyingBeforeReady()
    {
        using var output = new StringWriter();
        var warmUp = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blinkDelays = 0;
        var sequence = new BootSequence(
            output,
            (duration, _) =>
            {
                if (duration == TimeSpan.FromMilliseconds(500) && ++blinkDelays == 2)
                    warmUp.SetResult();

                return Task.CompletedTask;
            },
            new Random(42));

        await sequence.RunAsync(TestContext.Current.CancellationToken, warmUp: _ => warmUp.Task);

        var text = output.ToString();
        var readyingIndex = text.IndexOf("SYSTEM READYING", StringComparison.Ordinal);
        var readyIndex = text.LastIndexOf("SYSTEM READY", StringComparison.Ordinal);

        Assert.True(readyingIndex >= 0);
        Assert.True(readyIndex > readyingIndex);
        Assert.Equal(2, blinkDelays);
        Assert.Contains("\r  SYSTEM READY     ", text);
    }

    [Fact]
    public async Task RunAsync_WhenWarmUpCompletesDuringBlink_DoesNotWaitForBlinkInterval()
    {
        using var output = new StringWriter();
        var warmUp = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blinkDelay = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sequence = new BootSequence(
            output,
            (duration, _) => duration == TimeSpan.FromMilliseconds(500)
                ? blinkDelay.Task
                : Task.CompletedTask,
            new Random(42));

        var run = sequence.RunAsync(TestContext.Current.CancellationToken, warmUp: _ => warmUp.Task);

        // The boot sequence reaches its readying status and begins a blink delay.
        while (!output.ToString().Contains("SYSTEM READYING", StringComparison.Ordinal))
            await Task.Yield();

        warmUp.SetResult();
        await run.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.Contains("SYSTEM READY", output.ToString());
        Assert.False(blinkDelay.Task.IsCompleted);
    }

    [Fact]
    public async Task RunAsync_WhenWarmUpIsAlreadyComplete_DoesNotShowReadying()
    {
        using var output = new StringWriter();
        var sequence = new BootSequence(output, (_, _) => Task.CompletedTask, new Random(42));

        await sequence.RunAsync(TestContext.Current.CancellationToken, warmUp: _ => Task.CompletedTask);

        Assert.DoesNotContain("SYSTEM READYING", output.ToString());
        Assert.Contains("SYSTEM READY", output.ToString());
    }
}
