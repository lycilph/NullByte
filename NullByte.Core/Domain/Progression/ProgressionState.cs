namespace NullByte.Core.Domain.Progression;

/// <summary>
/// How far the player has come in the story, and so which features are unlocked.
/// </summary>
/// <remarks>
/// Only the completed chapter ids are the truth; unlocked features follow from them. Changes
/// happen on the game thread, like the rest of <see cref="GameState"/>.
/// </remarks>
public sealed class ProgressionState
{
    private readonly List<string> completedChapterIds = [];
    private readonly HashSet<GameFeature> unlockedFeatures = [];

    private ProgressionState(bool isUnrestricted) => IsUnrestricted = isUnrestricted;

    /// <summary>Everything is available. This is what a game gets unless it is a journey game, which keeps tests and tools simple.</summary>
    public static ProgressionState Unrestricted() => new(isUnrestricted: true);

    /// <summary>A new journey: nothing is unlocked yet.</summary>
    public static ProgressionState NewJourney() => new(isUnrestricted: false);

    public bool IsUnrestricted { get; }

    public IReadOnlyList<string> CompletedChapterIds => completedChapterIds;

    public IReadOnlySet<GameFeature> UnlockedFeatures => unlockedFeatures;

    public bool IsUnlocked(GameFeature feature) => IsUnrestricted || unlockedFeatures.Contains(feature);

    public bool HasCompleted(string chapterId) => completedChapterIds.Contains(chapterId);

    /// <summary>Records a finished chapter and unlocks what it grants. Returns false if it was already completed.</summary>
    public bool Complete(Chapter chapter)
    {
        ArgumentNullException.ThrowIfNull(chapter);

        if (HasCompleted(chapter.Id))
            return false;

        completedChapterIds.Add(chapter.Id);
        Grant(chapter);
        return true;
    }

    /// <summary>Unlocks what a chapter grants without recording it; used to rebuild features from a save.</summary>
    internal void Grant(Chapter chapter)
    {
        foreach (var feature in chapter.Grants)
            unlockedFeatures.Add(feature);
    }

    /// <summary>Restores a completed chapter id from a save. Features are rebuilt from the journey afterwards.</summary>
    internal void RestoreCompleted(string chapterId)
    {
        if (!HasCompleted(chapterId))
            completedChapterIds.Add(chapterId);
    }
}
