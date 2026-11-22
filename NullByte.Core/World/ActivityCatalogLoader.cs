using NullByte.Core.Domain;
using NullByte.Core.Domain.Activities;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NullByte.Core.World;

/// <summary>
/// Loads and validates the activity catalog from its serialized world definition.
/// </summary>
/// <remarks>
/// Every problem is reported as an <see cref="InvalidDataException"/> that says
/// which entry is wrong, so a typo in the data file is easy to find.
/// </remarks>
public sealed partial class ActivityCatalogLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Names are typed as command arguments, so they are lowercase slugs, like hostnames.
    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex NamePattern();

    /// <summary>Loads a catalog from JSON.</summary>
    public ActivityCatalog Load(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("Activity catalog JSON cannot be empty.", nameof(json));

        ActivityCatalogDefinition? definition;
        try
        {
            definition = JsonSerializer.Deserialize<ActivityCatalogDefinition>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Activity catalog JSON is invalid: {ex.Message}", ex);
        }

        if (definition is null)
            throw new InvalidDataException("Activity catalog JSON did not contain a valid catalog definition.");

        return Load(definition);
    }

    /// <summary>Builds a catalog from an already deserialized definition.</summary>
    public ActivityCatalog Load(ActivityCatalogDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var facilities = definition.Facilities.Select(CreateFacility).ToList();
        RequireUniqueNames(facilities.Select(f => f.Name), "facility");

        var courses = definition.Courses.Select(CreateCourse).ToList();
        RequireUniqueNames(courses.Select(c => c.Name), "course");

        foreach (var course in courses)
        {
            if (!facilities.Any(f => f.Kind == course.Kind))
                throw Invalid($"Course '{course.Name}' needs a {course.Kind.ToString().ToLowerInvariant()}, but none is defined.");
        }

        var companies = definition.Companies.Select(CreateCompany).ToList();
        RequireUniqueNames(companies.Select(c => c.Name), "company");

        var crimes = definition.Crimes.Select(CreateCrime).ToList();
        RequireUniqueNames(crimes.Select(c => c.Name), "crime");

        return new ActivityCatalog(facilities, courses, companies, crimes);
    }

    private static TrainingFacility CreateFacility(FacilityDefinition definition)
    {
        var name = RequireName(definition.Name, "facility");
        var kind = ParseKind(definition.Kind, $"facility '{name}'");

        RequirePositive(definition.ExperienceMultiplier, $"facility '{name}' experienceMultiplier");
        RequirePositive(definition.CostMultiplier, $"facility '{name}' costMultiplier");

        return new TrainingFacility(name, kind, definition.ExperienceMultiplier, definition.CostMultiplier);
    }

    private static Course CreateCourse(CourseDefinition definition)
    {
        var name = RequireName(definition.Name, "course");
        var kind = ParseKind(definition.Kind, $"course '{name}'");

        RequireNonNegative(definition.CostPerSecond, $"course '{name}' costPerSecond");

        var experience = ParseAmounts(definition.ExperiencePerSecond, $"course '{name}' experiencePerSecond");
        if (!experience.Values.Any(v => v > 0))
            throw Invalid($"Course '{name}' must give experience in at least one skill.");

        return new Course(name, kind, definition.CostPerSecond, experience);
    }

    private static Company CreateCompany(CompanyDefinition definition)
    {
        var name = RequireName(definition.Name, "company");

        if (definition.Careers.Count == 0)
            throw Invalid($"Company '{name}' must have at least one career path.");

        var careers = definition.Careers.Select(c => CreateCareer(name, c)).ToList();
        RequireUniqueNames(careers.Select(c => c.Name), $"career path of company '{name}'");

        return new Company(name, careers);
    }

    private static CareerPath CreateCareer(string company, CareerDefinition definition)
    {
        var name = RequireName(definition.Name, $"career path of company '{company}'");
        var context = $"career '{name}' of company '{company}'";

        if (definition.Ranks.Count == 0)
            throw Invalid($"The {context} must have at least one rank.");

        var ranks = definition.Ranks.Select(r => CreateRank(context, r)).ToList();

        for (var i = 1; i < ranks.Count; i++)
            RequireNotEasier(context, ranks[i - 1], ranks[i]);

        return new CareerPath(name, ranks);
    }

    private static JobRank CreateRank(string careerContext, JobRankDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.Title))
            throw Invalid($"A rank in the {careerContext} has no title.");

        var context = $"rank '{definition.Title}' of the {careerContext}";

        RequireNonNegative(definition.SalaryPerSecond, $"{context} salaryPerSecond");
        RequireNonNegative(definition.ReputationPerSecond, $"{context} reputationPerSecond");
        RequireNonNegative(definition.RequiredReputation, $"{context} requiredReputation");

        var levels = new Dictionary<SkillKind, int>();
        foreach (var (skillName, level) in definition.RequiredLevels ?? new Dictionary<string, int>())
        {
            if (level < 1)
                throw Invalid($"{context} requiredLevels '{skillName}' must be at least 1.");

            levels[ParseSkill(skillName, context)] = level;
        }

        var experience = ParseAmounts(definition.ExperiencePerSecond, $"{context} experiencePerSecond");

        return new JobRank(
            definition.Title,
            definition.SalaryPerSecond,
            definition.ReputationPerSecond,
            definition.RequiredReputation,
            levels,
            experience);
    }

    // A career is a ladder: climbing must never get easier or pay less.
    private static void RequireNotEasier(string careerContext, JobRank lower, JobRank higher)
    {
        var context = $"rank '{higher.Title}' of the {careerContext}";

        if (higher.SalaryPerSecond < lower.SalaryPerSecond)
            throw Invalid($"{context} pays less than the rank below it.");

        if (higher.RequiredReputation < lower.RequiredReputation)
            throw Invalid($"{context} needs less reputation than the rank below it.");

        foreach (var (skill, level) in lower.RequiredLevels)
        {
            if (!higher.RequiredLevels.TryGetValue(skill, out var higherLevel) || higherLevel < level)
                throw Invalid($"{context} needs a lower {skill} level than the rank below it.");
        }
    }

    private static Crime CreateCrime(CrimeDefinition definition)
    {
        var name = RequireName(definition.Name, "crime");
        var context = $"crime '{name}'";

        RequirePositive(definition.DurationSeconds, $"{context} durationSeconds");
        RequireNonNegative(definition.Money, $"{context} money");
        RequirePositive(definition.Difficulty, $"{context} difficulty");

        var experience = ParseAmounts(definition.Experience, $"{context} experience");
        var weights = ParseAmounts(definition.SuccessWeights, $"{context} successWeights");

        if (!weights.Values.Any(v => v > 0))
            throw Invalid($"The {context} must weigh at least one skill in successWeights.");

        return new Crime(name, definition.DurationSeconds, definition.Money, definition.Difficulty, experience, weights);
    }

    private static Dictionary<SkillKind, double> ParseAmounts(IReadOnlyDictionary<string, double> amounts, string context)
    {
        var result = new Dictionary<SkillKind, double>();

        foreach (var (skillName, amount) in amounts)
        {
            RequireNonNegative(amount, $"{context} '{skillName}'");
            result[ParseSkill(skillName, context)] = amount;
        }

        return result;
    }

    private static SkillKind ParseSkill(string name, string context)
    {
        foreach (var kind in Enum.GetValues<SkillKind>())
        {
            if (string.Equals(kind.ToString(), name, StringComparison.OrdinalIgnoreCase))
                return kind;
        }

        throw Invalid($"Unknown skill '{name}' in {context}.");
    }

    private static FacilityKind ParseKind(string value, string context)
    {
        foreach (var kind in Enum.GetValues<FacilityKind>())
        {
            if (string.Equals(kind.ToString(), value, StringComparison.OrdinalIgnoreCase))
                return kind;
        }

        throw Invalid($"Unknown facility kind '{value}' for {context}.");
    }

    private static string RequireName(string? name, string what)
    {
        if (string.IsNullOrWhiteSpace(name) || !NamePattern().IsMatch(name))
            throw Invalid($"The {what} name '{name}' must be lowercase letters, digits and hyphens, e.g. 'my-name'.");

        return name;
    }

    private static void RequireUniqueNames(IEnumerable<string> names, string what)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in names)
        {
            if (!seen.Add(name))
                throw Invalid($"The {what} name '{name}' is used more than once.");
        }
    }

    private static void RequirePositive(double value, string what)
    {
        if (!double.IsFinite(value) || value <= 0)
            throw Invalid($"{Capitalize(what)} must be a positive number.");
    }

    private static void RequireNonNegative(double value, string what)
    {
        if (!double.IsFinite(value) || value < 0)
            throw Invalid($"{Capitalize(what)} must be a non-negative number.");
    }

    private static string Capitalize(string text) => char.ToUpperInvariant(text[0]) + text[1..];

    private static InvalidDataException Invalid(string message) => new(message);
}