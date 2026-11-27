using NullByte.Core.Domain.Progression;
using NullByte.Core.World;

namespace NullByte.Core.Tests.Progression;

/// <summary>
/// Guards the story that ships with the game, so that the journey and the world cannot drift apart.
/// STORY.md describes the same sequence; update both together.
/// </summary>
public sealed class ShippedJourneyTests
{
    private static readonly Journey Journey = JourneyFactory.CreateDefault();

    [Fact]
    public void The_story_begins_at_once()
    {
        Assert.Empty(Journey.Chapters[0].Conditions);
    }

    [Fact]
    public void The_first_chapter_gives_the_player_something_to_do()
    {
        Assert.NotEmpty(Journey.Chapters[0].Grants);
    }

    [Fact]
    public void Every_feature_is_unlocked_by_exactly_one_chapter()
    {
        var granted = Journey.Chapters.SelectMany(chapter => chapter.Grants).ToList();

        foreach (var feature in Enum.GetValues<GameFeature>())
            Assert.Single(granted, feature);
    }

    [Fact]
    public void Every_chapter_after_the_first_explains_what_to_do_next()
    {
        Assert.All(Journey.Chapters.Skip(1), chapter => Assert.False(string.IsNullOrWhiteSpace(chapter.Hint), chapter.Id));
    }

    [Fact]
    public void Every_hostname_in_the_story_exists_in_the_network()
    {
        var network = InitialGameStateFactory.Create().Network;

        var hostnames = Journey.Chapters
            .SelectMany(chapter => chapter.Conditions)
            .Where(condition => condition.Hostname is not null)
            .Select(condition => condition.Hostname!);

        Assert.All(hostnames, hostname => Assert.True(network.TryGetServer(hostname, out _), hostname));
    }

    [Fact]
    public void A_new_game_is_a_journey_and_starts_locked()
    {
        var state = InitialGameStateFactory.Create();

        Assert.False(state.Progression.IsUnrestricted);
        Assert.Empty(state.Progression.UnlockedFeatures);
    }
}
