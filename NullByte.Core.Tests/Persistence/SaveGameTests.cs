using System.Text.Json.Nodes;
using NullByte.Core.Domain;
using NullByte.Core.Persistence;

namespace NullByte.Core.Tests.Persistence;

public class SaveGameTests
{
    private static readonly DateTimeOffset SavedAt = new(2026, 11, 17, 10, 30, 0, TimeSpan.Zero);
    private static readonly TimeSpan GameTime = TimeSpan.FromSeconds(754.25);
    private static readonly SaveGameSerializer Serializer = new();

    private static readonly ProcessSave[] Processes =
    [
        new() { Filename = "weaken.cx", Hostname = "home", Args = [], Threads = 1 },
        new() { Filename = "hack.cx", Hostname = "n00dles", Args = ["foodnstuff", "two words"], Threads = 2 }
    ];

    /// <summary>A small game that has been played a bit: every kind of state the save has to carry.</summary>
    internal static GameState CreateState()
    {
        var network = new Network();
        network.AddServer(new Server("home", 0, 0, 1, 1, 1, 1, 1, 8, hasRootAccess: true));
        network.AddServer(new Server("n00dles", 50_000, 25_000, 3, 1, 3, 1, 30, 4));
        network.AddServer(new Server("foodnstuff", 100_000, 60_000, 5, 2, 5, 5, 25, 8));
        network.AddServer(new Server("zero-day", 1_000_000, 500_000, 40, 7.54, 66.5, 50, 10, 16));

        network.Connect("home", "foodnstuff");
        network.Connect("home", "n00dles");
        network.Connect("n00dles", "foodnstuff");
        network.Connect("foodnstuff", "zero-day");

        var player = new Player("home", startingMoney: 0.1 + 0.2, startingHackingExperience: 1.0 / 3.0)
        {
            CurrentHostname = "n00dles"
        };
        player.DiscoverServer("n00dles");
        player.DiscoverServer("foodnstuff");

        var state = new GameState(player, network);

        network.GetServer("n00dles").GrantRootAccess();
        network.GetServer("n00dles").RemoveMoney(1.0 / 3.0);
        network.GetServer("n00dles").IncreaseSecurity(0.002);

        var node = state.Botnet.AddNode();
        node.ApplyUpgrade(BotnetUpgradeKind.Level);
        node.ApplyUpgrade(BotnetUpgradeKind.Ram);
        node.ApplyUpgrade(BotnetUpgradeKind.Cores);
        state.Botnet.AddNode();
        state.Botnet.RecordEarnings(12.345);

        state.FileSystem.WriteFile("home", "hack.cx", "var t = args[0];\nnb.Print(\"héllo \\\"world\\\" ✓\");\n");
        state.FileSystem.WriteFile("home", "empty.cx", "");
        state.FileSystem.WriteFile("n00dles", "hack.cx", "await nb.Hack();");

        return state;
    }

    private static SaveGame Capture(GameState state) => SaveGameMapper.Capture(state, GameTime, Processes, SavedAt);

    private static string Describe(GameState state) => Serializer.Serialize(Capture(state));

    [Fact]
    public void Restore_reproduces_everything_that_was_saved()
    {
        var original = CreateState();

        var restored = SaveGameMapper.Restore(Serializer.Deserialize(Serializer.Serialize(Capture(original))));

        // Comparing the captured form checks every field in one go, including exact doubles.
        Assert.Equal(Describe(original), Describe(restored.State));
        Assert.Equal(GameTime, restored.GameTime);
        // The list order is the restart order, so it must survive.
        Assert.Equal(new[] { "weaken.cx", "hack.cx" }, restored.Processes.Select(p => p.Filename));

        Assert.Equal(0.1 + 0.2, restored.State.Player.Money);
        Assert.Equal(1.0 / 3.0, restored.State.Player.HackingExperience);
        Assert.Equal("n00dles", restored.State.Player.CurrentHostname);
        Assert.True(restored.State.Network.GetServer("n00dles").HasRootAccess);
        Assert.Equal(2, restored.State.Botnet.NodeCount);
        Assert.Equal(12.345, restored.State.Botnet.TotalEarned);
        Assert.Equal(0.0, restored.State.Network.GetServer("home").UsedRam);
    }

    [Fact]
    public void Neighbors_keep_the_order_scan_shows_them_in()
    {
        var restored = SaveGameMapper.Restore(Serializer.Deserialize(Describe(CreateState())));

        Assert.Equal(
            new[] { "foodnstuff", "n00dles" },
            restored.State.Network.GetNeighbors("home").Select(server => server.Hostname));
        Assert.Equal(
            new[] { "foodnstuff", "home" },
            restored.State.Network.GetNeighbors("n00dles").Select(server => server.Hostname));
    }

    [Fact]
    public void File_contents_survive_exactly()
    {
        var restored = SaveGameMapper.Restore(Serializer.Deserialize(Describe(CreateState())));

        Assert.True(restored.State.FileSystem.TryReadFile("home", "hack.cx", out var content));
        Assert.Equal("var t = args[0];\nnb.Print(\"héllo \\\"world\\\" ✓\");\n", content);
        Assert.True(restored.State.FileSystem.TryReadFile("home", "empty.cx", out var empty));
        Assert.Equal("", empty);
    }

    [Fact]
    public void The_same_game_always_produces_the_same_file()
    {
        Assert.Equal(Describe(CreateState()), Describe(CreateState()));
    }

    [Fact]
    public void A_fully_weakened_server_with_fractional_minimum_security_still_loads()
    {
        // Subtracting down to the minimum can land a hair below it (7.539999999999999 for 7.54),
        // which the Server constructor refuses. The save must clamp instead of becoming unloadable.
        var network = new Network();
        network.AddServer(new Server("home", 0, 0, 1, 1, 1, 1, 1, 8, hasRootAccess: true));
        network.AddServer(new Server("zero-day", 1_000_000, 500_000, 40, 7.54, 66.41759912548187, 50, 10, 16));
        var state = new GameState(new Player("home"), network);

        var server = network.GetServer("zero-day");
        server.DecreaseSecurity(1_000_000);
        Assert.True(server.Security < server.MinimumSecurity, "precondition: rounding pushed security below its minimum");

        var json = Serializer.Serialize(SaveGameMapper.Capture(state, GameTime, [], SavedAt));
        var restored = SaveGameMapper.Restore(Serializer.Deserialize(json));

        Assert.Equal(7.54, restored.State.Network.GetServer("zero-day").Security);
    }

    [Fact]
    public void Golden_file_matches_the_current_format()
    {
        // If this fails you changed the save format. Bump SaveGame.CurrentVersion, add a migration,
        // and keep the old golden text as a test for that migration. Do not just edit the golden text.
        var golden = Normalize(GoldenSaves.V1);
        var state = Describe(CreateState());
        Assert.Equal(golden, state);
    }

    [Fact]
    public void The_golden_file_loads_and_saves_back_unchanged()
    {
        var restored = SaveGameMapper.Restore(Serializer.Deserialize(GoldenSaves.V1));

        var again = Serializer.Serialize(SaveGameMapper.Capture(restored.State, restored.GameTime, restored.Processes, SavedAt));

        Assert.Equal(Normalize(GoldenSaves.V1), again);
    }

    [Fact]
    public void A_save_from_a_newer_version_is_refused_with_a_clear_message()
    {
        var json = JsonNode.Parse(Describe(CreateState()))!.AsObject();
        json["version"] = SaveGame.CurrentVersion + 1;

        var error = Assert.Throws<SaveFormatException>(() => Serializer.Deserialize(json.ToJsonString()));

        Assert.Contains("newer version", error.Message);
    }

    [Fact]
    public void Migrations_run_in_order_up_to_the_current_version()
    {
        var steps = new List<int>();
        var migrator = new SaveMigrator(
            [new AddMoney(1, 100, steps), new AddMoney(2, 20, steps)],
            currentVersion: 3);
        var serializer = new SaveGameSerializer(migrator);

        var loaded = serializer.Deserialize(GoldenSaves.V1);

        Assert.Equal(new[] { 1, 2 }, steps);
        Assert.Equal(3, loaded.Version);
        Assert.Equal(0.1 + 0.2 + 120, loaded.Player.Money);
    }

    [Fact]
    public void A_missing_migration_is_an_error_not_a_guess()
    {
        var serializer = new SaveGameSerializer(new SaveMigrator([], currentVersion: 2));

        Assert.Throws<SaveFormatException>(() => serializer.Deserialize(GoldenSaves.V1));
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{ \"version\": \"one\" }")]
    public void Garbage_is_reported_as_a_damaged_save(string text)
    {
        Assert.Throws<SaveFormatException>(() => Serializer.Deserialize(text));
    }

    [Theory]
    [InlineData("files", "null")]
    [InlineData("processes", "{}")]
    [InlineData("player.money", "\"lots\"")]
    public void Wrongly_shaped_data_is_reported_as_a_damaged_save(string path, string value)
    {
        Assert.Throws<SaveFormatException>(() => Serializer.Deserialize(WithValue(path, value)));
    }

    [Theory]
    [InlineData("player.currentHostname", "\"nowhere\"")]
    [InlineData("player.money", "-5")]
    [InlineData("botnet.nodes[0].ram", "3")]
    [InlineData("files[0].name", "\"../escape.cx\"")]
    [InlineData("processes[0].hostname", "\"nowhere\"")]
    [InlineData("processes[0].threads", "0")]
    [InlineData("gameTime", "\"-00:00:01\"")]
    public void Data_the_game_would_reject_is_refused_before_anything_is_started(string path, string value)
    {
        var save = Serializer.Deserialize(WithValue(path, value));

        Assert.Throws<SaveFormatException>(() => SaveGameMapper.Restore(save));
    }

    private static string WithValue(string path, string jsonValue)
    {
        var root = JsonNode.Parse(Describe(CreateState()))!.AsObject();
        var node = (JsonNode)root;

        var segments = path.Split('.');
        for (var i = 0; i < segments.Length; i++)
        {
            var (name, index) = Parse(segments[i]);
            var parent = node;
            node = index is null ? parent[name]! : parent[name]![index.Value]!;

            if (i == segments.Length - 1)
            {
                var replacement = JsonNode.Parse(jsonValue);
                if (index is null)
                    parent.AsObject()[name] = replacement;
                else
                    parent[name]!.AsArray()[index.Value] = replacement;
            }
        }

        return root.ToJsonString();
    }

    private static (string Name, int? Index) Parse(string segment)
    {
        var open = segment.IndexOf('[');
        return open < 0
            ? (segment, null)
            : (segment[..open], int.Parse(segment[(open + 1)..^1]));
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n");

    private sealed class AddMoney(int from, double amount, List<int> steps) : ISaveMigration
    {
        public int FromVersion => from;

        public void Migrate(JsonObject save)
        {
            steps.Add(from);
            save["player"]!["money"] = save["player"]!["money"]!.GetValue<double>() + amount;
        }
    }
}