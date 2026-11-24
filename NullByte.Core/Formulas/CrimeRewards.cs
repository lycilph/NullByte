using NullByte.Core.Domain;

namespace NullByte.Core.Formulas;

/// <summary>
/// What one attempt at a crime gives.
/// </summary>
/// <param name="Money">Money earned. Zero for a failed attempt.</param>
/// <param name="Experience">Experience gained, per skill.</param>
public sealed record CrimeRewards(double Money, IReadOnlyDictionary<SkillKind, double> Experience);