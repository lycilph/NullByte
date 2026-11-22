namespace NullByte.Core.World;

/// <summary>
/// Describes the activity catalog as loaded from world data. Skills are
/// written by name (<c>"hacking"</c>, <c>"charisma"</c>, ...) and are
/// checked by <see cref="ActivityCatalogLoader"/>.
/// </summary>
public sealed record ActivityCatalogDefinition
{
    public required IReadOnlyList<FacilityDefinition> Facilities { get; init; }
    public required IReadOnlyList<CourseDefinition> Courses { get; init; }
    public required IReadOnlyList<CompanyDefinition> Companies { get; init; }
    public required IReadOnlyList<CrimeDefinition> Crimes { get; init; }
}

public sealed record FacilityDefinition
{
    public required string Name { get; init; }
    public required string Kind { get; init; }
    public required double ExperienceMultiplier { get; init; }
    public required double CostMultiplier { get; init; }
}

public sealed record CourseDefinition
{
    public required string Name { get; init; }
    public required string Kind { get; init; }
    public required double CostPerSecond { get; init; }
    public required IReadOnlyDictionary<string, double> ExperiencePerSecond { get; init; }
}

public sealed record CompanyDefinition
{
    public required string Name { get; init; }
    public required IReadOnlyList<CareerDefinition> Careers { get; init; }
}

public sealed record CareerDefinition
{
    public required string Name { get; init; }
    public required IReadOnlyList<JobRankDefinition> Ranks { get; init; }
}

public sealed record JobRankDefinition
{
    public required string Title { get; init; }
    public required double SalaryPerSecond { get; init; }
    public required double ReputationPerSecond { get; init; }
    public double RequiredReputation { get; init; }
    public IReadOnlyDictionary<string, int>? RequiredLevels { get; init; }
    public required IReadOnlyDictionary<string, double> ExperiencePerSecond { get; init; }
}

public sealed record CrimeDefinition
{
    public required string Name { get; init; }
    public required double DurationSeconds { get; init; }
    public required double Money { get; init; }
    public required double Difficulty { get; init; }
    public required IReadOnlyDictionary<string, double> Experience { get; init; }
    public required IReadOnlyDictionary<string, double> SuccessWeights { get; init; }
}