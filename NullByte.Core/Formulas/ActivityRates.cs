using NullByte.Core.Domain;

namespace NullByte.Core.Formulas;

/// <summary>
/// What an activity gives (and takes) every second, as it stands right now.
/// </summary>
/// <param name="MoneyPerSecond">Money per second. Negative when the activity costs money.</param>
/// <param name="ExperiencePerSecond">Experience per second, per skill.</param>
/// <param name="ReputationPerSecond">Reputation per second (with the company, for a job).</param>
public sealed record ActivityRates(
    double MoneyPerSecond,
    IReadOnlyDictionary<SkillKind, double> ExperiencePerSecond,
    double ReputationPerSecond = 0);