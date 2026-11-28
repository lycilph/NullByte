using NullByte.ConsoleUI.Terminal;
using NullByte.ConsoleUI.Tests.Infrastructure;
using NullByte.Core.Domain;
using NullByte.Core.Narrative;

namespace NullByte.ConsoleUI.Tests;

/// <summary>A new game with the home server and one more server to copy files to.</summary>
file static class FileGame
{
    public static ConsoleTestHarness Create()
        => ConsoleTestSetup.CreateHarness(new ConsoleTestOptions
        {
            State = TestGameFactory.CreateGame(additionalServers: [TestGameFactory.CreateServer("n00dles")])
        });
}

public class FileCommandsTests
{
    // ---- mkdir ----

    [Fact]
    public async Task Mkdir_creates_a_directory_and_its_parents()
    {
        var game = FileGame.Create();

        Assert.Equal(new[] { "Created tools/net/ on home." }, await game.RunAndCaptureAsync("mkdir tools/net"));

        Assert.Equal(new[] { "tools", "tools/net" }, game.Files.ListDirectories("home"));
    }

    [Fact]
    public async Task Mkdir_explains_what_is_in_the_way()
    {
        var game = FileGame.Create();
        game.Files.WriteFile("home", "a.cx", "x");
        game.Files.CreateDirectory("home", "tools");

        Assert.Equal(new[] { "Directory already exists: tools" }, await game.RunAndCaptureAsync("mkdir tools"));
        Assert.Equal(new[] { "A file with that name already exists: a.cx" }, await game.RunAndCaptureAsync("mkdir a.cx"));
        Assert.Equal(new[] { "'a.cx' is a file." }, await game.RunAndCaptureAsync("mkdir a.cx/sub"));
    }

    [Fact]
    public async Task A_bad_path_gives_a_plain_message()
    {
        var game = FileGame.Create();

        var output = Assert.Single(await game.RunAndCaptureAsync("mkdir a//b"));

        Assert.StartsWith("Path segments must be simple names", output);
        Assert.DoesNotContain("(Parameter", output);
    }

    // ---- ls ----

    [Fact]
    public async Task Ls_lists_directories_first_with_a_slash()
    {
        var game = FileGame.Create();
        game.Files.WriteFile("home", "z.cx", "x");
        game.Files.WriteFile("home", "tools/a.cx", "x");
        game.Files.CreateDirectory("home", "empty");

        Assert.Equal(new[] { "empty/", "tools/", "z.cx" }, await game.RunAndCaptureAsync("ls"));
        Assert.Equal(new[] { "a.cx" }, await game.RunAndCaptureAsync("ls tools"));
    }

    [Fact]
    public async Task Ls_reports_empty_missing_and_file_arguments()
    {
        var game = FileGame.Create();
        game.Files.CreateDirectory("home", "empty");
        game.Files.WriteFile("home", "a.cx", "x");

        Assert.Equal(new[] { "The directory is empty." }, await game.RunAndCaptureAsync("ls empty"));
        Assert.Equal(new[] { "Directory not found: nope" }, await game.RunAndCaptureAsync("ls nope"));
        Assert.Equal(new[] { "a.cx" }, await game.RunAndCaptureAsync("ls a.cx"));
    }

    [Fact]
    public async Task Ls_on_an_empty_server_says_so()
    {
        var game = FileGame.Create();

        Assert.Equal(new[] { "No files on this server." }, await game.RunAndCaptureAsync("ls"));
    }

    // ---- cat ----

    [Fact]
    public async Task Cat_reads_files_in_directories_and_refuses_directories()
    {
        var game = FileGame.Create();
        game.Files.WriteFile("home", "tools/a.cx", "hello");

        Assert.Equal(new[] { "hello" }, await game.RunAndCaptureAsync("cat tools/a.cx"));
        Assert.Equal(new[] { "tools is a directory." }, await game.RunAndCaptureAsync("cat tools"));
        Assert.Equal(new[] { "File not found: nope.cx" }, await game.RunAndCaptureAsync("cat nope.cx"));
    }

    [Fact]
    public async Task Cat_records_a_discovery_once_and_leaves_the_file_available()
    {
        var catalog = new NarrativeContentCatalog([], 
        [new NarrativeDiscovery("project.archive-record", "home", "archive/project.log", NarrativeDiscoveryRequirement.Optional)]);
        var game = ConsoleTestSetup.CreateHarness(new ConsoleTestOptions
        {
            ProgressionMode = NarrativeProgressionMode.Freeplay,
            NarrativeDiscoveries = catalog
        });
        game.Files.WriteFile("home", "archive/project.log", "Recovered record");

        Assert.Equal(new[] { "Recovered record" }, await game.RunAndCaptureAsync("cat archive/project.log"));
        Assert.Equal(new[] { "Recovered record" }, await game.RunAndCaptureAsync("cat archive/project.log"));

        Assert.Contains("project.archive-record", game.Session.State.Narrative.DiscoveredDiscoveries);
        Assert.Empty(game.Session.State.Narrative.UnlockedCapabilities);
        Assert.True(game.Files.TryReadFile("home", "archive/project.log", out var content));
        Assert.Equal("Recovered record", content);
    }

    // ---- rm and rmdir ----

    [Fact]
    public async Task Rm_deletes_a_file_but_explains_directories()
    {
        var game = FileGame.Create();
        game.Files.WriteFile("home", "a.cx", "x");
        game.Files.WriteFile("home", "tools/b.cx", "x");

        Assert.Equal(new[] { "Deleted a.cx." }, await game.RunAndCaptureAsync("rm a.cx"));

        var output = Assert.Single(await game.RunAndCaptureAsync("rm tools"));
        Assert.Contains("rm -r tools", output);
        Assert.True(game.Files.DirectoryExists("home", "tools"));
    }

    [Theory]
    [InlineData("rm -r tools")]
    [InlineData("rm tools -r")]
    [InlineData("rm -R tools")]
    public async Task Rm_with_the_recursive_flag_deletes_a_directory_and_its_content(string command)
    {
        var game = FileGame.Create();
        game.Files.WriteFile("home", "tools/a.cx", "x");
        game.Files.WriteFile("home", "tools/net/b.cx", "x");

        Assert.Equal(new[] { "Deleted tools/ and the 2 file(s) in it." }, await game.RunAndCaptureAsync(command));

        Assert.Empty(game.Files.ListFiles("home"));
        Assert.Empty(game.Files.ListDirectories("home"));
    }

    [Fact]
    public async Task Rm_protects_the_root_and_reports_missing_files()
    {
        var game = FileGame.Create();

        Assert.Equal(new[] { "The root directory cannot be deleted." }, await game.RunAndCaptureAsync("rm -r /"));
        Assert.Equal(new[] { "File not found: nope.cx" }, await game.RunAndCaptureAsync("rm nope.cx"));
        Assert.Equal(new[] { "Usage: rm [-r] <path>" }, await game.RunAndCaptureAsync("rm -r -r"));
    }

    [Fact]
    public async Task Rmdir_only_deletes_empty_directories()
    {
        var game = FileGame.Create();
        game.Files.CreateDirectory("home", "empty");
        game.Files.WriteFile("home", "full/a.cx", "x");
        game.Files.WriteFile("home", "a.cx", "x");

        Assert.Equal(new[] { "Deleted empty/." }, await game.RunAndCaptureAsync("rmdir empty"));
        Assert.StartsWith("Directory not empty: full", Assert.Single(await game.RunAndCaptureAsync("rmdir full")));
        Assert.Equal(new[] { "a.cx is a file. Use 'rm' to delete it." }, await game.RunAndCaptureAsync("rmdir a.cx"));
        Assert.Equal(new[] { "Directory not found: nope" }, await game.RunAndCaptureAsync("rmdir nope"));
    }

    // ---- mv ----

    [Fact]
    public async Task Mv_renames_a_file_and_moves_it_into_a_new_directory()
    {
        var game = FileGame.Create();
        game.Files.WriteFile("home", "a.cx", "x");

        Assert.Equal(new[] { "Moved a.cx to archive/old/a.cx on home." }, await game.RunAndCaptureAsync("mv a.cx archive/old/a.cx"));

        Assert.Equal(new[] { "archive/old/a.cx" }, game.Files.ListFiles("home"));
    }

    [Fact]
    public async Task Mv_moves_a_directory_with_its_content()
    {
        var game = FileGame.Create();
        game.Files.WriteFile("home", "old/a.cx", "x");

        Assert.Equal(new[] { "Moved old to new on home." }, await game.RunAndCaptureAsync("mv old new"));

        Assert.Equal(new[] { "new/a.cx" }, game.Files.ListFiles("home"));
    }

    [Fact]
    public async Task Mv_refuses_what_it_cannot_do()
    {
        var game = FileGame.Create();
        game.Files.WriteFile("home", "a.cx", "x");
        game.Files.WriteFile("home", "b.cx", "y");
        game.Files.WriteFile("home", "dir/c.cx", "z");

        Assert.Equal(new[] { "File already exists: b.cx" }, await game.RunAndCaptureAsync("mv a.cx b.cx"));
        Assert.Equal(new[] { "Directory already exists: dir" }, await game.RunAndCaptureAsync("mv a.cx dir"));
        Assert.Equal(new[] { "b.cx is a file, not a directory." }, await game.RunAndCaptureAsync("mv a.cx b.cx/a.cx"));
        Assert.Equal(new[] { "A directory cannot be moved into itself." }, await game.RunAndCaptureAsync("mv dir dir/inner"));
        Assert.Equal(new[] { "File not found: nope" }, await game.RunAndCaptureAsync("mv nope other"));

        Assert.Equal(new[] { "a.cx", "b.cx", "dir/c.cx" }, game.Files.ListFiles("home"));
    }

    // ---- scp ----

    [Fact]
    public async Task Scp_copies_a_file_in_a_directory_and_creates_the_directory_there()
    {
        var game = FileGame.Create();
        game.Files.WriteFile("home", "tools/a.cx", "x");

        Assert.Equal(new[] { "Files tools/a.cx copied to n00dles" }, await game.RunAndCaptureAsync("scp tools/a.cx n00dles"));

        Assert.True(game.Files.TryReadFile("n00dles", "tools/a.cx", out _));
        Assert.True(game.Files.DirectoryExists("n00dles", "tools"));
    }

    [Fact]
    public async Task Scp_says_it_copies_files_only()
    {
        var game = FileGame.Create();
        game.Files.WriteFile("home", "tools/a.cx", "x");

        Assert.Equal(new[] { "tools is a directory; scp copies files only." }, await game.RunAndCaptureAsync("scp tools n00dles"));
    }

    // ---- edit ----

    [Fact]
    public async Task Edit_refuses_before_the_editor_opens()
    {
        var game = FileGame.Create();
        game.Files.WriteFile("home", "a.cx", "x");
        game.Files.CreateDirectory("home", "tools");

        Assert.Equal(new[] { "tools is a directory." }, await game.RunAndCaptureAsync("edit tools"));
        Assert.Equal(new[] { "a.cx is a file, not a directory." }, await game.RunAndCaptureAsync("edit a.cx/b.cx"));
    }

    // ---- tab completion ----

    [Fact]
    public void Tab_completion_offers_directories_where_they_make_sense()
    {
        var commands = ConsoleTestSetup.CreateRegistry().Commands.Select(c => new CommandCompletionInfo(c.Name, c.Completions)).ToArray();
        var values = new Dictionary<CompletionKind, IReadOnlyList<string>>
        {
            [CompletionKind.Directory] = new[] { "tools", "tools/net" },
            [CompletionKind.Filename] = new[] { "a.cx" },
        };
        var snapshot = new CompletionSnapshot(commands, values);

        Assert.Equal(new[] { "tools/" }, CompletionEngine.Complete(snapshot, "mkdir to", 8).Candidates);
        Assert.Equal(new[] { "tools/" }, CompletionEngine.Complete(snapshot, "cd ", 3).Candidates);   // directories only
        Assert.Equal(new[] { "a.cx", "tools/" }, CompletionEngine.Complete(snapshot, "cat ", 4).Candidates);
        Assert.Equal(new[] { "a.cx", "tools/" }, CompletionEngine.Complete(snapshot, "rm -r ", 6).Candidates);
    }
}

public class WorkingDirectoryCommandsTests
{
    [Fact]
    public async Task Pwd_and_cd_move_around_the_directory_tree()
    {
        var game = FileGame.Create();
        game.Files.CreateDirectory("home", "tools/net");

        Assert.Equal(new[] { "/" }, await game.RunAndCaptureAsync("pwd"));

        Assert.Empty(await game.RunAndCaptureAsync("cd tools/net"));
        Assert.Equal(new[] { "/tools/net" }, await game.RunAndCaptureAsync("pwd"));

        await game.RunAndCaptureAsync("cd ..");
        Assert.Equal(new[] { "/tools" }, await game.RunAndCaptureAsync("pwd"));

        await game.RunAndCaptureAsync("cd net");
        await game.RunAndCaptureAsync("cd /");
        Assert.Equal(new[] { "/" }, await game.RunAndCaptureAsync("pwd"));

        await game.RunAndCaptureAsync("cd tools/net");
        await game.RunAndCaptureAsync("cd");   // no argument: the root
        Assert.Equal(new[] { "/" }, await game.RunAndCaptureAsync("pwd"));

        await game.RunAndCaptureAsync("cd tools");
        await game.RunAndCaptureAsync("cd ../../..");   // cannot go above the root
        Assert.Equal(new[] { "/" }, await game.RunAndCaptureAsync("pwd"));
    }

    [Fact]
    public async Task Cd_explains_what_is_wrong_and_stays_where_it_is()
    {
        var game = FileGame.Create();
        game.Files.WriteFile("home", "a.cx", "x");

        Assert.Equal(new[] { "Directory not found: nope" }, await game.RunAndCaptureAsync("cd nope"));
        Assert.Equal(new[] { "a.cx is a file, not a directory." }, await game.RunAndCaptureAsync("cd a.cx"));
        Assert.StartsWith("Path segments must be simple names", Assert.Single(await game.RunAndCaptureAsync("cd a//b")));

        Assert.Equal(new[] { "/" }, await game.RunAndCaptureAsync("pwd"));
    }

    [Fact]
    public async Task Commands_work_relative_to_the_current_directory()
    {
        var game = FileGame.Create();
        game.Files.WriteFile("home", "tools/a.cx", "hello");
        game.Files.WriteFile("home", "root.cx", "x");
        await game.RunAndCaptureAsync("cd tools");

        Assert.Equal(new[] { "a.cx" }, await game.RunAndCaptureAsync("ls"));
        Assert.Equal(new[] { "tools/", "root.cx" }, await game.RunAndCaptureAsync("ls /"));
        Assert.Equal(new[] { "tools/", "root.cx" }, await game.RunAndCaptureAsync("ls .."));
        Assert.Equal(new[] { "hello" }, await game.RunAndCaptureAsync("cat a.cx"));
        Assert.Equal(new[] { "x" }, await game.RunAndCaptureAsync("cat ../root.cx"));

        Assert.Equal(new[] { "Created tools/sub/ on home." }, await game.RunAndCaptureAsync("mkdir sub"));
        Assert.Equal(new[] { "Moved tools/a.cx to tools/sub/b.cx on home." }, await game.RunAndCaptureAsync("mv a.cx sub/b.cx"));
        Assert.Equal(new[] { "Deleted tools/sub/b.cx." }, await game.RunAndCaptureAsync("rm sub/b.cx"));
        Assert.Equal(new[] { "Deleted tools/sub/." }, await game.RunAndCaptureAsync("rmdir sub"));
    }

    [Fact]
    public async Task Scp_keeps_the_full_path_on_the_destination()
    {
        var game = FileGame.Create();
        game.Files.WriteFile("home", "tools/a.cx", "x");
        await game.RunAndCaptureAsync("cd tools");

        Assert.Equal(new[] { "Files tools/a.cx copied to n00dles" }, await game.RunAndCaptureAsync("scp a.cx n00dles"));

        Assert.True(game.Files.TryReadFile("n00dles", "tools/a.cx", out _));
    }
}