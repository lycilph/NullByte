using NullByte.Core.Domain.Activities;

namespace NullByte.Core.Services;

/// <summary>
/// Describes a promotion the player has just earned.
/// </summary>
/// <param name="Job">The job, which already holds its new rank.</param>
public sealed record JobPromotion(JobActivity Job);