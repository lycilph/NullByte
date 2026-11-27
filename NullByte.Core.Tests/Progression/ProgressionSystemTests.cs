using NullByte.Core.Configuration;
using NullByte.Core.Domain;
using NullByte.Core.Domain.Progression;
using NullByte.Core.Formulas;
using NullByte.Core.Systems;
using NullByte.Core.Tests.Infrastructure;

namespace NullByte.Core.Tests.Progression;

public sealed class ProgressionSystemTests
{
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(50);

    private static Chapter MakeChapter(string id, GameFeature[] grants, params ChapterCondition[] conditions)
        => new(id, id.ToUpperInvariant(), "hint", [$"story of {id}"], conditions, grants);

    private sealed class World
    {
        public GameState State { get; }
        public RecordingOutput Output { get; } = new();
        public ProgressionSystem System { get; }

        public World(Journey journey, ProgressionState? progression = null, double money = 0)
        {
            var network = new Network();
            var home = TestServers.Home();
            network.AddServer(home);
            network.AddServer(TestServers.Create("n00dles", maximumMoney: 100));
            network.Connect("home", "n00dles");

            State = new GameState(new Player(home.Id, money), network, progression ?? ProgressionState.NewJourney());
            System = new ProgressionSystem(State, journey, new SkillFormulas(new BalanceConfig()), Output);
        }

        public Server N00dles => State.Network.GetServer("n00dles");
    }

    [Fact]
    public void A_chapter_without_conditions_completes_on_the_first_update_and_tells_its_story()
    {
        var world = new World(new Journey([MakeChapter("one", [GameFeature.Scan])]));

        world.System.Update(Tick);

        Assert.True(world.State.Progression.HasCompleted("one"));
        Assert.True(world.State.Progression.IsUnlocked(GameFeature.Scan));
        Assert.Contains("story of one", world.Output.Lines);
        Assert.Contains("-- ONE --", world.Output.Lines);
    }

    [Fact]
    public void A_chapter_waits_until_its_condition_is_met()
    {
        var journey = new Journey([MakeChapter("rich", [GameFeature.Hack], new ChapterCondition(ConditionKind.MoneyAtLeast, 500))]);
        var world = new World(journey, money: 499);

        world.System.Update(Tick);

        Assert.False(world.State.Progression.HasCompleted("rich"));
        Assert.False(world.State.Progression.IsUnlocked(GameFeature.Hack));
        Assert.Empty(world.Output.Lines);

        world.State.Player.AddMoney(1);
        world.System.Update(Tick);

        Assert.True(world.State.Progression.HasCompleted("rich"));
        Assert.True(world.State.Progression.IsUnlocked(GameFeature.Hack));
    }

    [Fact]
    public void Every_condition_of_a_chapter_must_hold()
    {
        var journey = new Journey([MakeChapter("both", [GameFeature.Scan],
            new ChapterCondition(ConditionKind.MoneyAtLeast, 100),
            new ChapterCondition(ConditionKind.RootOnServer, Hostname: "n00dles"))]);
        var world = new World(journey, money: 100);

        world.System.Update(Tick);
        Assert.False(world.State.Progression.HasCompleted("both"));

        world.N00dles.TryGainRootAccess(1);
        world.System.Update(Tick);
        Assert.True(world.State.Progression.HasCompleted("both"));
    }

    [Fact]
    public void Chapters_are_completed_in_order_even_when_a_later_one_is_already_satisfied()
    {
        var journey = new Journey(
        [
            MakeChapter("first", [GameFeature.Scan], new ChapterCondition(ConditionKind.MoneyAtLeast, 1000)),
            MakeChapter("second", [GameFeature.Connect])
        ]);
        var world = new World(journey, money: 0);

        world.System.Update(Tick);

        Assert.Empty(world.State.Progression.CompletedChapterIds);
        Assert.False(world.State.Progression.IsUnlocked(GameFeature.Connect));
    }

    [Fact]
    public void Several_chapters_can_complete_in_one_update_in_order()
    {
        var journey = new Journey(
        [
            MakeChapter("a", [GameFeature.Scan]),
            MakeChapter("b", [GameFeature.Connect]),
            MakeChapter("c", [GameFeature.Nuke], new ChapterCondition(ConditionKind.MoneyAtLeast, 1000))
        ]);
        var world = new World(journey, money: 5);

        world.System.Update(Tick);

        Assert.Equal(new[] { "a", "b" }, world.State.Progression.CompletedChapterIds);
        Assert.True(world.State.Progression.IsUnlocked(GameFeature.Connect));
        Assert.False(world.State.Progression.IsUnlocked(GameFeature.Nuke));
    }

    [Fact]
    public void A_story_is_told_only_once()
    {
        var world = new World(new Journey([MakeChapter("one", [GameFeature.Scan])]));

        world.System.Update(Tick);
        var linesAfterFirst = world.Output.Lines.Count;
        world.System.Update(Tick);
        world.System.Update(Tick);

        Assert.Equal(linesAfterFirst, world.Output.Lines.Count);
    }

    [Fact]
    public void Completing_a_chapter_raises_an_event()
    {
        var world = new World(new Journey([MakeChapter("one", [GameFeature.Scan])]));
        var completed = new List<string>();
        world.System.ChapterCompleted += chapter => completed.Add(chapter.Id);

        world.System.Update(Tick);

        Assert.Equal(new[] { "one" }, completed);
    }

    [Fact]
    public void An_unrestricted_game_is_left_alone()
    {
        var world = new World(new Journey([MakeChapter("one", [GameFeature.Scan])]), ProgressionState.Unrestricted());

        world.System.Update(Tick);

        Assert.Empty(world.State.Progression.CompletedChapterIds);
        Assert.Empty(world.Output.Lines);
    }

    [Fact]
    public void Being_at_a_server_is_checked_by_hostname_and_is_not_remembered()
    {
        var journey = new Journey([MakeChapter("visit", [GameFeature.Scan], new ChapterCondition(ConditionKind.AtServer, Hostname: "n00dles"))]);
        var world = new World(journey);

        world.System.Update(Tick);
        Assert.False(world.State.Progression.HasCompleted("visit"));

        world.State.Player.CurrentServerId = world.N00dles.Id;
        world.System.Update(Tick);
        Assert.True(world.State.Progression.HasCompleted("visit"));
    }

    [Fact]
    public void Counting_conditions_read_the_game_state()
    {
        var journey = new Journey(
        [
            MakeChapter("seen", [GameFeature.Scan], new ChapterCondition(ConditionKind.ServersDiscoveredAtLeast, 2)),
            MakeChapter("owned", [GameFeature.Connect], new ChapterCondition(ConditionKind.ServersRootedAtLeast, 1)),
            MakeChapter("nodes", [GameFeature.Nuke], new ChapterCondition(ConditionKind.BotnetNodesAtLeast, 1))
        ]);
        var world = new World(journey);

        world.System.Update(Tick);
        Assert.Empty(world.State.Progression.CompletedChapterIds);

        world.State.Player.DiscoverServer(world.N00dles.Id);
        world.System.Update(Tick);
        Assert.Equal(new[] { "seen" }, world.State.Progression.CompletedChapterIds);

        world.N00dles.TryGainRootAccess(1);
        world.System.Update(Tick);
        Assert.Equal(new[] { "seen", "owned" }, world.State.Progression.CompletedChapterIds);

        world.State.Botnet.AddNode();
        world.System.Update(Tick);
        Assert.True(world.State.Progression.HasCompleted("nodes"));
    }

    [Fact]
    public void A_loaded_game_gets_its_features_back_from_the_chapters_it_completed()
    {
        var journey = new Journey([MakeChapter("one", [GameFeature.Scan, GameFeature.Connect]), MakeChapter("two", [GameFeature.Hack])]);
        var saved = ProgressionState.NewJourney();
        saved.RestoreCompleted("one");

        var world = new World(journey, saved);

        Assert.True(world.State.Progression.IsUnlocked(GameFeature.Scan));
        Assert.True(world.State.Progression.IsUnlocked(GameFeature.Connect));
        Assert.False(world.State.Progression.IsUnlocked(GameFeature.Hack));

        world.System.Update(Tick);

        // Chapter one is not told again; chapter two (no conditions) is the next news.
        Assert.DoesNotContain("story of one", world.Output.Lines);
        Assert.Contains("story of two", world.Output.Lines);
    }

    [Fact]
    public void A_saved_chapter_that_no_longer_exists_is_ignored()
    {
        var saved = ProgressionState.NewJourney();
        saved.RestoreCompleted("renamed-long-ago");

        var world = new World(new Journey([MakeChapter("one", [GameFeature.Scan])]), saved);

        Assert.False(world.State.Progression.IsUnlocked(GameFeature.Scan));
    }
}
