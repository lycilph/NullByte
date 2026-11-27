using NullByte.ConsoleUI.Commands;
using NullByte.ConsoleUI.Tests.Infrastructure;
using NullByte.Core.Domain;
using NullByte.Core.Domain.Progression;

namespace NullByte.ConsoleUI.Tests;

public sealed class ProgressionCommandTests
{
    // A game that is on the journey, so that nothing is unlocked until the story says so.
    private static ConsoleTestHarness CreateJourneyHarness()
    {
        var home = TestGameFactory.CreateServer("home");
        var state = new GameState(
            new Player(home.Id),
            TestGameFactory.CreateNetwork(home),
            ProgressionState.NewJourney());

        return ConsoleTestSetup.CreateHarness(new ConsoleTestOptions { State = state });
    }

    private static Chapter Granting(params GameFeature[] features)
        => new("test", "Test", "", ["story"], [], features);

    private static bool IsAvailable(ConsoleTestHarness harness, string commandLine)
        => harness.Registry.TryResolve(commandLine.Split(' '), out _, out _);

    // ---------- gating ----------

    [Theory]
    [InlineData("scan")]
    [InlineData("connect n00dles")]
    [InlineData("hack")]
    [InlineData("ls")]
    [InlineData("run")]
    [InlineData("botnet")]
    [InlineData("botnet buy")]
    [InlineData("cloud")]
    [InlineData("study")]
    [InlineData("backdoor")]
    public void A_new_journey_has_no_gated_commands(string commandLine)
    {
        var harness = CreateJourneyHarness();

        Assert.False(IsAvailable(harness, commandLine));
    }

    [Theory]
    [InlineData("help")]
    [InlineData("status")]
    [InlineData("money")]
    [InlineData("journey")]
    [InlineData("exit")]
    [InlineData("clear")]
    public void The_basics_are_there_from_the_start(string commandLine)
    {
        var harness = CreateJourneyHarness();

        Assert.True(IsAvailable(harness, commandLine));
    }

    [Theory]
    [InlineData("save")]
    [InlineData("saves")]
    [InlineData("load")]
    [InlineData("reset")]
    public void Saving_and_loading_never_need_unlocking(string name)
    {
        // The test host cannot save, so these are not registered there; the table is what matters.
        Assert.Contains(name, CommandUnlocks.AlwaysAvailable);
    }

    [Fact]
    public void Debug_commands_ignore_the_story()
    {
        var harness = CreateJourneyHarness();

        Assert.True(IsAvailable(harness, "debug"));
        Assert.True(IsAvailable(harness, "cheat"));
    }

    [Fact]
    public void Unlocking_a_feature_makes_its_commands_available()
    {
        var harness = CreateJourneyHarness();
        Assert.False(IsAvailable(harness, "hack"));

        harness.State.Progression.Complete(Granting(GameFeature.Hack));

        Assert.True(IsAvailable(harness, "hack"));
        Assert.False(IsAvailable(harness, "weaken"));
    }

    [Fact]
    public void A_subcommand_is_unlocked_with_its_parent_feature()
    {
        var harness = CreateJourneyHarness();

        harness.State.Progression.Complete(Granting(GameFeature.Botnet));

        Assert.True(IsAvailable(harness, "botnet"));
        Assert.True(IsAvailable(harness, "botnet buy"));
        Assert.True(IsAvailable(harness, "botnet upgrade"));
    }

    [Fact]
    public async Task Help_lists_only_what_has_been_unlocked()
    {
        var harness = CreateJourneyHarness();

        var before = await harness.RunAndCaptureAsync("help");
        Assert.DoesNotContain(before, line => line.TrimStart().StartsWith("hack"));

        harness.State.Progression.Complete(Granting(GameFeature.Hack));

        var after = await harness.RunAndCaptureAsync("help");
        Assert.Contains(after, line => line.TrimStart().StartsWith("hack"));
    }

    [Fact]
    public async Task Typing_a_locked_command_points_to_the_journey()
    {
        var harness = CreateJourneyHarness();

        var lines = await harness.RunAndCaptureAsync("hack");

        Assert.Contains("You do not know how to 'hack' yet.", lines);
        Assert.Contains(lines, line => line.Contains("journey"));
    }

    [Fact]
    public async Task Typing_a_command_that_does_not_exist_is_still_unknown()
    {
        var harness = CreateJourneyHarness();

        var lines = await harness.RunAndCaptureAsync("frobnicate");

        Assert.Contains("Unknown command: frobnicate", lines);
    }

    [Fact]
    public void An_unrestricted_game_has_every_command()
    {
        var harness = ConsoleTestSetup.CreateHarness();

        Assert.True(IsAvailable(harness, "hack"));
        Assert.True(IsAvailable(harness, "botnet buy"));
        Assert.True(IsAvailable(harness, "cloud"));
    }

    // ---------- coherence ----------

    [Fact]
    public void Every_command_has_an_unlock_decision()
    {
        var registry = ConsoleTestSetup.CreateRegistry();

        // A new command must be added to CommandUnlocks (and STORY.md), either as gated or as always available.
        Assert.All(registry.Commands, command => Assert.True(CommandUnlocks.IsDecided(command), command.Name));
    }

    [Fact]
    public void Every_feature_unlocks_at_least_one_command()
    {
        var registry = ConsoleTestSetup.CreateRegistry();

        var unlocked = registry.Commands
            .Select(command => CommandUnlocks.TryGetFeature(command, out var feature) ? (GameFeature?)feature : null)
            .Where(feature => feature is not null)
            .Select(feature => feature!.Value)
            .ToHashSet();

        Assert.All(Enum.GetValues<GameFeature>(), feature => Assert.Contains(feature, unlocked));
    }

    // ---------- the journey command ----------

    [Fact]
    public async Task The_journey_starts_with_no_chapters_and_the_first_hint()
    {
        var harness = CreateJourneyHarness();
        var journey = harness.Session.Services.Journey;

        var lines = await harness.RunAndCaptureAsync("journey");

        Assert.Contains($"Journey: 0 of {journey.Chapters.Count} chapters", lines);
    }

    [Fact]
    public async Task The_journey_lists_completed_chapters_and_gives_the_hint_for_the_next()
    {
        var harness = CreateJourneyHarness();
        var journey = harness.Session.Services.Journey;
        harness.State.Progression.Complete(journey.Chapters[0]);
        harness.State.Progression.Complete(journey.Chapters[1]);

        var lines = await harness.RunAndCaptureAsync("journey");

        Assert.Contains($"Journey: 2 of {journey.Chapters.Count} chapters", lines);
        Assert.Contains(lines, line => line.Contains(journey.Chapters[0].Title));
        Assert.Contains(lines, line => line.Contains(journey.Chapters[1].Title));
        Assert.DoesNotContain(lines, line => line.Contains(journey.Chapters[2].Title));
        Assert.Contains(journey.Chapters[2].Hint, lines);
    }

    [Fact]
    public async Task A_completed_chapter_can_be_read_again()
    {
        var harness = CreateJourneyHarness();
        var journey = harness.Session.Services.Journey;
        harness.State.Progression.Complete(journey.Chapters[0]);

        var lines = await harness.RunAndCaptureAsync("journey 1");

        Assert.Contains(journey.Chapters[0].Story[0], lines);
    }

    [Fact]
    public async Task A_chapter_that_has_not_happened_stays_secret()
    {
        var harness = CreateJourneyHarness();
        var journey = harness.Session.Services.Journey;

        var lines = await harness.RunAndCaptureAsync("journey 3");

        Assert.Contains("You have not reached chapter 3 yet.", lines);
        Assert.DoesNotContain(lines, line => line.Contains(journey.Chapters[2].Story[0]));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("999")]
    [InlineData("one")]
    public async Task A_chapter_number_outside_the_story_is_refused(string argument)
    {
        var harness = CreateJourneyHarness();

        var lines = await harness.RunAndCaptureAsync($"journey {argument}");

        Assert.Contains(lines, line => line.StartsWith($"There is no chapter '{argument}'"));
    }

    [Fact]
    public async Task An_unrestricted_game_has_no_story_to_show()
    {
        var harness = ConsoleTestSetup.CreateHarness();

        var lines = await harness.RunAndCaptureAsync("journey");

        Assert.Contains("There is no story in this game: everything is unlocked.", lines);
    }
}
