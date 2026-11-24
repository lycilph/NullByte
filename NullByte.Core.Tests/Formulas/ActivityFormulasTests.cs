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
    private static readonly Crime CoinFlip = new("coin-flip", 4, 500, 0.02,
    new Dictionary<SkillKind, double> { [SkillKind.Dexterity] = 8 },
    new Dictionary<SkillKind, double> { [SkillKind.Dexterity] = 1 });

    private static readonly Crime Mixed = new("mixed", 4, 200, 0.05,
        new Dictionary<SkillKind, double> { [SkillKind.Strength] = 1 },
        new Dictionary<SkillKind, double> { [SkillKind.Strength] = 3, [SkillKind.Agility] = 1 });

    private static PlayerSkills SkillsAt(SkillKind skill, int level)
    {
        var skills = new PlayerSkills();
        skills.AddExperience(skill, new SkillFormulas(new BalanceConfig()).CalculateExperienceForLevel(skill, level));
        return skills;
    }

    [Fact]
    public void The_chance_of_a_crime_is_the_skill_level_against_the_difficulty()
    {
        var formulas = new ActivityFormulas(new BalanceConfig());

        Assert.Equal(0.5, formulas.CalculateCrimeSuccessChance(CoinFlip, new PlayerSkills()), precision: 9);   // level 1 / (0.02 * 100)
        Assert.Equal(1.0, formulas.CalculateCrimeSuccessChance(CoinFlip, SkillsAt(SkillKind.Dexterity, 2)), precision: 9);
    }

    [Fact]
    public void The_chance_is_never_more_than_certain()
    {
        var formulas = new ActivityFormulas(new BalanceConfig());

        Assert.Equal(1.0, formulas.CalculateCrimeSuccessChance(CoinFlip, SkillsAt(SkillKind.Dexterity, 50)));
    }

    [Fact]
    public void The_skills_of_a_crime_count_in_proportion_to_their_weights()
    {
        var formulas = new ActivityFormulas(new BalanceConfig());
        var skills = SkillsAt(SkillKind.Strength, 3);                  // strength 3, agility 1

        // (3 * 3 + 1 * 1) / 4 = 2.5, against 0.05 * 100 = 5
        Assert.Equal(0.5, formulas.CalculateCrimeSuccessChance(Mixed, skills), precision: 9);
    }

    [Fact]
    public void A_skill_that_a_crime_does_not_weigh_makes_no_difference()
    {
        var formulas = new ActivityFormulas(new BalanceConfig());

        Assert.Equal(
            formulas.CalculateCrimeSuccessChance(CoinFlip, new PlayerSkills()),
            formulas.CalculateCrimeSuccessChance(CoinFlip, SkillsAt(SkillKind.Hacking, 40)));
    }

    [Fact]
    public void The_skill_scale_is_used()
    {
        var formulas = new ActivityFormulas(new BalanceConfig { CrimeSkillScale = 200 });

        Assert.Equal(0.25, formulas.CalculateCrimeSuccessChance(CoinFlip, new PlayerSkills()), precision: 9);
    }

    [Fact]
    public void A_successful_attempt_gives_all_the_money_and_experience()
    {
        var rewards = new ActivityFormulas(new BalanceConfig()).CalculateCrimeRewards(CoinFlip, ActivityMode.Focused, succeeded: true);

        Assert.Equal(500, rewards.Money);
        Assert.Equal(8, rewards.Experience[SkillKind.Dexterity]);
    }

    [Fact]
    public void A_failed_attempt_gives_no_money_and_a_quarter_of_the_experience()
    {
        var rewards = new ActivityFormulas(new BalanceConfig()).CalculateCrimeRewards(CoinFlip, ActivityMode.Focused, succeeded: false);

        Assert.Equal(0, rewards.Money);
        Assert.Equal(2, rewards.Experience[SkillKind.Dexterity], precision: 9);
    }

    [Fact]
    public void Not_being_focused_reduces_the_rewards_but_not_the_chance()
    {
        var formulas = new ActivityFormulas(new BalanceConfig());

        var success = formulas.CalculateCrimeRewards(CoinFlip, ActivityMode.Background, succeeded: true);
        var failure = formulas.CalculateCrimeRewards(CoinFlip, ActivityMode.Background, succeeded: false);

        Assert.Equal(400, success.Money, precision: 9);
        Assert.Equal(6.4, success.Experience[SkillKind.Dexterity], precision: 9);
        Assert.Equal(1.6, failure.Experience[SkillKind.Dexterity], precision: 9);       // 8 * 0.25 * 0.8
    }

    [Fact]
    public void Crime_balance_multipliers_and_the_failure_factor_are_used()
    {
        var balance = new BalanceConfig { CrimeMoneyMultiplier = 2, CrimeExperienceMultiplier = 3, CrimeFailureExperienceFactor = 0.5 };
        var formulas = new ActivityFormulas(balance);

        var success = formulas.CalculateCrimeRewards(CoinFlip, ActivityMode.Focused, succeeded: true);
        var failure = formulas.CalculateCrimeRewards(CoinFlip, ActivityMode.Focused, succeeded: false);

        Assert.Equal(1000, success.Money);
        Assert.Equal(24, success.Experience[SkillKind.Dexterity]);
        Assert.Equal(12, failure.Experience[SkillKind.Dexterity]);
    }

    [Fact]
    public void A_crime_has_no_constant_rates()
    {
        var formulas = new ActivityFormulas(new BalanceConfig());

        Assert.Throws<NotSupportedException>(() => formulas.CalculateRates(new CrimeActivity(CoinFlip, repeat: true, ActivityMode.Focused)));
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