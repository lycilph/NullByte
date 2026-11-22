using NullByte.Core.Configuration;
using NullByte.Core.Domain;
using NullByte.Core.Domain.Activities;

namespace NullByte.Core.Formulas;

/// <summary>
/// Calculates what activities give and cost.
/// </summary>
/// <remarks>
/// This class holds no game state. Not being focused reduces what an activity
/// <em>gives</em> (experience, and later salary and reputation), never what it
/// <em>costs</em>.
/// </remarks>
public sealed class ActivityFormulas
{
    private readonly BalanceConfig balance;

    public ActivityFormulas(BalanceConfig balance)
    {
        this.balance = balance
            ?? throw new ArgumentNullException(nameof(balance));
    }

    /// <summary>Calculates the current rates of an activity.</summary>
    public ActivityRates CalculateRates(Activity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);

        return activity switch
        {
            ClassActivity classActivity => CalculateClassRates(classActivity.Course, classActivity.Facility, classActivity.Mode),
            _ => throw new NotSupportedException($"Unknown activity type {activity.GetType().Name}.")
        };
    }

    /// <summary>Calculates the rates of taking a course at a facility.</summary>
    public ActivityRates CalculateClassRates(Course course, TrainingFacility facility, ActivityMode mode)
    {
        ArgumentNullException.ThrowIfNull(course);
        ArgumentNullException.ThrowIfNull(facility);

        var cost = course.CostPerSecond * facility.CostMultiplier * balance.ClassCostMultiplier;

        var experienceFactor =
            facility.ExperienceMultiplier
            * balance.ClassExperienceMultiplier
            * CalculateRewardFactor(mode);

        var experience = course.ExperiencePerSecond.ToDictionary(
            skill => skill.Key,
            skill => skill.Value * experienceFactor);

        return new ActivityRates(-cost, experience);
    }

    /// <summary>Gets the share of its rewards an activity gives in a mode.</summary>
    public double CalculateRewardFactor(ActivityMode mode) => mode switch
    {
        ActivityMode.Focused => 1.0,
        ActivityMode.Background => balance.UnfocusedRewardFactor,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown activity mode.")
    };
}