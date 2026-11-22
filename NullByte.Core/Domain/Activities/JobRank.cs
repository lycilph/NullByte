namespace NullByte.Core.Domain;

/// <summary>
/// One position on a career path. All rates are per second of work.
/// </summary>
/// <param name="Title">Display title, e.g. <c>Senior Developer</c>.</param>
/// <param name="SalaryPerSecond">Money earned each second.</param>
/// <param name="ReputationPerSecond">Company reputation earned each second.</param>
/// <param name="RequiredReputation">Company reputation needed to hold this rank.</param>
/// <param name="RequiredLevels">Skill levels needed to hold this rank (skills not listed need none).</param>
/// <param name="ExperiencePerSecond">Experience gained each second, per skill.</param>
public sealed record JobRank(
    string Title,
    double SalaryPerSecond,
    double ReputationPerSecond,
    double RequiredReputation,
    IReadOnlyDictionary<SkillKind, int> RequiredLevels,
    IReadOnlyDictionary<SkillKind, double> ExperiencePerSecond);