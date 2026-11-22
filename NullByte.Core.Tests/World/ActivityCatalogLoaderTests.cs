using NullByte.Core.Domain;
using NullByte.Core.Domain.Activities;
using NullByte.Core.World;
using Xunit;

namespace NullByte.Core.Tests.World;

public sealed class ActivityCatalogLoaderTests
{
    private readonly ActivityCatalogLoader loader = new();

    [Fact]
    public void Default_catalog_loads_and_can_be_looked_up_ignoring_case()
    {
        var catalog = ActivityCatalogFactory.CreateDefault();

        Assert.NotEmpty(catalog.Facilities);
        Assert.NotEmpty(catalog.Companies);
        Assert.True(catalog.TryGetCourse("ALGORITHMS", out var course));
        Assert.Equal(FacilityKind.University, course.Kind);
        Assert.True(catalog.TryGetCrime("shoplift", out _));
        Assert.False(catalog.TryGetCrime("no-such-crime", out _));
    }

    [Fact]
    public void Default_catalog_has_a_course_for_every_combat_skill()
    {
        var catalog = ActivityCatalogFactory.CreateDefault();

        foreach (var name in new[] { "strength", "defense", "dexterity", "agility" })
        {
            Assert.True(catalog.TryGetCourse(name, out var course));
            Assert.Equal(FacilityKind.Gym, course.Kind);
        }
    }

    [Fact]
    public void A_valid_definition_is_converted()
    {
        var catalog = loader.Load(Valid());

        var rank = catalog.Companies.Single().Careers.Single().Ranks[1];
        Assert.Equal(4, rank.SalaryPerSecond);
        Assert.Equal(10, rank.RequiredLevels[SkillKind.Hacking]);
        Assert.Equal(1.5, catalog.Courses.Single().ExperiencePerSecond[SkillKind.Hacking]);
    }

    public static TheoryData<string, Func<ActivityCatalogDefinition, ActivityCatalogDefinition>, string> BadData => new()
    {
        { "unknown skill", d => d with { Courses = [Course() with { ExperiencePerSecond = Amounts(("hackin", 1)) }] }, "Unknown skill 'hackin'" },
        { "course gives nothing", d => d with { Courses = [Course() with { ExperiencePerSecond = Amounts(("hacking", 0)) }] }, "at least one skill" },
        { "unknown kind", d => d with { Facilities = [Facility() with { Kind = "spa" }] }, "Unknown facility kind" },
        { "course without facility", d => d with { Courses = [Course() with { Kind = "gym" }] }, "needs a gym" },
        { "name with space", d => d with { Crimes = [Crime() with { Name = "Bad Name" }] }, "lowercase" },
        { "duplicate crime", d => d with { Crimes = [Crime(), Crime()] }, "used more than once" },
        { "zero duration", d => d with { Crimes = [Crime() with { DurationSeconds = 0 }] }, "durationSeconds" },
        { "negative money", d => d with { Crimes = [Crime() with { Money = -1 }] }, "money" },
        { "no success weights", d => d with { Crimes = [Crime() with { SuccessWeights = Amounts() }] }, "at least one skill" },
        { "no ranks", d => d with { Companies = [Company() with { Careers = [new CareerDefinition { Name = "software", Ranks = [] }] }] }, "at least one rank" },
        { "salary drops", d => d with { Companies = [Company(Rank("a", 5, 0, 1), Rank("b", 4, 0, 1))] }, "pays less" },
        { "reputation drops", d => d with { Companies = [Company(Rank("a", 1, 100, 1), Rank("b", 2, 50, 1))] }, "less reputation" },
        { "level drops", d => d with { Companies = [Company(Rank("a", 1, 0, 10), Rank("b", 2, 0, 5))] }, "lower hacking level" },
    };

    [Theory]
    [MemberData(nameof(BadData))]
    public void Invalid_data_is_rejected_with_a_helpful_message(
        string _,
        Func<ActivityCatalogDefinition, ActivityCatalogDefinition> break_,
        string expected)
    {
        var ex = Assert.Throws<InvalidDataException>(() => loader.Load(break_(Valid())));

        Assert.Contains(expected, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("{ nope")]
    [InlineData("{}")]
    public void Malformed_json_is_rejected(string json)
    {
        Assert.Throws<InvalidDataException>(() => loader.Load(json));
    }

    private static ActivityCatalogDefinition Valid() => new()
    {
        Facilities = [Facility()],
        Courses = [Course()],
        Companies = [Company(Rank("intern", 2, 0, 1), Rank("developer", 4, 100, 10))],
        Crimes = [Crime()]
    };

    private static IReadOnlyDictionary<string, double> Amounts(params (string Skill, double Amount)[] amounts)
        => amounts.ToDictionary(a => a.Skill, a => a.Amount);

    private static FacilityDefinition Facility() => new()
    {
        Name = "rothman-university",
        Kind = "university",
        ExperienceMultiplier = 1,
        CostMultiplier = 1
    };

    private static CourseDefinition Course() => new()
    {
        Name = "algorithms",
        Kind = "university",
        CostPerSecond = 10,
        ExperiencePerSecond = Amounts(("hacking", 1.5))
    };

    private static CrimeDefinition Crime() => new()
    {
        Name = "shoplift",
        DurationSeconds = 2,
        Money = 300,
        Difficulty = 0.05,
        Experience = Amounts(("dexterity", 2)),
        SuccessWeights = Amounts(("dexterity", 1))
    };

    private static JobRankDefinition Rank(string title, double salary, double reputation, int hackingLevel) => new()
    {
        Title = title,
        SalaryPerSecond = salary,
        ReputationPerSecond = 0.1,
        RequiredReputation = reputation,
        RequiredLevels = new Dictionary<string, int> { ["hacking"] = hackingLevel },
        ExperiencePerSecond = Amounts(("hacking", 0.2))
    };

    private static CompanyDefinition Company(params JobRankDefinition[] ranks) => new()
    {
        Name = "pixelworks",
        Careers = [new CareerDefinition { Name = "software", Ranks = ranks }]
    };
}