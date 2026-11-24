namespace NullByte.Core.Domain.Activities;

/// <summary>
/// An immutable snapshot of what an activity has earned so far.
/// </summary>
/// <remarks>
/// A new snapshot is published on every update, so a reader on another thread
/// (such as the terminal drawing a live display) always sees a consistent one.
/// </remarks>
/// <param name="Elapsed">How long the activity has been running, in game time.</param>
/// <param name="NetMoney">Money gained so far, net of costs: negative if it has cost more than it paid.</param>
/// <param name="ExperienceGained">Experience gained so far, per skill.</param>
/// <param name="ReputationGained">Reputation gained so far (with the company, for a job).</param>
/// <param name="Attempts">Attempts made so far (crimes).</param>
/// <param name="Successes">Attempts that succeeded (crimes).</param>
public sealed record ActivityProgress(
    TimeSpan Elapsed,
    double NetMoney,
    IReadOnlyDictionary<SkillKind, double> ExperienceGained,
    double ReputationGained = 0,
    int Attempts = 0,
    int Successes = 0)
{
    public static ActivityProgress Empty { get; } = new(TimeSpan.Zero, 0, new Dictionary<SkillKind, double>());
}