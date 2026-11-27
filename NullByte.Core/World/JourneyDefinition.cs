using NullByte.Core.Domain.Progression;

namespace NullByte.Core.World;

/// <summary>The story as loaded from world data, checked by <see cref="JourneyLoader"/>.</summary>
public sealed record JourneyDefinition
{
    public required IReadOnlyList<ChapterDefinition> Chapters { get; init; }
}

public sealed record ChapterDefinition
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string Hint { get; init; } = "";
    public required IReadOnlyList<string> Story { get; init; }
    public IReadOnlyList<ConditionDefinition> Conditions { get; init; } = [];
    public IReadOnlyList<GameFeature> Grants { get; init; } = [];
}

public sealed record ConditionDefinition
{
    public required ConditionKind Kind { get; init; }
    public double Amount { get; init; }
    public string? Hostname { get; init; }
}
