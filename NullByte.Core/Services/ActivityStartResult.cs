using NullByte.Core.Domain.Activities;

namespace NullByte.Core.Services;

/// <summary>
/// Describes the outcome of trying to start an activity.
/// </summary>
/// <param name="Succeeded">Whether the activity was started.</param>
/// <param name="FailureMessage">Why it could not be started. Empty on success.</param>
/// <param name="Activity">The activity that was started, or null on failure.</param>
/// <param name="Notice">Something the player should be told about how it started, e.g. that they were hired. Often empty.</param>
public sealed record ActivityStartResult(bool Succeeded, string FailureMessage, Activity? Activity, string Notice = "")
{
    public static ActivityStartResult Failed(string message) => new(false, message, null);
}