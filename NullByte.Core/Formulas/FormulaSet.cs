using NullByte.Core.Configuration;

namespace NullByte.Core.FormulaSet;

/// <summary>
/// Provides the game's formula subsystem as a single composition boundary.
///
/// The individual formula classes remain responsible for their own domain;
/// this class only groups them and ensures they are created from the same
/// balance configuration.
/// </summary>
public sealed class FormulaSet
{
    public FormulaSet(BalanceConfig balance)
    {
        ArgumentNullException.ThrowIfNull(balance);

        Skills = new SkillFormulaSet(balance);
        Hacking = new HackingFormulaSet(balance);
        Botnet = new BotnetFormulaSet(balance);
        Network = new NetworkFormulaSet(balance);
        Activities = new ActivityFormulaSet(balance);
    }

    public SkillFormulaSet Skills { get; }

    public HackingFormulaSet Hacking { get; }

    public BotnetFormulaSet Botnet { get; }

    public NetworkFormulaSet Network { get; }

    public ActivityFormulaSet Activities { get; }
}
