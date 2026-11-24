namespace NullByte.Core.Domain.Activities;

/// <summary>
/// Something the player is busy doing: a course, a job or a crime. The player
/// has at most one at a time (see <see cref="ActivityState"/>).
/// </summary>
/// <remarks>
/// <para>
/// An activity is state only. It remembers what it is and what it has earned so
/// far; the rates come from <c>ActivityFormulas</c> and are applied by
/// <c>ActivitySystem</c>.
/// </para>
/// <para>
/// It is changed only on the game thread, but may be read from any thread (the
/// terminal draws a live display of it): <see cref="Mode"/> and
/// <see cref="Progress"/> are published safely for that.
/// </para>
/// </remarks>
public abstract class Activity
{
    private int mode;
    private ActivityProgress progress = ActivityProgress.Empty;

    protected Activity(ActivityMode mode)
    {
        this.mode = (int)mode;
    }

    /// <summary>A short description, e.g. "Studying algorithms at rothman-university".</summary>
    public abstract string Description { get; }

    /// <summary>Whether the player is focused on this activity.</summary>
    public ActivityMode Mode
    {
        get => (ActivityMode)Volatile.Read(ref mode);
        internal set => Volatile.Write(ref mode, (int)value);
    }

    /// <summary>Everything earned so far, as one consistent snapshot.</summary>
    public ActivityProgress Progress => Volatile.Read(ref progress);

    /// <summary>How long the activity has been running, in game time.</summary>
    public TimeSpan Elapsed => Progress.Elapsed;

    /// <summary>Money gained so far, net of costs: negative if the activity has cost more than it paid.</summary>
    public double NetMoney => Progress.NetMoney;

    /// <summary>Experience gained so far, per skill.</summary>
    public IReadOnlyDictionary<SkillKind, double> ExperienceGained => Progress.ExperienceGained;

    /// <summary>Reputation gained so far.</summary>
    public double ReputationGained => Progress.ReputationGained;

    /// <summary>Attempts made so far (crimes).</summary>
    public int Attempts => Progress.Attempts;

    /// <summary>Attempts that succeeded so far (crimes).</summary>
    public int Successes => Progress.Successes;

    internal void RecordProgress(
        TimeSpan elapsed,
        double money,
        IReadOnlyDictionary<SkillKind, double> experience,
        double reputation = 0,
        int attempts = 0,
        int successes = 0)
    {
        var current = progress;
        var totals = new Dictionary<SkillKind, double>(current.ExperienceGained);

        foreach (var (skill, amount) in experience)
            totals[skill] = totals.GetValueOrDefault(skill) + amount;

        Volatile.Write(ref progress, new ActivityProgress(
            current.Elapsed + elapsed,
            current.NetMoney + money,
            totals,
            current.ReputationGained + reputation,
            current.Attempts + attempts,
            current.Successes + successes));
    }
}