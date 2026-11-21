using NullByte.ConsoleUI.Commands;
using NullByte.ConsoleUI.Commands.Groups;
using NullByte.ConsoleUI.Terminal;
using NullByte.Core.Configuration;
using NullByte.Core.Domain;
using NullByte.Core.Formulas;
using NullByte.Core.Services;
using NullByte.Core.Time;

namespace NullByte.ConsoleUI.Tests;

internal sealed class RecordingTerminal : IGameTerminal
{
    public List<string> Lines { get; } = [];

    public CancellationToken StoppingToken => CancellationToken.None;

    public void WriteLine(string message) => Lines.Add(message);
    public void WriteLine() => Lines.Add("");

    public void SuspendProcessing() { }
    public void ResumeProcessing() { }
    public void StartProgress(string msg, Func<double> progress) { }
    public void StopProgress() { }
    public void SetPrompt(string prompt) { }
    public void SetInputEnabled(bool enabled) { }
    public void Clear() { }

    public IAsyncEnumerable<string> ReadCommandsAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}

internal sealed class FileCommandsFixture
{
    private readonly RecordingTerminal terminal = new();
    private readonly CommandDispatcher dispatcher;

    public FileCommandsFixture()
    {
        var network = new Network();
        network.AddServer(NewServer("home"));
        network.AddServer(NewServer("n00dles"));

        var state = new GameState(new Player("home", 0, 0), network);
        var networkService = new NetworkService(
            state, new GameScheduler(new FakeGameClock()), new NetworkFormulas(new BalanceConfig()));

        Files = state.FileSystem;
        Registry = new CommandRegistry();
        Work = new WorkingDirectory(state);
        new FileCommands(Files, networkService, Work, terminal).Register(Registry);
        dispatcher = new CommandDispatcher(Registry, terminal);
    }

    public VirtualFileSystem Files { get; }
    public WorkingDirectory Work { get; }
    public CommandRegistry Registry { get; }

    /// <summary>Runs a command line and returns what it printed.</summary>
    public async Task<IReadOnlyList<string>> RunAsync(string commandLine)
    {
        terminal.Lines.Clear();
        await dispatcher.DispatchAsync(commandLine);
        return terminal.Lines.ToArray();
    }

    private static Server NewServer(string hostname) => new(
        hostname, maximumMoney: 0, startingMoney: 0, baseDifficulty: 1, minimumSecurity: 1,
        startingSecurity: 1, requiredHackingLevel: 1, growthParameter: 0, maximumRam: 8);
}

public class FileCommandsTests
{
    // ---- mkdir ----

    [Fact]
    public async Task Mkdir_creates_a_directory_and_its_parents()
    {
        var game = new FileCommandsFixture();

        Assert.Equal(new[] { "Created tools/net/ on home." }, await game.RunAsync("mkdir tools/net"));

        Assert.Equal(new[] { "tools", "tools/net" }, game.Files.ListDirectories("home"));
    }

    [Fact]
    public async Task Mkdir_explains_what_is_in_the_way()
    {
        var game = new FileCommandsFixture();
        game.Files.WriteFile("home", "a.cx", "x");
        game.Files.CreateDirectory("home", "tools");

        Assert.Equal(new[] { "Directory already exists: tools" }, await game.RunAsync("mkdir tools"));
        Assert.Equal(new[] { "A file with that name already exists: a.cx" }, await game.RunAsync("mkdir a.cx"));
        Assert.Equal(new[] { "'a.cx' is a file." }, await game.RunAsync("mkdir a.cx/sub"));
    }

    [Fact]
    public async Task A_bad_path_gives_a_plain_message()
    {
        var game = new FileCommandsFixture();

        var output = Assert.Single(await game.RunAsync("mkdir a//b"));

        Assert.StartsWith("Path segments must be simple names", output);
        Assert.DoesNotContain("(Parameter", output);
    }

    // ---- ls ----

    [Fact]
    public async Task Ls_lists_directories_first_with_a_slash()
    {
        var game = new FileCommandsFixture();
        game.Files.WriteFile("home", "z.cx", "x");
        game.Files.WriteFile("home", "tools/a.cx", "x");
        game.Files.CreateDirectory("home", "empty");

        Assert.Equal(new[] { "empty/", "tools/", "z.cx" }, await game.RunAsync("ls"));
        Assert.Equal(new[] { "a.cx" }, await game.RunAsync("ls tools"));
    }

    [Fact]
    public async Task Ls_reports_empty_missing_and_file_arguments()
    {
        var game = new FileCommandsFixture();
        game.Files.CreateDirectory("home", "empty");
        game.Files.WriteFile("home", "a.cx", "x");

        Assert.Equal(new[] { "The directory is empty." }, await game.RunAsync("ls empty"));
        Assert.Equal(new[] { "Directory not found: nope" }, await game.RunAsync("ls nope"));
        Assert.Equal(new[] { "a.cx" }, await game.RunAsync("ls a.cx"));
    }

    [Fact]
    public async Task Ls_on_an_empty_server_says_so()
    {
        var game = new FileCommandsFixture();

        Assert.Equal(new[] { "No files on this server." }, await game.RunAsync("ls"));
    }

    // ---- cat ----

    [Fact]
    public async Task Cat_reads_files_in_directories_and_refuses_directories()
    {
        var game = new FileCommandsFixture();
        game.Files.WriteFile("home", "tools/a.cx", "hello");

        Assert.Equal(new[] { "hello" }, await game.RunAsync("cat tools/a.cx"));
        Assert.Equal(new[] { "tools is a directory." }, await game.RunAsync("cat tools"));
        Assert.Equal(new[] { "File not found: nope.cx" }, await game.RunAsync("cat nope.cx"));
    }

    // ---- rm and rmdir ----

    [Fact]
    public async Task Rm_deletes_a_file_but_explains_directories()
    {
        var game = new FileCommandsFixture();
        game.Files.WriteFile("home", "a.cx", "x");
        game.Files.WriteFile("home", "tools/b.cx", "x");

        Assert.Equal(new[] { "Deleted a.cx." }, await game.RunAsync("rm a.cx"));

        var output = Assert.Single(await game.RunAsync("rm tools"));
        Assert.Contains("rm -r tools", output);
        Assert.True(game.Files.DirectoryExists("home", "tools"));
    }

    [Theory]
    [InlineData("rm -r tools")]
    [InlineData("rm tools -r")]
    [InlineData("rm -R tools")]
    public async Task Rm_with_the_recursive_flag_deletes_a_directory_and_its_content(string command)
    {
        var game = new FileCommandsFixture();
        game.Files.WriteFile("home", "tools/a.cx", "x");
        game.Files.WriteFile("home", "tools/net/b.cx", "x");

        Assert.Equal(new[] { "Deleted tools/ and the 2 file(s) in it." }, await game.RunAsync(command));

        Assert.Empty(game.Files.ListFiles("home"));
        Assert.Empty(game.Files.ListDirectories("home"));
    }

    [Fact]
    public async Task Rm_protects_the_root_and_reports_missing_files()
    {
        var game = new FileCommandsFixture();

        Assert.Equal(new[] { "The root directory cannot be deleted." }, await game.RunAsync("rm -r /"));
        Assert.Equal(new[] { "File not found: nope.cx" }, await game.RunAsync("rm nope.cx"));
        Assert.Equal(new[] { "Usage: rm [-r] <path>" }, await game.RunAsync("rm -r -r"));
    }

    [Fact]
    public async Task Rmdir_only_deletes_empty_directories()
    {
        var game = new FileCommandsFixture();
        game.Files.CreateDirectory("home", "empty");
        game.Files.WriteFile("home", "full/a.cx", "x");
        game.Files.WriteFile("home", "a.cx", "x");

        Assert.Equal(new[] { "Deleted empty/." }, await game.RunAsync("rmdir empty"));
        Assert.StartsWith("Directory not empty: full", Assert.Single(await game.RunAsync("rmdir full")));
        Assert.Equal(new[] { "a.cx is a file. Use 'rm' to delete it." }, await game.RunAsync("rmdir a.cx"));
        Assert.Equal(new[] { "Directory not found: nope" }, await game.RunAsync("rmdir nope"));
    }

    // ---- mv ----

    [Fact]
    public async Task Mv_renames_a_file_and_moves_it_into_a_new_directory()
    {
        var game = new FileCommandsFixture();
        game.Files.WriteFile("home", "a.cx", "x");

        Assert.Equal(new[] { "Moved a.cx to archive/old/a.cx on home." }, await game.RunAsync("mv a.cx archive/old/a.cx"));

        Assert.Equal(new[] { "archive/old/a.cx" }, game.Files.ListFiles("home"));
    }

    [Fact]
    public async Task Mv_moves_a_directory_with_its_content()
    {
        var game = new FileCommandsFixture();
        game.Files.WriteFile("home", "old/a.cx", "x");

        Assert.Equal(new[] { "Moved old to new on home." }, await game.RunAsync("mv old new"));

        Assert.Equal(new[] { "new/a.cx" }, game.Files.ListFiles("home"));
    }

    [Fact]
    public async Task Mv_refuses_what_it_cannot_do()
    {
        var game = new FileCommandsFixture();
        game.Files.WriteFile("home", "a.cx", "x");
        game.Files.WriteFile("home", "b.cx", "y");
        game.Files.WriteFile("home", "dir/c.cx", "z");

        Assert.Equal(new[] { "File already exists: b.cx" }, await game.RunAsync("mv a.cx b.cx"));
        Assert.Equal(new[] { "Directory already exists: dir" }, await game.RunAsync("mv a.cx dir"));
        Assert.Equal(new[] { "b.cx is a file, not a directory." }, await game.RunAsync("mv a.cx b.cx/a.cx"));
        Assert.Equal(new[] { "A directory cannot be moved into itself." }, await game.RunAsync("mv dir dir/inner"));
        Assert.Equal(new[] { "File not found: nope" }, await game.RunAsync("mv nope other"));

        Assert.Equal(new[] { "a.cx", "b.cx", "dir/c.cx" }, game.Files.ListFiles("home"));
    }

    // ---- scp ----

    [Fact]
    public async Task Scp_copies_a_file_in_a_directory_and_creates_the_directory_there()
    {
        var game = new FileCommandsFixture();
        game.Files.WriteFile("home", "tools/a.cx", "x");

        Assert.Equal(new[] { "Files tools/a.cx copied to n00dles" }, await game.RunAsync("scp tools/a.cx n00dles"));

        Assert.True(game.Files.TryReadFile("n00dles", "tools/a.cx", out _));
        Assert.True(game.Files.DirectoryExists("n00dles", "tools"));
    }

    [Fact]
    public async Task Scp_says_it_copies_files_only()
    {
        var game = new FileCommandsFixture();
        game.Files.WriteFile("home", "tools/a.cx", "x");

        Assert.Equal(new[] { "tools is a directory; scp copies files only." }, await game.RunAsync("scp tools n00dles"));
    }

    // ---- edit ----

    [Fact]
    public async Task Edit_refuses_before_the_editor_opens()
    {
        var game = new FileCommandsFixture();
        game.Files.WriteFile("home", "a.cx", "x");
        game.Files.CreateDirectory("home", "tools");

        Assert.Equal(new[] { "tools is a directory." }, await game.RunAsync("edit tools"));
        Assert.Equal(new[] { "a.cx is a file, not a directory." }, await game.RunAsync("edit a.cx/b.cx"));
    }

    // ---- tab completion ----

    [Fact]
    public void Tab_completion_offers_directories_where_they_make_sense()
    {
        var game = new FileCommandsFixture();
        var commands = game.Registry.Commands.Select(c => new CommandCompletionInfo(c.Name, c.Completions)).ToArray();
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
        var game = new FileCommandsFixture();
        game.Files.CreateDirectory("home", "tools/net");

        Assert.Equal(new[] { "/" }, await game.RunAsync("pwd"));

        Assert.Empty(await game.RunAsync("cd tools/net"));
        Assert.Equal(new[] { "/tools/net" }, await game.RunAsync("pwd"));

        await game.RunAsync("cd ..");
        Assert.Equal(new[] { "/tools" }, await game.RunAsync("pwd"));

        await game.RunAsync("cd net");
        await game.RunAsync("cd /");
        Assert.Equal(new[] { "/" }, await game.RunAsync("pwd"));

        await game.RunAsync("cd tools/net");
        await game.RunAsync("cd");   // no argument: the root
        Assert.Equal(new[] { "/" }, await game.RunAsync("pwd"));

        await game.RunAsync("cd tools");
        await game.RunAsync("cd ../../..");   // cannot go above the root
        Assert.Equal(new[] { "/" }, await game.RunAsync("pwd"));
    }

    [Fact]
    public async Task Cd_explains_what_is_wrong_and_stays_where_it_is()
    {
        var game = new FileCommandsFixture();
        game.Files.WriteFile("home", "a.cx", "x");

        Assert.Equal(new[] { "Directory not found: nope" }, await game.RunAsync("cd nope"));
        Assert.Equal(new[] { "a.cx is a file, not a directory." }, await game.RunAsync("cd a.cx"));
        Assert.StartsWith("Path segments must be simple names", Assert.Single(await game.RunAsync("cd a//b")));

        Assert.Equal(new[] { "/" }, await game.RunAsync("pwd"));
    }

    [Fact]
    public async Task Commands_work_relative_to_the_current_directory()
    {
        var game = new FileCommandsFixture();
        game.Files.WriteFile("home", "tools/a.cx", "hello");
        game.Files.WriteFile("home", "root.cx", "x");
        await game.RunAsync("cd tools");

        Assert.Equal(new[] { "a.cx" }, await game.RunAsync("ls"));
        Assert.Equal(new[] { "tools/", "root.cx" }, await game.RunAsync("ls /"));
        Assert.Equal(new[] { "tools/", "root.cx" }, await game.RunAsync("ls .."));
        Assert.Equal(new[] { "hello" }, await game.RunAsync("cat a.cx"));
        Assert.Equal(new[] { "x" }, await game.RunAsync("cat ../root.cx"));

        Assert.Equal(new[] { "Created tools/sub/ on home." }, await game.RunAsync("mkdir sub"));
        Assert.Equal(new[] { "Moved tools/a.cx to tools/sub/b.cx on home." }, await game.RunAsync("mv a.cx sub/b.cx"));
        Assert.Equal(new[] { "Deleted tools/sub/b.cx." }, await game.RunAsync("rm sub/b.cx"));
        Assert.Equal(new[] { "Deleted tools/sub/." }, await game.RunAsync("rmdir sub"));
    }

    [Fact]
    public async Task Scp_keeps_the_full_path_on_the_destination()
    {
        var game = new FileCommandsFixture();
        game.Files.WriteFile("home", "tools/a.cx", "x");
        await game.RunAsync("cd tools");

        Assert.Equal(new[] { "Files tools/a.cx copied to n00dles" }, await game.RunAsync("scp a.cx n00dles"));

        Assert.True(game.Files.TryReadFile("n00dles", "tools/a.cx", out _));
    }
}