namespace NullByte.Core.Domain;

/// <summary>
/// One in-flight job on a mark (hack, grow, or scrub). Money is NOT frozen
/// here anymore — steals are computed at completion from the target's live
/// pool, so parallel jobs can't over-steal (Phase 4.5). Serialized with the save.
/// </summary>
public sealed class ActiveJob
{
    public string TargetId { get; init; } = "";
    public string TargetName { get; init; } = "";
    public JobKind Kind { get; init; }
    public int TicksRemaining { get; set; }
    public int TotalTicks { get; init; }
    public double XpReward { get; init; } // hack & grow pay XP; scrub pays none
}
