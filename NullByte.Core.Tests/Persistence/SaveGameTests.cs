using System.Text.Json.Nodes;
using NullByte.Core.Domain;
using NullByte.Core.Persistence;
using NullByte.Core.Persistence.Migrations;

namespace NullByte.Core.Tests.Persistence;

public class SaveGameTests
{
    private static readonly DateTimeOffset SavedAt = new(2026, 11, 17, 10, 30, 0, TimeSpan.Zero);
    private static readonly TimeSpan GameTime = TimeSpan.FromSeconds(754.25);
    private static readonly SaveGameSerializer Serializer = new();

    private const string FoodnstuffServerId = "83d7acb2-037f-5621-bd1d-c708349ae12f";
    private const string HomeServerId = "6e6c7219-14ba-58f9-ac7f-2df3c2f12f5e";
    private const string N00dlesServerId = "98a3870e-58cb-5079-93b5-bb06f209320b";
    private const string ZeroDayServerId = "e68d08ef-e8d3-5b8b-9bd3-001535c25142";

    private static readonly ProcessSave[] Processes =
    [
        new() { Filename = "weaken.cx", ServerId = HomeServerId, Args = [], Threads = 1 },
        new() { Filename = "hack.cx", ServerId = N00dlesServerId, Args = ["foodnstuff", "two words"], Threads = 2 }
    ];

    /// <summary>A small game that has been played a bit: every kind of state the save has to carry.</summary>
    internal static GameState CreateState()
    {
        var network = new Network();
        network.AddServer(new Server("home", 0, 0, 1, 1, 1, 1, 1, 8, hasRootAccess: true, id: ServerId.Parse(HomeServerId)));
        network.AddServer(new Server("n00dles", 50_000, 25_000, 3, 1, 3, 1, 30, 4, id: ServerId.Parse(N00dlesServerId)));
        network.AddServer(new Server("foodnstuff", 100_000, 60_000, 5, 2, 5, 5, 25, 8, id: ServerId.Parse(FoodnstuffServerId)));
        network.AddServer(new Server("zero-day", 1_000_000, 500_000, 40, 7.54, 66.5, 50, 10, 16, id: ServerId.Parse(ZeroDayServerId)));

        // Deliberately not made in alphabetical order.
        network.Connect("home", "n00dles");
        network.Connect("home", "foodnstuff");
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

        state.FileSystem.RestoreFile("home", "hack.cx", "var t = args[0];\nnb.Print(\"héllo \\\"world\\\" ✓\");\n", SavedAt, SavedAt);
        state.FileSystem.RestoreFile("home", "empty.cx", "", SavedAt, SavedAt);
        state.FileSystem.RestoreFile("n00dles", "hack.cx", "await nb.Hack();", SavedAt, SavedAt);

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
    public void Neighbors_are_alphabetical_before_and_after_a_load()
    {
        var original = CreateState();
        var restored = SaveGameMapper.Restore(Serializer.Deserialize(Describe(original)));

        foreach (var state in new[] { original, restored.State })
        {
            Assert.Equal(
                new[] { "foodnstuff", "n00dles" },
                state.Network.GetNeighbors("home").Select(server => server.Hostname));
            Assert.Equal(
                new[] { "foodnstuff", "home" },
                state.Network.GetNeighbors("n00dles").Select(server => server.Hostname));
        }
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
    public void The_file_does_not_depend_on_the_order_things_were_created_in()
    {
        // A restored game is built in a different order than the one that was saved, so the file
        // must describe the graph, not the history of how it was put together.
        var hosts = new[] { "home", "n00dles", "foodnstuff", "zero-day" };
        var edges = new[] { ("home", "n00dles"), ("home", "foodnstuff"), ("n00dles", "foodnstuff"), ("foodnstuff", "zero-day") };

        string Save(IEnumerable<string> hostOrder, IEnumerable<(string, string)> edgeOrder)
        {
            var network = new Network();
            foreach (var host in hostOrder)
                network.AddServer(new Server(host, 1000, 500, 3, 1, 3, 1, 30, 4, hasRootAccess: host == "home"));
            foreach (var (a, b) in edgeOrder)
                network.Connect(a, b);

            return Serializer.Serialize(SaveGameMapper.Capture(new GameState(new Player("home"), network), GameTime, [], SavedAt));
        }

        var reversedAndFlipped = edges.Reverse().Select(edge => (edge.Item2, edge.Item1));

        Assert.Equal(Save(hosts, edges), Save(hosts.Reverse(), reversedAndFlipped));
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
        string expected = Normalize(GoldenSaves.V6);
        string actual = Describe(CreateState());
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void The_current_golden_file_loads_and_saves_back_unchanged()
    {
        var restored = SaveGameMapper.Restore(Serializer.Deserialize(GoldenSaves.V5));

        var again = Serializer.Serialize(SaveGameMapper.Capture(restored.State, restored.GameTime, restored.Processes, SavedAt));

        Assert.Equal(Normalize(GoldenSaves.V5), again);
    }

    [Fact]
    public void The_golden_file_is_migrated_correctly()
    {
        var restored = SaveGameMapper.Restore(Serializer.Deserialize(GoldenSaves.V1));

        var again = Serializer.Serialize(SaveGameMapper.Capture(restored.State, restored.GameTime, restored.Processes, SavedAt));

        Assert.Equal(Normalize(GoldenSaves.V5), again);
    }

    [Fact]
    public void The_version_4_golden_file_is_migrated_correctly()
    {
        var restored = SaveGameMapper.Restore(Serializer.Deserialize(GoldenSaves.V4));

        var again = Serializer.Serialize(SaveGameMapper.Capture(restored.State, restored.GameTime, restored.Processes, SavedAt));

        Assert.Equal(Normalize(GoldenSaves.V5), again);
    }

    [Fact]
    public void The_version_5_migration_leaves_every_server_without_a_backdoor()
    {
        var save = JsonNode.Parse(GoldenSaves.V4)!.AsObject();

        new V4ToV5Migration().Migrate(save);

        var servers = save["servers"]!.AsArray();
        Assert.NotEmpty(servers);
        Assert.All(servers, server => Assert.False(server!["hasBackdoor"]!.GetValue<bool>()));
    }

    [Fact]
    public void The_version_5_migration_keeps_a_backdoor_that_is_already_there()
    {
        var save = JsonNode.Parse("""{ "servers": [ { "hostname": "a", "hasBackdoor": true } ] }""")!.AsObject();

        new V4ToV5Migration().Migrate(save);

        Assert.True(save["servers"]![0]!["hasBackdoor"]!.GetValue<bool>());
    }

    [Fact]
    public void A_backdoor_survives_a_save_and_load()
    {
        var original = CreateState();
        original.Network.GetServer("n00dles").InstallBackdoor();

        var restored = SaveGameMapper.Restore(Serializer.Deserialize(Describe(original)));

        Assert.True(restored.State.Network.GetServer("n00dles").HasBackdoor);
        Assert.False(restored.State.Network.GetServer("foodnstuff").HasBackdoor);
        Assert.Equal(Describe(original), Describe(restored.State));
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
        // Starts from the newest golden save, because these made-up migrations do not add what the real ones do.
        var migrator = new SaveMigrator(
            [new AddMoney(5, 100, steps), new AddMoney(6, 20, steps)],
            currentVersion: 7);
        var serializer = new SaveGameSerializer(migrator);

        var loaded = serializer.Deserialize(GoldenSaves.V5);

        Assert.Equal(new[] { 5, 6 }, steps);
        Assert.Equal(7, loaded.Version);
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
    [InlineData("processes[0].serverId", "\"00000000-0000-0000-0000-000000000000\"")]
    [InlineData("processes[0].threads", "0")]
    [InlineData("gameTime", "\"-00:00:01\"")]
    public void Data_the_game_would_reject_is_refused_before_anything_is_started(string path, string value)
    {
        var save = Serializer.Deserialize(WithValue(path, value));

        Assert.Throws<SaveFormatException>(() => SaveGameMapper.Restore(save));
    }

    [Fact]
    public void CaptureAndRestore_PreservesFileTimestamps()
    {
        var state = CreateState();
        var created = new DateTimeOffset(
            2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

        var modified = new DateTimeOffset(
            2026, 1, 3, 6, 7, 8, TimeSpan.Zero);

        state.FileSystem.RestoreFile(
            "home",
            "script.cx",
            "print(\"hello\");",
            created,
            modified);

        var save = SaveGameMapper.Capture(
            state,
            TimeSpan.FromMinutes(5),
            [],
            new DateTimeOffset(2026, 1, 4, 0, 0, 0, TimeSpan.Zero));

        //var file = Assert.Single(save.Files);
        var file = save.Files.First(s => string.Equals(s.Name, "script.cx"));

        Assert.Equal(created, file.Created);
        Assert.Equal(modified, file.Modified);

        var restored = SaveGameMapper.Restore(save);

        Assert.True(restored.State.FileSystem.TryGetFileTimes("home", "script.cx", out var restoredTimes));
        Assert.Equal(created, restoredTimes.Created);
        Assert.Equal(modified, restoredTimes.Modified);

        Assert.True(restored.State.FileSystem.TryReadFile("home", "script.cx", out var content));
        Assert.Equal("print(\"hello\");", content);
    }

    [Fact]
    public void Version_1_save_gets_file_timestamps_from_saved_at()
    {
        var restored = SaveGameMapper.Restore(Serializer.Deserialize(GoldenSaves.V1));

        var expected = new DateTimeOffset(
            2026, 11, 17, 10, 30, 0, TimeSpan.Zero);

        var files = new[]
        {
            ("home", "empty.cx"),
            ("home", "hack.cx"),
            ("n00dles", "hack.cx")
        };

        foreach (var (hostname, filename) in files)
        {
            Assert.True(restored.State.FileSystem.TryGetFileTimes(hostname, filename, out var times));
            Assert.Equal(expected, times.Created);
            Assert.Equal(expected, times.Modified);
        }
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
