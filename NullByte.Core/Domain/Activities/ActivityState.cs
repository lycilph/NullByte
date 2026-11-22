namespace NullByte.Core.Domain.Activities;

/// <summary>
/// What the player is busy with right now.
/// </summary>
/// <remarks>
/// Only <c>ActivityService</c> changes it, so starting and ending an activity
/// always goes through one place.
/// </remarks>
public sealed class ActivityState
{
    /// <summary>Gets the current activity, or null if the player is idle.</summary>
    public Activity? Current { get; internal set; }
}