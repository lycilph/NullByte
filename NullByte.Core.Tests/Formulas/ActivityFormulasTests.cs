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