using NullByte.Core.Domain;
using NullByte.Core.Persistence;
using NullByte.Core.Scripting;
using NullByte.Shell.Persistence;
using NullByte.Shell.Tests.Infrastructure;

namespace NullByte.Shell.Tests.Persistence;

public sealed class SessionSaveLoadTests
{
    private const string LoopScript = "while (true) { await nb.Sleep(TimeSpan.FromSeconds(1)); }";

    private static readonly SaveGameSerializer Serializer = new();

    /// <summary>Plays a little: a script running with two threads on home, args and all.</summary>
    private static async Task<(GameSession Session, SaveGame Save, double UsedRam)> StartPlayedGameAsync(RecordingOutput output)
    {
        var session = GameSession.Create(new GameSessionOptions { Output = output, NarrativeMode = NarrativeProgressionMode.Freeplay });
        await session.StartAsync();

        var usedRam = await session.GameThread.InvokeAsync(async () =>
        {
            session.State.FileSystem.WriteFile("home", "loop.cx", LoopScript);
            var result = await session.Services.Scripts.Start("home", "loop.cx", ["alpha", "two words"], threads: 2);
            Assert.True(result.Succeeded, result.FailureMessage);
            return session.State.Network.GetServer("home").UsedRam;
        });

        Assert.True(usedRam > 0);

        var save = await session.CaptureSaveAsync(DateTimeOffset.UtcNow);
        return (session, save, usedRam);
    }

    private static RestoredGame RoundTrip(SaveGame save)
        => SaveGameMapper.Restore(Serializer.Deserialize(Serializer.Serialize(save)));

    [Fact]
    public async Task A_running_script_comes_back_with_the_same_threads_arguments_and_ram()
    {
        var output = new RecordingOutput();
        var (first, save, usedRam) = await StartPlayedGameAsync(output);
        await using var keepAlive = first;

        Assert.Single(save.Processes);

        await using var second = GameSession.Create(new GameSessionOptions { Output = output }.WithSave(RoundTrip(save)));
        await second.StartAsync();

        var running = await second.GameThread.InvokeAsync(() =>
            Task.FromResult(second.Services.Scripts.ProcessesRunning("home")));

        var restarted = Assert.Single(running);
        Assert.Equal("loop.cx", restarted.Filename);
        Assert.Equal(2, restarted.Threads);
        Assert.Equal(new[] { "alpha", "two words" }, restarted.Args);
        Assert.Equal(ScriptProcessState.Running, restarted.State);

        var usedRamAfter = await second.GameThread.InvokeAsync(() =>
            Task.FromResult(second.State.Network.GetServer("home").UsedRam));
        Assert.Equal(usedRam, usedRamAfter);
    }

    [Fact]
    public async Task The_clock_continues_from_the_saved_game_time()
    {
        var output = new RecordingOutput();
        var (first, save, _) = await StartPlayedGameAsync(output);
        await using var keepAlive = first;

        var later = save with { GameTime = TimeSpan.FromHours(3) };

        await using var second = GameSession.Create(new GameSessionOptions { Output = output }.WithSave(RoundTrip(later)));
        await second.StartAsync();

        Assert.True(second.Clock.Elapsed >= TimeSpan.FromHours(3));
        Assert.True(second.Clock.Elapsed < TimeSpan.FromHours(3) + TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task A_script_that_cannot_restart_is_reported_and_the_game_still_loads()
    {
        var output = new RecordingOutput();
        var (first, save, _) = await StartPlayedGameAsync(output);
        await using var keepAlive = first;

        // The script file is gone from the save, so its process cannot start again.
        var withoutScript = save with { Files = save.Files.Where(file => file.Name != "loop.cx").ToArray() };

        await using var second = GameSession.Create(new GameSessionOptions { Output = output }.WithSave(RoundTrip(withoutScript)));
        await second.StartAsync();

        Assert.Contains(output.Lines, line => line.Contains("Could not restart loop.cx") && line.Contains("File not found"));

        var running = await second.GameThread.InvokeAsync(() =>
            Task.FromResult(second.Services.Scripts.ProcessesRunning("home")));
        Assert.Empty(running);
        Assert.Equal(0.0, second.State.Network.GetServer("home").UsedRam);
    }

    [Fact]
    public async Task Save_and_load_through_the_manager_round_trips_a_slot()
    {
        var output = new RecordingOutput();
        var (first, _, _) = await StartPlayedGameAsync(output);
        await using var keepAlive = first;

        var manager = new SaveManager(new InMemorySaveStore(), Serializer);
        await manager.SaveAsync(first, "slot1", TestContext.Current.CancellationToken);

        var restored = await manager.LoadAsync("slot1", TestContext.Current.CancellationToken);
        Assert.NotNull(restored);
        Assert.Equal(new[] { "loop.cx" }, restored!.State.FileSystem.ListFiles("home"));
        Assert.Null(await manager.LoadAsync("missing", TestContext.Current.CancellationToken));
    }
}
