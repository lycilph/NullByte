using NullByte.Core.Configuration;
using NullByte.Core.Domain;
using NullByte.Core.Domain.Activities;
using NullByte.Core.Formulas;

namespace NullByte.Core.Tests.Formulas;

public sealed class ActivityFormulasTests
{
    private static readonly Course Algorithms = new("algorithms", FacilityKind.University, 10,
        new Dictionary<SkillKind, double> { [SkillKind.Hacking] = 2 });

    private static readonly TrainingFacility Plain = new("plain-university", FacilityKind.University, 1, 1);
    private static readonly TrainingFacility Fancy = new("fancy-university", FacilityKind.University, 2, 3);
    private static JobRank Intern(double salary = 4, double reputation = 1, double requiredReputation = 0, int hackingLevel = 0)
        => new("Intern", salary, reputation, requiredReputation,
        hackingLevel > 0 ? new Dictionary<SkillKind, int> { [SkillKind.Hacking] = hackingLevel } : new Dictionary<SkillKind, int>(),
        new Dictionary<SkillKind, double> { [SkillKind.Hacking] = 0.5 });

    private static PlayerSkills SkillsAtHackingLevel(int level)
    {
        var skills = new PlayerSkills();
        skills.AddExperience(SkillKind.Hacking, new SkillFormulas(new BalanceConfig()).CalculateExperienceForLevel(SkillKind.Hacking, level));
        return skills;
    }

    [Fact]
    public void A_focused_job_pays_a_salary_and_gives_experience_and_reputation()
    {
        var rates = new ActivityFormulas(new BalanceConfig()).CalculateJobRates(Intern(), ActivityMode.Focused);

        Assert.Equal(4, rates.MoneyPerSecond);
        Assert.Equal(0.5, rates.ExperiencePerSecond[SkillKind.Hacking]);
        Assert.Equal(1, rates.ReputationPerSecond);
    }

    [Fact]
    public void A_background_job_reduces_every_reward()
    {
        var rates = new ActivityFormulas(new BalanceConfig()).CalculateJobRates(Intern(), ActivityMode.Background);

        Assert.Equal(3.2, rates.MoneyPerSecond, precision: 9);
        Assert.Equal(0.4, rates.ExperiencePerSecond[SkillKind.Hacking], precision: 9);
        Assert.Equal(0.8, rates.ReputationPerSecond, precision: 9);
    }

    [Fact]
    public void Job_balance_multipliers_are_used()
    {
        var balance = new BalanceConfig { JobSalaryMultiplier = 2, JobExperienceMultiplier = 3, JobReputationMultiplier = 4 };

        var rates = new ActivityFormulas(balance).CalculateJobRates(Intern(), ActivityMode.Focused);

        Assert.Equal(8, rates.MoneyPerSecond);
        Assert.Equal(1.5, rates.ExperiencePerSecond[SkillKind.Hacking]);
        Assert.Equal(4, rates.ReputationPerSecond);
    }

    [Fact]
    public void The_rates_of_a_job_activity_follow_its_rank()
    {
        var career = new CareerPath("software", [Intern(salary: 4), Intern(salary: 10)]);
        var company = new Company("testcorp", [career]);
        var job = new JobActivity(company, career, rankIndex: 0, ActivityMode.Focused);
        var formulas = new ActivityFormulas(new BalanceConfig());

        Assert.Equal(4, formulas.CalculateRates(job).MoneyPerSecond);

        job.RankIndex = 1;

        Assert.Equal(10, formulas.CalculateRates(job).MoneyPerSecond);
    }

    [Fact]
    public void Requirements_need_both_the_skill_level_and_the_reputation()
    {
        var formulas = new ActivityFormulas(new BalanceConfig());
        var rank = Intern(requiredReputation: 10, hackingLevel: 3);

        Assert.True(formulas.MeetsRequirements(rank, SkillsAtHackingLevel(3), reputation: 10));
        Assert.False(formulas.MeetsRequirements(rank, SkillsAtHackingLevel(3), reputation: 9.9));
        Assert.False(formulas.MeetsRequirements(rank, SkillsAtHackingLevel(2), reputation: 10));
        Assert.True(formulas.MeetsRequirements(Intern(), new PlayerSkills(), reputation: 0));
    }

    [Fact]
    public void Unmet_requirements_say_what_is_missing()
    {
        var formulas = new ActivityFormulas(new BalanceConfig());
        var rank = Intern(requiredReputation: 10, hackingLevel: 3);

        var unmet = formulas.GetUnmetRequirements(rank, new PlayerSkills(), reputation: 4);

        Assert.Equal(2, unmet.Count);
        Assert.Equal(new UnmetRequirement("hacking level", 3, 1), unmet[0]);
        Assert.Equal(new UnmetRequirement("reputation", 10, 4), unmet[1]);
        Assert.Empty(formulas.GetUnmetRequirements(rank, SkillsAtHackingLevel(3), reputation: 10));
    }

    [Fact]
    public void The_highest_qualifying_rank_is_found()
    {
        var formulas = new ActivityFormulas(new BalanceConfig());
        var career = new CareerPath("software",
        [
            Intern(hackingLevel: 3),
            Intern(requiredReputation: 10, hackingLevel: 3),
            Intern(requiredReputation: 100, hackingLevel: 10)
        ]);

        Assert.Equal(-1, formulas.FindHighestQualifyingRank(career, new PlayerSkills(), 0));
        Assert.Equal(0, formulas.FindHighestQualifyingRank(career, SkillsAtHackingLevel(3), 0));
        Assert.Equal(1, formulas.FindHighestQualifyingRank(career, SkillsAtHackingLevel(3), 50));
        Assert.Equal(1, formulas.FindHighestQualifyingRank(career, SkillsAtHackingLevel(9), 500));
        Assert.Equal(2, formulas.FindHighestQualifyingRank(career, SkillsAtHackingLevel(10), 100));
    }

    [Fact]
    public void Focused_class_gives_full_experience_and_costs_money()
    {
        var rates = new ActivityFormulas(new BalanceConfig()).CalculateClassRates(Algorithms, Plain, ActivityMode.Focused);

        Assert.Equal(-10, rates.MoneyPerSecond);
        Assert.Equal(2, rates.ExperiencePerSecond[SkillKind.Hacking]);
    }

    [Fact]
    public void Background_class_gives_less_experience_but_costs_the_same()
    {
        var rates = new ActivityFormulas(new BalanceConfig()).CalculateClassRates(Algorithms, Plain, ActivityMode.Background);

        Assert.Equal(-10, rates.MoneyPerSecond);
        Assert.Equal(1.6, rates.ExperiencePerSecond[SkillKind.Hacking], precision: 9);
    }

    [Fact]
    public void Facility_multipliers_scale_cost_and_experience()
    {
        var rates = new ActivityFormulas(new BalanceConfig()).CalculateClassRates(Algorithms, Fancy, ActivityMode.Focused);

        Assert.Equal(-30, rates.MoneyPerSecond);
        Assert.Equal(4, rates.ExperiencePerSecond[SkillKind.Hacking]);
    }

    [Fact]
    public void Balance_multipliers_and_the_unfocused_factor_are_used()
    {
        var balance = new BalanceConfig { ClassCostMultiplier = 2, ClassExperienceMultiplier = 3, UnfocusedRewardFactor = 0.5 };

        var rates = new ActivityFormulas(balance).CalculateClassRates(Algorithms, Plain, ActivityMode.Background);

        Assert.Equal(-20, rates.MoneyPerSecond);
        Assert.Equal(3, rates.ExperiencePerSecond[SkillKind.Hacking], precision: 9);
    }

    [Fact]
    public void Rates_of_an_activity_follow_its_current_mode()
    {
        var formulas = new ActivityFormulas(new BalanceConfig());
        var activity = new ClassActivity(Algorithms, Plain, ActivityMode.Focused);

        Assert.Equal(2, formulas.CalculateRates(activity).ExperiencePerSecond[SkillKind.Hacking]);

        activity.Mode = ActivityMode.Background;

        Assert.Equal(1.6, formulas.CalculateRates(activity).ExperiencePerSecond[SkillKind.Hacking], precision: 9);
    }

    [Fact]
    public void Reward_factor_is_one_when_focused()
    {
        var formulas = new ActivityFormulas(new BalanceConfig());

        Assert.Equal(1.0, formulas.CalculateRewardFactor(ActivityMode.Focused));
        Assert.Equal(0.8, formulas.CalculateRewardFactor(ActivityMode.Background));
    }
}