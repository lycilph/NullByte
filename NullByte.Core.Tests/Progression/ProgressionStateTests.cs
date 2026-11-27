using NullByte.Core.Domain.Progression;

namespace NullByte.Core.Tests.Progression;

public sealed class ProgressionStateTests
{
    private static Chapter ChapterGranting(string id, params GameFeature[] grants)
        => new(id, id, "", ["story"], [], grants);

    [Fact]
    public void An_unrestricted_state_has_every_feature()
    {
        var state = ProgressionState.Unrestricted();

        Assert.All(Enum.GetValues<GameFeature>(), feature => Assert.True(state.IsUnlocked(feature)));
    }

    [Fact]
    public void A_new_journey_has_no_features()
    {
        var state = ProgressionState.NewJourney();

        Assert.All(Enum.GetValues<GameFeature>(), feature => Assert.False(state.IsUnlocked(feature)));
        Assert.Empty(state.CompletedChapterIds);
    }

    [Fact]
    public void Completing_a_chapter_unlocks_what_it_grants_and_nothing_else()
    {
        var state = ProgressionState.NewJourney();

        var completed = state.Complete(ChapterGranting("one", GameFeature.Scan, GameFeature.Connect));

        Assert.True(completed);
        Assert.True(state.IsUnlocked(GameFeature.Scan));
        Assert.True(state.IsUnlocked(GameFeature.Connect));
        Assert.False(state.IsUnlocked(GameFeature.Hack));
        Assert.Equal(new[] { "one" }, state.CompletedChapterIds);
    }

    [Fact]
    public void Completing_a_chapter_twice_does_nothing_the_second_time()
    {
        var state = ProgressionState.NewJourney();
        var chapter = ChapterGranting("one", GameFeature.Scan);

        state.Complete(chapter);

        Assert.False(state.Complete(chapter));
        Assert.Equal(new[] { "one" }, state.CompletedChapterIds);
    }

    [Fact]
    public void Chapters_are_remembered_in_the_order_they_were_completed()
    {
        var state = ProgressionState.NewJourney();

        state.Complete(ChapterGranting("a"));
        state.Complete(ChapterGranting("b"));
        state.Complete(ChapterGranting("c"));

        Assert.Equal(new[] { "a", "b", "c" }, state.CompletedChapterIds);
    }

    [Fact]
    public void The_journey_offers_the_first_unfinished_chapter_next()
    {
        var journey = new Journey([ChapterGranting("a"), ChapterGranting("b"), ChapterGranting("c")]);
        var state = ProgressionState.NewJourney();

        Assert.Equal("a", journey.NextChapter(state)?.Id);

        state.Complete(journey.Chapters[0]);
        Assert.Equal("b", journey.NextChapter(state)?.Id);

        state.Complete(journey.Chapters[1]);
        state.Complete(journey.Chapters[2]);
        Assert.Null(journey.NextChapter(state));
    }

    [Fact]
    public void A_journey_with_a_repeated_chapter_id_is_refused()
    {
        Assert.Throws<ArgumentException>(() => new Journey([ChapterGranting("a"), ChapterGranting("a")]));
    }
}
