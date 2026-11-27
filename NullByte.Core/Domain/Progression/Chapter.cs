namespace NullByte.Core.Domain.Progression;

/// <summary>What has to be true before a chapter is completed.</summary>
public enum ConditionKind
{
    /// <summary>At least <see cref="ChapterCondition.Amount"/> money.</summary>
    MoneyAtLeast,

    /// <summary>A hacking level of at least <see cref="ChapterCondition.Amount"/>.</summary>
    HackingLevelAtLeast,

    /// <summary>At least <see cref="ChapterCondition.Amount"/> servers discovered.</summary>
    ServersDiscoveredAtLeast,

    /// <summary>Root access on at least <see cref="ChapterCondition.Amount"/> servers. The home server and cloud servers do not count.</summary>
    ServersRootedAtLeast,

    /// <summary>A back door on at least <see cref="ChapterCondition.Amount"/> servers. The home server and cloud servers do not count.</summary>
    ServersBackdooredAtLeast,

    /// <summary>At least <see cref="ChapterCondition.Amount"/> botnet nodes.</summary>
    BotnetNodesAtLeast,

    /// <summary>The player is connected to the server named <see cref="ChapterCondition.Hostname"/>.</summary>
    AtServer,

    /// <summary>The player has root access on the server named <see cref="ChapterCondition.Hostname"/>.</summary>
    RootOnServer
}

/// <summary>One requirement of a chapter. Every condition is read from the game state, so nothing extra has to be saved.</summary>
public sealed record ChapterCondition(ConditionKind Kind, double Amount = 0, string? Hostname = null);

/// <summary>
/// One beat of the story. A chapter is completed when all its conditions hold; completing it
/// tells the story and unlocks its features.
/// </summary>
/// <param name="Id">A stable lowercase slug. Saves refer to chapters by id, so never rename one.</param>
/// <param name="Title">The chapter name shown in the journey log.</param>
/// <param name="Hint">An in-character nudge toward the conditions, shown while the chapter is the current goal.</param>
/// <param name="Story">The text told when the chapter completes, one entry per line.</param>
/// <param name="Conditions">All of these must hold. Empty means the chapter completes at once.</param>
/// <param name="Grants">The features this chapter unlocks.</param>
public sealed record Chapter(
    string Id,
    string Title,
    string Hint,
    IReadOnlyList<string> Story,
    IReadOnlyList<ChapterCondition> Conditions,
    IReadOnlyList<GameFeature> Grants);
