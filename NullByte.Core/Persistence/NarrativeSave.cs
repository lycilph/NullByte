using NullByte.Core.Domain;

namespace NullByte.Core.Persistence;

public sealed record NarrativeSave
{
    public required NarrativeProgressionMode Mode { get; init; }
    public required IReadOnlyList<string> UnlockedCapabilities { get; init; }
    public required IReadOnlyList<string> CompletedMilestones { get; init; }
    public required IReadOnlyList<string> DeliveredMessages { get; init; }
    public required IReadOnlyList<string> DiscoveredDiscoveries { get; init; }
}
