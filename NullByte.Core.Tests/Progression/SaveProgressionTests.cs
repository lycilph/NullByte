using System.Text.Json.Nodes;
using NullByte.Core.Domain;
using NullByte.Core.Domain.Progression;
using NullByte.Core.Persistence;
using NullByte.Core.Persistence.Migrations;
using NullByte.Core.Tests.Persistence;
using NullByte.Core.World;

namespace NullByte.Core.Tests.Progression;

public sealed class SaveProgressionTests
{
    private readonly SaveGameSerializer serializer = new();

    private static GameState NewJourneyState() => InitialGameStateFactory.Create();

    private RestoredGame RoundTrip(GameState state)
    {
        var save = SaveGameMapper.Capture(state, TimeSpan.FromMinutes(1), [], DateTimeOffset.UnixEpoch);

        return SaveGameMapper.Restore(serializer.Deserialize(serializer.Serialize(save)));
    }

    [Fact]
    public void Completed_chapters_survive_a_save_in_order()
    {
        var state = NewJourneyState();
        var journey = JourneyFactory.CreateDefault();
        state.Progression.Complete(journey.Chapters[0]);
        state.Progression.Complete(journey.Chapters[1]);

        var restored = RoundTrip(state);

        Assert.False(restored.State.Progression.IsUnrestricted);
        Assert.Equal(new[] { journey.Chapters[0].Id, journey.Chapters[1].Id }, restored.State.Progression.CompletedChapterIds);
    }

    [Fact]
    public void An_unrestricted_game_stays_unrestricted_through_a_save()
    {
        var state = NewJourneyState();
        var unrestricted = new GameState(state.Player, state.Network, ProgressionState.Unrestricted());

        var restored = RoundTrip(unrestricted);

        Assert.True(restored.State.Progression.IsUnrestricted);
        Assert.Empty(restored.State.Progression.CompletedChapterIds);
    }

    [Fact]
    public void The_version_7_migration_leaves_existing_games_unrestricted()
    {
        var save = JsonNode.Parse(GoldenSaves.V7)!.AsObject();

        new V7ToV8Migration().Migrate(save);

        Assert.True(save["progression"]!["unrestricted"]!.GetValue<bool>());
        Assert.Empty(save["progression"]!["completedChapters"]!.AsArray());
    }

    [Fact]
    public void A_version_7_save_loads_with_everything_unlocked()
    {
        var restored = SaveGameMapper.Restore(serializer.Deserialize(GoldenSaves.V7));

        Assert.True(restored.State.Progression.IsUnrestricted);
        Assert.All(Enum.GetValues<GameFeature>(), feature => Assert.True(restored.State.Progression.IsUnlocked(feature)));
    }
}
