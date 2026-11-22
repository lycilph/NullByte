namespace NullByte.Core.Domain;

/// <summary>
/// The experience a player has gained in each <see cref="SkillKind"/>.
/// </summary>
/// <remarks>
/// This is plain state: it only stores and adds experience. Turning experience
/// into a level is the job of <see cref="Formulas.SkillFormulas"/>.
/// </remarks>
public sealed class PlayerSkills
{
    private static readonly int SkillCount = Enum.GetValues<SkillKind>().Length;

    private readonly double[] experience = new double[SkillCount];

    /// <summary>Gets the accumulated experience in a skill.</summary>
    public double GetExperience(SkillKind kind) => experience[IndexOf(kind)];

    /// <summary>Adds experience to a skill.</summary>
    public void AddExperience(SkillKind kind, double amount)
    {
        var index = IndexOf(kind);

        if (!double.IsFinite(amount) || amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Experience must be a finite, non-negative value.");

        experience[index] += amount;
    }

    private static int IndexOf(SkillKind kind)
    {
        var index = (int)kind;

        if (index < 0 || index >= SkillCount)
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown skill.");

        return index;
    }
}