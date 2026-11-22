namespace NullByte.Core.Domain.Activities;

/// <summary>
/// Something a player can learn at any facility of the matching kind.
/// All rates are base values, per second, before facility and balance multipliers.
/// </summary>
/// <param name="Name">Unique command-friendly name, e.g. <c>algorithms</c> or <c>strength</c>.</param>
/// <param name="Kind">The kind of facility that offers this course.</param>
/// <param name="CostPerSecond">Money charged each second.</param>
/// <param name="ExperiencePerSecond">Experience gained each second, per skill.</param>
public sealed record Course(
    string Name,
    FacilityKind Kind,
    double CostPerSecond,
    IReadOnlyDictionary<SkillKind, double> ExperiencePerSecond);