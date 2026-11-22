namespace NullByte.Core.Domain.Activities;

/// <summary>
/// Something the player is busy doing: a course, a job or a crime. The player
/// has at most one at a time (see <see cref="ActivityState"/>).
/// </summary>
/// <remarks>
/// An activity is state only. It remembers what it is and what it has earned so
/// far; the rates come from <c>ActivityFormulas</c> and are applied by
/// <c>ActivitySystem</c>.
/// </remarks>
public abstract class Activity
{
    private readonly Dictionary<SkillKind, double> experienceGained = [];

    protected Activity(ActivityMode mode)
    {
        Mode = mode;
    }

    /// <summary>A short description, e.g. "Studying algorithms at rothman-university".</summary>
    public abstract string Description { get; }

    /// <summary>Whether the player is focused on this activity.</summary>
    public ActivityMode Mode { get; internal set; }

    /// <summary>How long the activity has been running, in game time.</summary>
    public TimeSpan Elapsed { get; private set; }

    /// <summary>Money gained so far, net of costs: negative if the activity has cost more than it paid.</summary>
    public double NetMoney { get; private set; }

    /// <summary>Experience gained so far, per skill.</summary>
    public IReadOnlyDictionary<SkillKind, double> ExperienceGained => experienceGained;

    internal void RecordProgress(TimeSpan elapsed, double money, IReadOnlyDictionary<SkillKind, double> experience)
    {
        Elapsed += elapsed;
        NetMoney += money;

        foreach (var (skill, amount) in experience)
            experienceGained[skill] = experienceGained.GetValueOrDefault(skill) + amount;
    }
}