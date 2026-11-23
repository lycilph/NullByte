using NullByte.ConsoleUI.Commands;
using NullByte.Core.Domain;
using NullByte.Core.Domain.Activities;
using NullByte.Core.Formulas;

namespace NullByte.ConsoleUI.Tests.Commands;

public sealed class ActivityFormatterTests : IDisposable
{
    private readonly InvariantCulture culture = new();

    public void Dispose() => culture.Dispose();

    [Fact]
    public void Reputation_is_shown_where_there_is_any()
    {
        var xp = new Dictionary<SkillKind, double> { [SkillKind.Hacking] = 0.5 };

        Assert.Equal("+31.00 reputation", ActivityFormatter.FormatReputation(31));
        Assert.Equal("+0.10 reputation/s", ActivityFormatter.FormatReputation(0.1, perSecond: true));
        Assert.Equal("pays $4.00/s, +0.50 hacking xp/s, +1.00 reputation/s", ActivityFormatter.FormatRates(new ActivityRates(4, xp, 1)));
        Assert.Equal("1m 05s | +$40.00 | +5.00 hacking xp | +10.00 reputation",
            ActivityFormatter.FormatProgress(new ActivityProgress(TimeSpan.FromSeconds(65), 40, xp.ToDictionary(e => e.Key, _ => 5.0), 10)));
    }

    [Fact]
    public void Requirements_list_the_levels_then_the_reputation()
    {
        var rank = new JobRank("Developer", 10, 2, 100,
            new Dictionary<SkillKind, int> { [SkillKind.Charisma] = 10, [SkillKind.Hacking] = 25 },
            new Dictionary<SkillKind, double>());

        Assert.Equal("hacking level 25, charisma level 10, reputation 100", ActivityFormatter.FormatRequirements(rank));
        Assert.Equal("no requirements",
            ActivityFormatter.FormatRequirements(new JobRank("Intern", 4, 1, 0, new Dictionary<SkillKind, int>(), new Dictionary<SkillKind, double>())));
    }

    [Fact]
    public void A_job_is_tagged_working_and_summarised_with_its_reputation()
    {
        var career = new CareerPath("software", [new JobRank("Intern", 4, 1, 0, new Dictionary<SkillKind, int>(), new Dictionary<SkillKind, double>())]);
        var job = new JobActivity(new Company("testcorp", [career]), career, 0, ActivityMode.Focused);
        job.RecordProgress(TimeSpan.FromSeconds(10), 40, new Dictionary<SkillKind, double> { [SkillKind.Hacking] = 5 }, 10);

        Assert.Equal("working", ActivityFormatter.PromptTag(job));
        Assert.Equal("Working at testcorp (software): 10s, +$40.00, +5.00 hacking xp, +10.00 reputation.", ActivityFormatter.FormatSummary(job));
    }

    [Theory]
    [InlineData(0, "0s")]
    [InlineData(7.9, "7s")]
    [InlineData(65, "1m 05s")]
    [InlineData(3723, "1h 02m 03s")]
    [InlineData(-5, "0s")]
    public void Durations_are_shown_in_whole_seconds(double seconds, string expected)
    {
        Assert.Equal(expected, ActivityFormatter.FormatDuration(TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void Money_is_shown_with_its_sign()
    {
        Assert.Equal("$1,234.50", ActivityFormatter.FormatMoney(1234.5));
        Assert.Equal("-$3.00", ActivityFormatter.FormatMoney(-3));
        Assert.Equal("+$12.00", ActivityFormatter.FormatSignedMoney(12));
        Assert.Equal("-$3.00", ActivityFormatter.FormatSignedMoney(-3));
        Assert.Equal("+$0.00", ActivityFormatter.FormatSignedMoney(0));
    }

    [Fact]
    public void Experience_lists_only_the_skills_that_gained_in_a_fixed_order()
    {
        var experience = new Dictionary<SkillKind, double>
        {
            [SkillKind.Charisma] = 5,
            [SkillKind.Hacking] = 166,
            [SkillKind.Strength] = 0
        };

        Assert.Equal("+166.00 hacking xp, +5.00 charisma xp", ActivityFormatter.FormatExperience(experience));
        Assert.Equal("+166.00 hacking xp/s, +5.00 charisma xp/s", ActivityFormatter.FormatExperience(experience, perSecond: true));
        Assert.Equal("no experience", ActivityFormatter.FormatExperience(new Dictionary<SkillKind, double>()));
    }

    [Fact]
    public void Rates_say_whether_the_activity_costs_pays_or_is_free()
    {
        var xp = new Dictionary<SkillKind, double> { [SkillKind.Hacking] = 2 };

        Assert.Equal("costs $40.00/s, +2.00 hacking xp/s", ActivityFormatter.FormatRates(new ActivityRates(-40, xp)));
        Assert.Equal("pays $5.00/s, +2.00 hacking xp/s", ActivityFormatter.FormatRates(new ActivityRates(5, xp)));
        Assert.Equal("free, +2.00 hacking xp/s", ActivityFormatter.FormatRates(new ActivityRates(0, xp)));
    }

    [Fact]
    public void Progress_and_summary_show_time_money_and_experience()
    {
        var progress = new ActivityProgress(
            TimeSpan.FromSeconds(83), -830, new Dictionary<SkillKind, double> { [SkillKind.Hacking] = 166 });

        Assert.Equal("1m 23s | -$830.00 | +166.00 hacking xp", ActivityFormatter.FormatProgress(progress));
    }

    [Fact]
    public void The_prompt_tag_depends_on_the_kind_of_facility()
    {
        var xp = new Dictionary<SkillKind, double> { [SkillKind.Hacking] = 1 };
        var university = new TrainingFacility("u", FacilityKind.University, 1, 1);
        var gym = new TrainingFacility("g", FacilityKind.Gym, 1, 1);

        Assert.Equal("studying", ActivityFormatter.PromptTag(new ClassActivity(new Course("c", FacilityKind.University, 0, xp), university, ActivityMode.Focused)));
        Assert.Equal("training", ActivityFormatter.PromptTag(new ClassActivity(new Course("c", FacilityKind.Gym, 0, xp), gym, ActivityMode.Focused)));
    }
}