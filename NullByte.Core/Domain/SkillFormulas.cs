using NullByte.Core.Configuration;
using NullByte.Core.Domain;

namespace NullByte.Core.Formulas;

/// <summary>
/// Turns skill experience into skill levels, and back.
/// </summary>
/// <remarks>
/// All skills share one curve and differ only in their multiplier
/// (see <see cref="BalanceConfig"/>). This class holds no game state.
/// </remarks>
public sealed class SkillFormulas
{
    private readonly BalanceConfig balance;

    public SkillFormulas(BalanceConfig balance)
    {
        this.balance = balance
            ?? throw new ArgumentNullException(nameof(balance));
    }

    /// <summary>Calculates the level of a skill from its accumulated experience.</summary>
    public int CalculateLevel(SkillKind kind, double experience)
    {
        if (!double.IsFinite(experience) || experience < 0)
            throw new ArgumentOutOfRangeException(nameof(experience), "Experience must be finite and non-negative.");

        var baseSkill =
            balance.SkillLogarithmMultiplier
            * Math.Log(experience + balance.SkillExperienceOffset)
            - balance.SkillLevelOffset;

        var modifiedSkill = baseSkill * GetMultiplier(kind);

        return Math.Max(1, (int)Math.Floor(modifiedSkill));
    }

    /// <summary>
    /// Calculates the experience needed to reach a level in a skill. Accurate to floating-point
    /// precision: it can exceed the exact threshold by a few ulps, never fall short of it.
    /// </summary>
    public double CalculateExperienceForLevel(SkillKind kind, int level)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);

        var multiplier = GetMultiplier(kind);

        if (!double.IsFinite(multiplier) || multiplier <= 0)
            throw new InvalidOperationException($"The {kind} skill multiplier must be finite and positive.");

        var requiredBaseSkill = level / multiplier;

        var experience =
            Math.Exp((requiredBaseSkill + balance.SkillLevelOffset)
                / balance.SkillLogarithmMultiplier)
            - balance.SkillExperienceOffset;

        experience = Math.Max(0.0, experience);

        while (CalculateLevel(kind, experience) < level)
        {
            var next = Math.BitIncrement(experience);

            if (!double.IsFinite(next) || next <= experience)
                throw new OverflowException($"Could not calculate finite experience for {kind} level {level}.");

            experience = next;
        }

        return experience;
    }

    private double GetMultiplier(SkillKind kind) => kind switch
    {
        SkillKind.Hacking => balance.HackingSkillMultiplier,
        SkillKind.Strength => balance.StrengthSkillMultiplier,
        SkillKind.Defense => balance.DefenseSkillMultiplier,
        SkillKind.Dexterity => balance.DexteritySkillMultiplier,
        SkillKind.Agility => balance.AgilitySkillMultiplier,
        SkillKind.Charisma => balance.CharismaSkillMultiplier,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown skill.")
    };
}