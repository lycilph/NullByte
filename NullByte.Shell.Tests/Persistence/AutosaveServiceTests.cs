using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NullByte.Core.Persistence;
using NullByte.Shell.Persistence;
using NullByte.Shell.Tests.Infrastructure;

namespace NullByte.Shell.Tests.Persistence;

/// <summary>
/// Autosave on a fake clock: a tick happens exactly when the test moves time forward, so nothing here sleeps.
/// </summary>
public sealed class AutosaveServiceTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Autosave_writes_the_autosave_slot_when_the_interval_has_passed()
    {
        var output = new RecordingOutput();
        var store = Substitute.For<ISaveStore>();
        var time = new FakeTimeProvider();
        await using var session = await StartSessionAsync(output);

        await using (new AutosaveService(new SaveManager(store, new SaveGameSerializer()), session, output, Interval, time))
        {
            time.Advance(Interval);
            await output.WaitForLineAsync("Game autosaved.", Timeout);
        }

        await store.Received(1).WriteAsync(AutosaveService.Slot, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Autosave_does_not_write_before_the_interval_has_passed()
    {
        var output = new RecordingOutput();
        var store = Substitute.For<ISaveStore>();
        var time = new FakeTimeProvider();
        await using var session = await StartSessionAsync(output);

        await using (new AutosaveService(new SaveManager(store, new SaveGameSerializer()), session, output, Interval, time))
        {
            time.Advance(Interval - TimeSpan.FromSeconds(1));
        }

        await store.DidNotReceive().WriteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        Assert.DoesNotContain("Game autosaved.", output.Lines);
    }

    [Fact]
    public async Task A_failing_store_is_reported_and_autosave_keeps_running()
    {
        var output = new RecordingOutput();
        var store = Substitute.For<ISaveStore>();
        store
            .WriteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new IOException("disk full")));
        var time = new FakeTimeProvider();
        await using var session = await StartSessionAsync(output);

        await using (new AutosaveService(new SaveManager(store, new SaveGameSerializer()), session, output, Interval, time))
        {
            time.Advance(Interval);
            await output.WaitForLineAsync("Autosave failed: disk full", Timeout);
        }

        Assert.DoesNotContain("Game autosaved.", output.Lines);
    }

    private static async Task<GameSession> StartSessionAsync(RecordingOutput output)
    {
        var session = GameSession.Create(new GameSessionOptions { Output = output });
        await session.StartAsync();
        return session;
    }
}
