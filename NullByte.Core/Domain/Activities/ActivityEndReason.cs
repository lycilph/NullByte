namespace NullByte.Core.Domain.Activities;

/// <summary>
/// Why an activity ended.
/// </summary>
public enum ActivityEndReason
{
    /// <summary>The player stopped it.</summary>
    Stopped,

    /// <summary>The player could no longer pay for it (for example tuition).</summary>
    CannotAfford
}