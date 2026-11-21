using NullByte.ConsoleUI.Terminal;

namespace NullByte.ConsoleUI.Tests;

public class PathCompletionTests
{
    private static CompletionSnapshot Snapshot(string currentDirectory = "")
    {
        var commands = new[]
        {
            new CommandCompletionInfo("cd", [new ArgumentCompletion { ArgumentIndex = 0, Kind = CompletionKind.Directory }]),
            new CommandCompletionInfo("cat", [new ArgumentCompletion { ArgumentIndex = 0, Kind = CompletionKind.Filename }]),
            new CommandCompletionInfo("scp", [new ArgumentCompletion { ArgumentIndex = -1, Kind = CompletionKind.FilenameOrKnownServer }]),
        };

        var values = new Dictionary<CompletionKind, IReadOnlyList<string>>
        {
            [CompletionKind.Directory] = new[] { "lib", "tools", "tools/net", "tools/net/deep" },
            [CompletionKind.Filename] = new[] { "root.cx", "lib/x.cx", "tools/a.cx", "tools/hello.cx", "tools/net/scan.cx" },
            [CompletionKind.KnownServer] = new[] { "home", "n00dles" },
        };

        return new CompletionSnapshot(commands, values, currentDirectory);
    }

    private static CompletionResult Complete(CompletionSnapshot snapshot, string input)
        => CompletionEngine.Complete(snapshot, input, input.Length);

    // ---- from the root ----

    [Fact]
    public void The_root_offers_its_files_and_directories_one_level_at_a_time()
    {
        Assert.Equal(new[] { "lib/", "root.cx", "tools/" }, Complete(Snapshot(), "cat ").Candidates);
    }

    [Fact]
    public void A_single_directory_completes_with_a_slash_and_no_space()
    {
        var result = Complete(Snapshot(), "cat to");

        Assert.Equal("tools/", result.Replacement);
        Assert.Equal(4, result.ReplaceStart);
        Assert.Equal(2, result.ReplaceLength);
    }

    [Fact]
    public void A_single_file_completes_with_a_space()
    {
        Assert.Equal("tools/hello.cx ", Complete(Snapshot(), "cat tools/he").Replacement);
    }

    [Fact]
    public void Typing_a_directory_shows_what_is_inside_it()
    {
        Assert.Equal(new[] { "tools/a.cx", "tools/hello.cx", "tools/net/" }, Complete(Snapshot(), "cat tools/").Candidates);
    }

    [Fact]
    public void Only_directories_are_offered_where_a_directory_is_expected()
    {
        Assert.Equal(new[] { "lib/", "tools/" }, Complete(Snapshot(), "cd ").Candidates);
        Assert.Equal(new[] { "tools/net/" }, Complete(Snapshot(), "cd tools/").Candidates);
    }

    // ---- relative to the current directory ----

    [Fact]
    public void Completion_is_relative_to_the_current_directory()
    {
        var snapshot = Snapshot("tools");

        Assert.Equal(new[] { "a.cx", "hello.cx", "net/" }, Complete(snapshot, "cat ").Candidates);
        Assert.Equal("hello.cx ", Complete(snapshot, "cat he").Replacement);
        Assert.Equal("net/", Complete(snapshot, "cat n").Replacement);
        Assert.Equal(new[] { "net/deep/", "net/scan.cx" }, Complete(snapshot, "cat net/").Candidates);
    }

    [Fact]
    public void A_leading_slash_completes_from_the_root()
    {
        var snapshot = Snapshot("tools");

        Assert.Equal(new[] { "/lib/", "/root.cx", "/tools/" }, Complete(snapshot, "cat /").Candidates);
        Assert.Equal("/root.cx ", Complete(snapshot, "cat /ro").Replacement);
    }

    [Fact]
    public void Parent_directories_complete_from_where_they_lead()
    {
        var snapshot = Snapshot("tools/net");

        Assert.Equal(new[] { "../a.cx", "../hello.cx", "../net/" }, Complete(snapshot, "cat ../").Candidates);
        Assert.Equal("../../lib/", Complete(snapshot, "cat ../../l").Replacement);
    }

    [Fact]
    public void Dots_complete_to_the_directory_steps()
    {
        var snapshot = Snapshot("tools");

        var parent = Complete(snapshot, "cd ..");
        Assert.Equal("../", parent.Replacement);   // no trailing space: more path can follow

        Assert.Equal(new[] { "../", "./" }, Complete(snapshot, "cd .").Candidates);
    }

    // ---- mistakes and other kinds ----

    [Fact]
    public void A_path_through_something_that_does_not_exist_completes_nothing()
    {
        Assert.False(Complete(Snapshot(), "cat nope/").HasCandidates);
        Assert.False(Complete(Snapshot(), "cat a//b/").HasCandidates);
    }

    [Fact]
    public void Matching_ignores_case()
    {
        Assert.Equal("tools/", Complete(Snapshot(), "cat TO").Replacement);
    }

    [Fact]
    public void Scp_offers_files_directories_and_servers_together()
    {
        Assert.Equal(new[] { "home", "lib/", "n00dles", "root.cx", "tools/" }, Complete(Snapshot(), "scp ").Candidates);
        Assert.Equal("n00dles ", Complete(Snapshot(), "scp n").Replacement);
        Assert.Equal("tools/", Complete(Snapshot(), "scp to").Replacement);
    }
}