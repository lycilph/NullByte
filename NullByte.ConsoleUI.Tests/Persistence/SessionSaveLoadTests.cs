using NullByte.Core.Persistence;
using NullByte.Core.Scripting;
using NullByte.Core.Services;
using NullByte.Shell;
using NullByte.Shell.Persistence;

namespace NullByte.ConsoleUI.Tests.Persistence;

public class SessionSaveLoadTests
{
    private const string LoopScript = "while (true) { await nb.Sleep(TimeSpan.FromSeconds(1)); }";

    private static readonly SaveGameSerializer Serializer = new();

    private sealed class TestOutput : IGameOutput
    {
        private readonly List<string> lines = [];

        public IReadOnlyList<string> Lines { get { lock (lines) return lines.ToArray(); } }

        public void WriteLine(string message) { lock (lines) lines.Add(message); }
        public void WriteLine() => WriteLine("");
    }

    private sealed class MemoryStore : ISaveStore
    {
        private readonly Dictionary<string, string> slots = [];

        public Task WriteAsync(string slot, string json, CancellationToken cancellationToken = default)
        {
            lock (slots) slots[slot] = json;
            return Task.CompletedTask;
        }

        public Task<string?> ReadAsync(string slot, CancellationToken cancellationToken = default)
        {
            lock (slots) return Task.FromResult(slots.GetValueOrDefault(slot));
        }

        public Task<IReadOnlyList<SaveSlotInfo>> ListAsync(CancellationToken cancellationToken = default)
        {
            lock (slots) return Task.FromResult<IReadOnlyList<SaveSlotInfo>>(slots.Keys.Select(k => new SaveSlotInfo(k, DateTimeOffset.UnixEpoch)).ToList());
        }

        public Task DeleteAsync(string slot, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    /// <summary>Plays a little: a script running with two threads on home, args and all.</summary>
    private static async Task<(GameSession Session, SaveGame Save, double UsedRam)> StartPlayedGameAsync(TestOutput output)
    {
        var session = GameSession.Create(new GameSessionOptions { Output = output });
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
        var output = new TestOutput();
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
        var output = new TestOutput();
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
        var output = new TestOutput();
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
        var output = new TestOutput();
        var (first, _, _) = await StartPlayedGameAsync(output);
        await using var keepAlive = first;

        var manager = new SaveManager(new MemoryStore(), Serializer);
        await manager.SaveAsync(first, "slot1", TestContext.Current.CancellationToken);

        var restored = await manager.LoadAsync("slot1", TestContext.Current.CancellationToken);
        Assert.NotNull(restored);
        Assert.Equal(new[] { "loop.cx" }, restored!.State.FileSystem.ListFiles("home"));
        Assert.Null(await manager.LoadAsync("missing", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Autosave_writes_the_autosave_slot_on_its_interval()
    {
        var output = new TestOutput();
        var (first, _, _) = await StartPlayedGameAsync(output);
        await using var keepAlive = first;

        var store = new MemoryStore();
        var manager = new SaveManager(store, Serializer);

        await using (new AutosaveService(manager, first, output, TimeSpan.FromMilliseconds(50)))
        {
            for (var i = 0; i < 100 && await store.ReadAsync(AutosaveService.Slot, TestContext.Current.CancellationToken) is null; i++)
                await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        Assert.NotNull(await store.ReadAsync(AutosaveService.Slot, TestContext.Current.CancellationToken));
        Assert.Contains(output.Lines, line => line == "Game autosaved.");
    }

    [Fact]
    public async Task The_file_store_keeps_the_previous_save_as_a_backup_and_rejects_unsafe_names()
    {
        var directory = Path.Combine(Path.GetTempPath(), "nullbyte-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileSaveStore(directory);

            Assert.Empty(await store.ListAsync(TestContext.Current.CancellationToken));
            Assert.Null(await store.ReadAsync("one", TestContext.Current.CancellationToken));

            await store.WriteAsync("one", "first", TestContext.Current.CancellationToken);
            await store.WriteAsync("one", "second", TestContext.Current.CancellationToken);

            Assert.Equal("second", await store.ReadAsync("one", TestContext.Current.CancellationToken));
            Assert.Equal("first", await File.ReadAllTextAsync(Path.Combine(directory, "one.json.bak"), TestContext.Current.CancellationToken));
            Assert.Equal(new[] { "one" }, (await store.ListAsync(TestContext.Current.CancellationToken)).Select(slot => slot.Name));
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));

            await Assert.ThrowsAsync<ArgumentException>(() => store.WriteAsync("../outside", "x", TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentException>(() => store.WriteAsync("", "x", TestContext.Current.CancellationToken));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
