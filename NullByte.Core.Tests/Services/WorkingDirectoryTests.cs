using NullByte.Core.Configuration;
using NullByte.Core.Domain;
using NullByte.Core.Formulas;
using NullByte.Core.Services;
using NullByte.Core.Time;
using static NullByte.Core.Tests.Domain.FileSystemTestSupport;

namespace NullByte.Core.Tests;

public class WorkingDirectoryTests
{
    private static (GameState State, WorkingDirectory Work, NetworkService Network) Create()
    {
        var network = new Network();
        network.AddServer(NewServer("home"));
        network.AddServer(NewServer("n00dles"));
        network.Connect("home", "n00dles");

        var state = new GameState(new Player("home", 0, 0), network);
        var networkService = new NetworkService(
            state, new GameScheduler(new FakeGameClock()), new NetworkFormulas(new BalanceConfig()));

        return (state, new WorkingDirectory(state), networkService);
    }

    [Fact]
    public void The_player_starts_at_the_root_and_can_change_to_existing_directories()
    {
        var (state, work, _) = Create();
        state.FileSystem.CreateDirectory("home", "tools/net");

        Assert.Equal("", work.Current);

        Assert.True(work.TryChange("tools/net", out _));
        Assert.Equal("tools/net", work.Current);

        Assert.True(work.TryChange("..", out _));
        Assert.Equal("tools", work.Current);
    }

    [Fact]
    public void Changing_to_something_that_is_not_a_directory_explains_why_and_changes_nothing()
    {
        var (state, work, _) = Create();
        state.FileSystem.WriteFile("home", "a.cx", "x");

        Assert.False(work.TryChange("nope", out var missing));
        Assert.Equal("Directory not found: nope", missing);

        Assert.False(work.TryChange("a.cx", out var file));
        Assert.Equal("a.cx is a file, not a directory.", file);

        Assert.Equal("", work.Current);
    }

    [Fact]
    public void A_deleted_current_directory_sends_the_player_back_to_the_root()
    {
        var (state, work, _) = Create();
        state.FileSystem.CreateDirectory("home", "tools/net");
        work.TryChange("tools/net", out _);

        Assert.False(work.ResetIfMissing());   // still there

        state.FileSystem.DeleteDirectory("home", "tools", recursive: true);

        Assert.True(work.ResetIfMissing());
        Assert.Equal("", work.Current);
        Assert.False(work.ResetIfMissing());   // nothing more to do
    }

    [Fact]
    public void The_directory_resets_when_the_player_moves_to_another_server()
    {
        var (state, work, network) = Create();
        state.Player.DiscoverServer("n00dles");
        state.FileSystem.CreateDirectory("home", "tools");
        state.FileSystem.CreateDirectory("n00dles", "logs");
        work.TryChange("tools", out _);

        network.GoHome();   // already home: the server does not change
        Assert.Equal("tools", work.Current);

        network.Connect("n00dles");
        Assert.Equal("n00dles", state.Player.CurrentHostname);
        Assert.Equal("", work.Current);

        work.TryChange("logs", out _);
        network.GoHome();
        Assert.Equal("home", state.Player.CurrentHostname);
        Assert.Equal("", work.Current);
    }
}