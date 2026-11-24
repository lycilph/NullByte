namespace NullByte.Core.Domain.Activities;

/// <summary>
/// Why an activity ended.
/// </summary>
public enum ActivityEndReason
{
    /// <summary>The player stopped it.</summary>
    Stopped,
    /// <summary>The player could no longer pay for it (for example tuition).</summary>
    CannotAfford,
    /// <summary>It was meant to happen once, and has (a crime that is not repeated).</summary>
    Completed
}