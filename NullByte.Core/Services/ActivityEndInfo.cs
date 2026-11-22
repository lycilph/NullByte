using NullByte.Core.Domain.Activities;

namespace NullByte.Core.Services;

/// <summary>
/// Describes an activity that has just ended.
/// </summary>
/// <param name="Activity">The activity, with everything it earned.</param>
/// <param name="Reason">Why it ended.</param>
public sealed record ActivityEndInfo(Activity Activity, ActivityEndReason Reason);