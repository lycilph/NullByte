using NullByte.Core.Configuration;
using NullByte.Core.Domain;
using NullByte.Core.Domain.Activities;

namespace NullByte.Core.Formulas;

/// <remarks>
/// This class holds no game state. Not being focused reduces what an activity
/// <em>gives</em> (experience, salary and reputation), never what it
/// <em>costs</em>.
/// </remarks>
public sealed class ActivityFormulas
{
    private readonly BalanceConfig balance;
    private readonly SkillFormulas skillFormulas;

    public ActivityFormulas(BalanceConfig balance)
    {
        this.balance = balance
            ?? throw new ArgumentNullException(nameof(balance));

        skillFormulas = new SkillFormulas(balance);
    }

    /// <summary>Calculates the current rates of an activity.</summary>
    public ActivityRates CalculateRates(Activity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);

        return activity switch
        {
            ClassActivity classActivity => CalculateClassRates(classActivity.Course, classActivity.Facility, classActivity.Mode),
            JobActivity jobActivity => CalculateJobRates(jobActivity.Rank, jobActivity.Mode),
            CrimeActivity => throw new NotSupportedException("A crime has no constant rates: it pays when an attempt ends."),
            _ => throw new NotSupportedException($"Unknown activity type {activity.GetType().Name}.")
        };
    }

    /// <summary>Calculates the rates of taking a course at a facility.</summary>
    public ActivityRates CalculateClassRates(Course course, TrainingFacility facility, ActivityMode mode)
    {
        ArgumentNullException.ThrowIfNull(course);
        ArgumentNullException.ThrowIfNull(facility);

        var cost = course.CostPerSecond * facility.CostMultiplier * balance.ClassCostMultiplier;

        // Free courses remain available at every facility, but facility upgrades only
        // improve paid courses. Global balance and activity-mode factors still apply.
        var facilityExperienceMultiplier = course.CostPerSecond > 0
            ? facility.ExperienceMultiplier
            : 1.0;

        var experienceFactor =
            facilityExperienceMultiplier
            * balance.ClassExperienceMultiplier
            * CalculateRewardFactor(mode);

        var experience = course.ExperiencePerSecond.ToDictionary(
            skill => skill.Key,
            skill => skill.Value * experienceFactor);

        return new ActivityRates(-cost, experience);
    }

    /// <summary>Calculates the rates of working at a rank. Salary, experience and reputation are all rewards.</summary>
    public ActivityRates CalculateJobRates(JobRank rank, ActivityMode mode)
    {
        ArgumentNullException.ThrowIfNull(rank);

        var reward = CalculateRewardFactor(mode);

        var experience = rank.ExperiencePerSecond.ToDictionary(
            skill => skill.Key,
            skill => skill.Value * balance.JobExperienceMultiplier * reward);

        return new ActivityRates(
            rank.SalaryPerSecond * balance.JobSalaryMultiplier * reward,
            experience,
            rank.ReputationPerSecond * balance.JobReputationMultiplier * reward);
    }

    /// <summary>
    /// Calculates the chance that an attempt at a crime succeeds: the average of the skill
    /// levels the crime asks for (weighted by how much each counts), against how hard the
    /// crime is. Whether the player is focused makes no difference.
    /// </summary>
    public double CalculateCrimeSuccessChance(Crime crime, PlayerSkills skills)
    {
        ArgumentNullException.ThrowIfNull(crime);
        ArgumentNullException.ThrowIfNull(skills);

        var totalWeight = crime.SuccessWeights.Values.Sum();

        if (totalWeight <= 0)
            throw new InvalidOperationException($"The crime '{crime.Name}' has no success weights.");

        var weightedLevel = crime.SuccessWeights.Sum(entry => entry.Value * GetLevel(skills, entry.Key)) / totalWeight;

        return Math.Clamp(weightedLevel / (crime.Difficulty * balance.CrimeSkillScale), 0.0, 1.0);
    }

    /// <summary>
    /// Calculates what one attempt at a crime gives. A failed attempt pays no money and only
    /// part of the experience; not being focused reduces everything that is given.
    /// </summary>
    public CrimeRewards CalculateCrimeRewards(Crime crime, ActivityMode mode, bool succeeded)
    {
        ArgumentNullException.ThrowIfNull(crime);

        var reward = CalculateRewardFactor(mode);
        var experienceFactor = balance.CrimeExperienceMultiplier * reward * (succeeded ? 1.0 : balance.CrimeFailureExperienceFactor);

        var experience = crime.Experience.ToDictionary(
            skill => skill.Key,
            skill => skill.Value * experienceFactor);

        var money = succeeded ? crime.Money * balance.CrimeMoneyMultiplier * reward : 0.0;

        return new CrimeRewards(money, experience);
    }

    /// <summary>Gets whether the player has the skill levels and reputation a rank asks for.</summary>
    public bool MeetsRequirements(JobRank rank, PlayerSkills skills, double reputation)
    {
        ArgumentNullException.ThrowIfNull(rank);
        ArgumentNullException.ThrowIfNull(skills);

        if (reputation < rank.RequiredReputation)
            return false;

        foreach (var (skill, level) in rank.RequiredLevels)
        {
            if (GetLevel(skills, skill) < level)
                return false;
        }

        return true;
    }

    /// <summary>Lists what the player still lacks for a rank. Empty if they qualify.</summary>
    public IReadOnlyList<UnmetRequirement> GetUnmetRequirements(JobRank rank, PlayerSkills skills, double reputation)
    {
        ArgumentNullException.ThrowIfNull(rank);
        ArgumentNullException.ThrowIfNull(skills);

        var unmet = new List<UnmetRequirement>();

        foreach (var (skill, level) in rank.RequiredLevels.OrderBy(entry => entry.Key))
        {
            var current = GetLevel(skills, skill);

            if (current < level)
                unmet.Add(new UnmetRequirement($"{skill.ToString().ToLowerInvariant()} level", level, current));
        }

        if (reputation < rank.RequiredReputation)
            unmet.Add(new UnmetRequirement("reputation", rank.RequiredReputation, reputation));

        return unmet;
    }

    /// <summary>
    /// Finds the highest rank of a career the player qualifies for.
    /// </summary>
    /// <returns>Its index, or -1 if the player does not even qualify for the entry rank.</returns>
    public int FindHighestQualifyingRank(CareerPath career, PlayerSkills skills, double reputation)
    {
        ArgumentNullException.ThrowIfNull(career);

        // Each rank asks for at least as much as the one before (the loader checks), so qualifying is a prefix.
        var highest = -1;

        for (var i = 0; i < career.Ranks.Count && MeetsRequirements(career.Ranks[i], skills, reputation); i++)
            highest = i;

        return highest;
    }

    private int GetLevel(PlayerSkills skills, SkillKind skill)
        => skillFormulas.CalculateLevel(skill, skills.GetExperience(skill));

    /// <summary>Gets the share of its rewards an activity gives in a mode.</summary>
    public double CalculateRewardFactor(ActivityMode mode) => mode switch
    {
        ActivityMode.Focused => 1.0,
        ActivityMode.Background => balance.UnfocusedRewardFactor,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown activity mode.")
    };
}