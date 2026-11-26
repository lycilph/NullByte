using System.Text.Json.Nodes;
using NullByte.Core.Domain;
using NullByte.Core.Persistence;
using NullByte.Core.Persistence.Migrations;
using static NullByte.Core.Tests.Infrastructure.TestFileSystem;

namespace NullByte.Core.Tests.Persistence;

public class SaveDirectoriesTests
{
    private static GameState NewState()
    {
        var network = new Network();
        network.AddServer(NewServer("home"));
        network.AddServer(NewServer("n00dles"));

        return new GameState(new Player("home", 0, 0), network);
    }

    private static GameState StateWithFolders()
    {
        var state = NewState();
        state.FileSystem.CreateDirectory("home", "empty");
        state.FileSystem.WriteFile("home", "tools/net/scan.cx", "scan");
        state.FileSystem.CreateDirectory("n00dles", "logs");
        return state;
    }

    private static string Save(GameState state)
        => new SaveGameSerializer().Serialize(
            SaveGameMapper.Capture(state, TimeSpan.Zero, [], DateTimeOffset.UnixEpoch));

    // ---- round trip ----

    [Fact]
    public void Directories_survive_a_save_and_load_including_empty_ones()
    {
        var state = StateWithFolders();
        state.FileSystem.TryGetFileTimes("home", "tools/net/scan.cx", out var before);

        var serializer = new SaveGameSerializer();
        var restored = SaveGameMapper.Restore(serializer.Deserialize(Save(state))).State.FileSystem;

        Assert.Equal(new[] { "empty", "tools", "tools/net" }, restored.ListDirectories("home"));
        Assert.Equal(new[] { "logs" }, restored.ListDirectories("n00dles"));
        Assert.Equal(new[] { "tools/net/scan.cx" }, restored.ListFiles("home"));
        Assert.True(restored.TryGetFileTimes("home", "tools/net/scan.cx", out var after));
        Assert.Equal(before, after);
    }

    [Fact]
    public void Saved_directories_are_sorted_by_server_and_path()
    {
        var node = JsonNode.Parse(Save(StateWithFolders()))!.AsObject();

        var saved = node["directories"]!.AsArray()
            .Select(d => $"{d!["hostname"]}:{d["path"]}")
            .ToArray();

        Assert.Equal(new[] { "home:empty", "home:tools", "home:tools/net", "n00dles:logs" }, saved);
    }

    // ---- migration ----

    [Fact]
    public void The_migration_adds_an_empty_directory_list_and_leaves_the_rest_alone()
    {
        var save = JsonNode.Parse("""{ "version": 2, "files": [ { "hostname": "home", "name": "a.cx" } ] }""")!.AsObject();

        new V2ToV3Migration().Migrate(save);

        Assert.Empty(save["directories"]!.AsArray());
        Assert.Equal("a.cx", save["files"]![0]!["name"]!.GetValue<string>());
    }

    [Fact]
    public void A_version_1_save_is_upgraded_all_the_way_to_version_3()
    {
        var save = JsonNode.Parse("""
            { "version": 1, "savedAtUtc": "2026-01-01T00:00:00+00:00",
              "files": [ { "hostname": "home", "name": "a.cx", "content": "x" } ] }
            """)!.AsObject();

        new SaveMigrator(currentVersion: 3).Upgrade(save);

        Assert.Equal(3, save["version"]!.GetValue<int>());
        Assert.NotNull(save["files"]![0]!["created"]);
        Assert.Empty(save["directories"]!.AsArray());
    }

    [Fact]
    public void A_version_2_save_loads_with_no_saved_directories()
    {
        var node = JsonNode.Parse(Save(StateWithFolders()))!.AsObject();
        node["version"] = 2;
        node.Remove("directories");

        var save = new SaveGameSerializer().Deserialize(node.ToJsonString());

        Assert.Equal(SaveGame.CurrentVersion, save.Version);
        Assert.Empty(save.Directories);

        // The files still bring their parent directories with them.
        var files = SaveGameMapper.Restore(save).State.FileSystem;
        Assert.Equal(new[] { "tools", "tools/net" }, files.ListDirectories("home"));
    }

    // ---- damaged saves ----

    [Theory]
    [InlineData("home", "..")]        // not a valid path
    [InlineData("nowhere", "a")]      // not a server
    public void A_save_with_an_invalid_directory_is_refused(string hostname, string path)
    {
        var node = JsonNode.Parse(Save(NewState()))!.AsObject();
        node["directories"]!.AsArray().Add(new JsonObject { ["hostname"] = hostname, ["path"] = path });

        var save = new SaveGameSerializer().Deserialize(node.ToJsonString());

        Assert.Throws<SaveFormatException>(() => SaveGameMapper.Restore(save));
    }

    [Fact]
    public void A_save_with_a_directory_that_is_also_a_file_is_refused()
    {
        var state = NewState();
        state.FileSystem.WriteFile("home", "a.cx", "x");

        var node = JsonNode.Parse(Save(state))!.AsObject();
        node["directories"]!.AsArray().Add(new JsonObject { ["hostname"] = "home", ["path"] = "a.cx" });

        var save = new SaveGameSerializer().Deserialize(node.ToJsonString());

        Assert.Throws<SaveFormatException>(() => SaveGameMapper.Restore(save));
    }

    [Fact]
    public void A_save_without_the_directory_list_is_damaged()
    {
        var node = JsonNode.Parse(Save(NewState()))!.AsObject();
        node.Remove("directories");   // version 3 requires it

        Assert.Throws<SaveFormatException>(() => new SaveGameSerializer().Deserialize(node.ToJsonString()));
    }
}