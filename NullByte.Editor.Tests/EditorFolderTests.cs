using System.Text.Json;
using System.Text.Json.Nodes;
using NullByte.Core.Domain;
using NullByte.Core.Simulation;
using NullByte.Scripting.Compilation;
using NullByte.Scripting.LanguageService;

namespace NullByte.Editor.Tests;

internal sealed class InlineGameThread : IGameThread
{
    public bool IsGameThread => true;
    public bool IsRunning => true;

    public Task InvokeAsync(Func<Task> callback) => callback();
    public Task<T> InvokeAsync<T>(Func<Task<T>> callback) => callback();
}

internal sealed class EditorFixture
{
    private readonly EditorFileService service;

    public EditorFixture()
    {
        var network = new Network();
        network.AddServer(NewServer("home"));
        network.AddServer(NewServer("n00dles"));

        State = new GameState(new Player("home", 0, 0), network);

        var backend = new EditorBackend(State, new InlineGameThread());
        service = new EditorFileService(() => backend, new ScriptCompiler(), new ScriptLanguageService());
    }

    public GameState State { get; }
    public VirtualFileSystem Files => State.FileSystem;

    /// <summary>Sends one request and returns the whole response object.</summary>
    public async Task<JsonObject> SendAsync(string op, string path, params (string Name, object Value)[] extra)
    {
        var request = new JsonObject { ["id"] = 1, ["op"] = op, ["path"] = path };
        foreach (var (name, value) in extra)
            request[name] = JsonSerializer.SerializeToNode(value);

        return JsonNode.Parse(await service.HandleAsync(request.ToJsonString()))!.AsObject();
    }

    private static Server NewServer(string hostname) => new(
        hostname, maximumMoney: 0, startingMoney: 0, baseDifficulty: 1, minimumSecurity: 1,
        startingSecurity: 1, requiredHackingLevel: 1, growthParameter: 0, maximumRam: 8);
}

public class EditorFolderTests
{
    private static string? Code(JsonObject response)
        => response["ok"]!.GetValue<bool>() ? null : response["error"]!["code"]!.GetValue<string>();

    private static string Message(JsonObject response) => response["error"]!["message"]!.GetValue<string>();

    private static string[] Entries(JsonObject response)
        => response["result"]!["entries"]!.AsArray()
            .Select(e => $"{e!["name"]!.GetValue<string>()}:{e["type"]!.GetValue<string>()}")
            .ToArray();

    // ---- reading ----

    [Fact]
    public async Task Stat_describes_nested_files_and_directories()
    {
        var editor = new EditorFixture();
        editor.Files.WriteFile("home", "tools/net/scan.cx", "héllo");   // 6 bytes in UTF-8

        var file = (await editor.SendAsync("stat", "/home/tools/net/scan.cx"))["result"]!;
        Assert.Equal("file", file["type"]!.GetValue<string>());
        Assert.Equal(6, file["size"]!.GetValue<long>());
        Assert.True(file["created"]!.GetValue<long>() > 0);
        Assert.True(file["modified"]!.GetValue<long>() > 0);

        Assert.Equal("directory", (await editor.SendAsync("stat", "/home/tools/net"))["result"]!["type"]!.GetValue<string>());
        Assert.Equal("directory", (await editor.SendAsync("stat", "/home"))["result"]!["type"]!.GetValue<string>());
        Assert.Equal("NotFound", Code(await editor.SendAsync("stat", "/home/tools/nope")));
    }

    [Fact]
    public async Task ReadDirectory_lists_the_direct_children_with_their_kinds()
    {
        var editor = new EditorFixture();
        editor.Files.WriteFile("home", "z.cx", "x");
        editor.Files.WriteFile("home", "tools/net/scan.cx", "x");
        editor.Files.CreateDirectory("home", "empty");

        Assert.Equal(new[] { "empty:directory", "tools:directory", "z.cx:file" },
            Entries(await editor.SendAsync("readDirectory", "/home")));
        Assert.Equal(new[] { "net:directory" }, Entries(await editor.SendAsync("readDirectory", "/home/tools")));
        Assert.Equal("NotADirectory", Code(await editor.SendAsync("readDirectory", "/home/z.cx")));
        Assert.Equal("NotFound", Code(await editor.SendAsync("readDirectory", "/home/nope")));
    }

    [Fact]
    public async Task ReadFile_reads_nested_files_and_refuses_directories()
    {
        var editor = new EditorFixture();
        editor.Files.WriteFile("home", "tools/a.cx", "hello");

        var response = await editor.SendAsync("readFile", "/home/tools/a.cx");

        Assert.Equal("hello", response["result"]!["content"]!.GetValue<string>());
        Assert.Equal("IsADirectory", Code(await editor.SendAsync("readFile", "/home/tools")));
        Assert.Equal("NotFound", Code(await editor.SendAsync("readFile", "/home/tools/nope.cx")));
    }

    // ---- writing and creating ----

    [Fact]
    public async Task WriteFile_creates_the_file_and_its_directories()
    {
        var editor = new EditorFixture();

        var response = await editor.SendAsync("writeFile", "/home/tools/net/scan.cx",
            ("content", "x"), ("create", true), ("overwrite", false));

        Assert.Null(Code(response));
        Assert.True(response["result"]!["created"]!.GetValue<bool>());
        Assert.Equal(new[] { "tools", "tools/net" }, editor.Files.ListDirectories("home"));
        Assert.Equal(new[] { "tools/net/scan.cx" }, editor.Files.ListFiles("home"));
    }

    [Fact]
    public async Task WriteFile_explains_what_is_in_the_way()
    {
        var editor = new EditorFixture();
        editor.Files.CreateDirectory("home", "dir");
        editor.Files.WriteFile("home", "a.cx", "x");

        Assert.Equal("IsADirectory", Code(await editor.SendAsync("writeFile", "/home/dir", ("content", "x"), ("create", true), ("overwrite", true))));
        Assert.Equal("NotADirectory", Code(await editor.SendAsync("writeFile", "/home/a.cx/b.cx", ("content", "x"), ("create", true), ("overwrite", true))));
        Assert.Equal("FileExists", Code(await editor.SendAsync("writeFile", "/home/a.cx", ("content", "x"), ("create", true), ("overwrite", false))));
        Assert.Equal("NotFound", Code(await editor.SendAsync("writeFile", "/home/new.cx", ("content", "x"), ("create", false), ("overwrite", false))));
        Assert.Equal("NoPermissions", Code(await editor.SendAsync("writeFile", "/home", ("content", "x"), ("create", true), ("overwrite", true))));
    }

    [Fact]
    public async Task CreateDirectory_creates_empty_directories_and_parents()
    {
        var editor = new EditorFixture();

        Assert.Null(Code(await editor.SendAsync("createDirectory", "/home/tools/net")));

        Assert.Equal(new[] { "tools", "tools/net" }, editor.Files.ListDirectories("home"));
        Assert.Empty(editor.Files.ListFiles("home"));
    }

    [Fact]
    public async Task CreateDirectory_refuses_what_it_cannot_create()
    {
        var editor = new EditorFixture();
        editor.Files.WriteFile("home", "a.cx", "x");
        editor.Files.CreateDirectory("home", "dir");

        Assert.Equal("FileExists", Code(await editor.SendAsync("createDirectory", "/home/dir")));
        Assert.Equal("FileExists", Code(await editor.SendAsync("createDirectory", "/home/a.cx")));
        Assert.Equal("NotADirectory", Code(await editor.SendAsync("createDirectory", "/home/a.cx/sub")));
        Assert.Equal("NoPermissions", Code(await editor.SendAsync("createDirectory", "/home")));    // servers are not created here
        Assert.Equal("NoPermissions", Code(await editor.SendAsync("createDirectory", "/")));
    }

    // ---- deleting ----

    [Fact]
    public async Task Delete_removes_files_and_empty_directories()
    {
        var editor = new EditorFixture();
        editor.Files.WriteFile("home", "tools/a.cx", "x");
        editor.Files.CreateDirectory("home", "empty");

        Assert.Null(Code(await editor.SendAsync("delete", "/home/tools/a.cx", ("recursive", false))));
        Assert.Null(Code(await editor.SendAsync("delete", "/home/empty", ("recursive", false))));

        Assert.Empty(editor.Files.ListFiles("home"));
        Assert.Equal(new[] { "tools" }, editor.Files.ListDirectories("home"));
    }

    [Fact]
    public async Task Delete_of_a_directory_with_content_needs_the_recursive_flag()
    {
        var editor = new EditorFixture();
        editor.Files.WriteFile("home", "tools/net/a.cx", "x");

        Assert.Equal("NoPermissions", Code(await editor.SendAsync("delete", "/home/tools", ("recursive", false))));
        Assert.True(editor.Files.DirectoryExists("home", "tools"));

        Assert.Null(Code(await editor.SendAsync("delete", "/home/tools", ("recursive", true))));
        Assert.Empty(editor.Files.ListFiles("home"));
        Assert.Empty(editor.Files.ListDirectories("home"));
    }

    [Fact]
    public async Task Delete_refuses_servers_and_reports_missing_paths()
    {
        var editor = new EditorFixture();

        Assert.Equal("NoPermissions", Code(await editor.SendAsync("delete", "/home", ("recursive", true))));
        Assert.Equal("NoPermissions", Code(await editor.SendAsync("delete", "/", ("recursive", true))));
        Assert.Equal("NotFound", Code(await editor.SendAsync("delete", "/home/nope", ("recursive", true))));
    }

    // ---- renaming ----

    [Fact]
    public async Task Rename_moves_a_file_into_a_new_directory()
    {
        var editor = new EditorFixture();
        editor.Files.WriteFile("home", "a.cx", "x");

        var response = await editor.SendAsync("rename", "/home/a.cx", ("newPath", "/home/archive/old/a.cx"), ("overwrite", false));

        Assert.Null(Code(response));
        Assert.Equal(new[] { "archive/old/a.cx" }, editor.Files.ListFiles("home"));
    }

    [Fact]
    public async Task Rename_moves_a_directory_with_its_content()
    {
        var editor = new EditorFixture();
        editor.Files.WriteFile("home", "old/a.cx", "x");
        editor.Files.WriteFile("home", "old/sub/b.cx", "y");

        Assert.Null(Code(await editor.SendAsync("rename", "/home/old", ("newPath", "/home/new"), ("overwrite", false))));

        Assert.Equal(new[] { "new/a.cx", "new/sub/b.cx" }, editor.Files.ListFiles("home"));
    }

    [Fact]
    public async Task Rename_onto_something_that_exists_needs_overwrite_and_the_same_kind()
    {
        var editor = new EditorFixture();
        editor.Files.WriteFile("home", "a.cx", "from a");
        editor.Files.WriteFile("home", "b.cx", "from b");
        editor.Files.WriteFile("home", "src/s.cx", "1");
        editor.Files.WriteFile("home", "dst/d.cx", "2");

        Assert.Equal("FileExists", Code(await editor.SendAsync("rename", "/home/a.cx", ("newPath", "/home/b.cx"), ("overwrite", false))));
        Assert.Equal("NoPermissions", Code(await editor.SendAsync("rename", "/home/a.cx", ("newPath", "/home/dst"), ("overwrite", true))));

        Assert.Null(Code(await editor.SendAsync("rename", "/home/a.cx", ("newPath", "/home/b.cx"), ("overwrite", true))));
        editor.Files.TryReadFile("home", "b.cx", out var content);
        Assert.Equal("from a", content);

        Assert.Null(Code(await editor.SendAsync("rename", "/home/src", ("newPath", "/home/dst"), ("overwrite", true))));
        Assert.Equal(new[] { "b.cx", "dst/s.cx" }, editor.Files.ListFiles("home"));
    }

    [Fact]
    public async Task Rename_refuses_impossible_moves()
    {
        var editor = new EditorFixture();
        editor.State.Player.DiscoverServer("n00dles");
        editor.Files.WriteFile("home", "a/b/c.cx", "x");
        editor.Files.WriteFile("home", "f.cx", "x");

        Assert.Equal("NoPermissions", Code(await editor.SendAsync("rename", "/home/a", ("newPath", "/home/a/inner"), ("overwrite", false))));
        Assert.Equal("NoPermissions", Code(await editor.SendAsync("rename", "/home/a/b", ("newPath", "/home/a"), ("overwrite", true))));
        Assert.Equal("NoPermissions", Code(await editor.SendAsync("rename", "/home/f.cx", ("newPath", "/n00dles/f.cx"), ("overwrite", false))));
        Assert.Equal("NotADirectory", Code(await editor.SendAsync("rename", "/home/a/b/c.cx", ("newPath", "/home/f.cx/c.cx"), ("overwrite", false))));
        Assert.Equal("NotFound", Code(await editor.SendAsync("rename", "/home/nope", ("newPath", "/home/other"), ("overwrite", false))));

        Assert.Equal(new[] { "a/b/c.cx", "f.cx" }, editor.Files.ListFiles("home"));
    }

    [Fact]
    public async Task Rename_to_the_same_path_does_nothing()
    {
        var editor = new EditorFixture();
        editor.Files.WriteFile("home", "a/b.cx", "x");

        Assert.Null(Code(await editor.SendAsync("rename", "/home/a", ("newPath", "/home/a"), ("overwrite", false))));
        Assert.Equal(new[] { "a/b.cx" }, editor.Files.ListFiles("home"));
    }

    // ---- paths and visibility ----

    [Fact]
    public async Task Bad_paths_are_refused_with_a_plain_message()
    {
        var editor = new EditorFixture();

        var dots = await editor.SendAsync("stat", "/home/a/../b");
        Assert.Equal("NoPermissions", Code(dots));
        Assert.StartsWith("Path segments must be simple names", Message(dots));

        // Eight levels are fine, nine are not.
        Assert.Equal("NotFound", Code(await editor.SendAsync("stat", "/home/a/b/c/d/e/f/g/h")));
        Assert.Equal("NoPermissions", Code(await editor.SendAsync("stat", "/home/a/b/c/d/e/f/g/h/i")));
    }

    [Fact]
    public async Task Undiscovered_servers_are_hidden_until_they_are_discovered()
    {
        var editor = new EditorFixture();
        editor.Files.WriteFile("n00dles", "tools/a.cx", "x");

        Assert.Equal("NotFound", Code(await editor.SendAsync("stat", "/n00dles/tools/a.cx")));

        editor.State.Player.DiscoverServer("n00dles");

        Assert.Null(Code(await editor.SendAsync("stat", "/n00dles/tools/a.cx")));
    }

    [Fact]
    public async Task Diagnose_accepts_nested_paths()
    {
        var editor = new EditorFixture();

        var response = await editor.SendAsync("diagnose", "/home/tools/a.cx", ("content", "var a = ;"));

        Assert.Null(Code(response));
        Assert.NotEmpty(response["result"]!["diagnostics"]!.AsArray());
    }
}