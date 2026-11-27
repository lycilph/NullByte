namespace NullByte.Core.Domain;

/// <summary>
/// Durable narrative progression state. Capability IDs and milestone IDs are stable content identifiers.
/// </summary>
public sealed class NarrativeState
{
    public NarrativeState(
        NarrativeProgressionMode mode,
        IEnumerable<string>? unlockedCapabilities = null,
        IEnumerable<string>? completedMilestones = null,
        IEnumerable<string>? deliveredMessages = null)
    {
        Mode = mode;
        UnlockedCapabilities = new HashSet<string>(unlockedCapabilities ?? [], StringComparer.Ordinal);
        CompletedMilestones = new HashSet<string>(completedMilestones ?? [], StringComparer.Ordinal);
        DeliveredMessages = new HashSet<string>(deliveredMessages ?? [], StringComparer.Ordinal);
    }

    public NarrativeProgressionMode Mode { get; set; }

    public HashSet<string> UnlockedCapabilities { get; }

    public HashSet<string> CompletedMilestones { get; }

    public HashSet<string> DeliveredMessages { get; }

    public bool IsFreeplay => Mode == NarrativeProgressionMode.Freeplay;

    public bool HasCapability(string capability)
        => UnlockedCapabilities.Contains(capability);

    public bool CompleteMilestone(string milestone)
        => CompletedMilestones.Add(milestone);

    public bool DeliverMessage(string message)
        => DeliveredMessages.Add(message);

    public void UnlockCapability(string capability)
        => UnlockedCapabilities.Add(capability);
}
