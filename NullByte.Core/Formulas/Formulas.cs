using NullByte.Core.Configuration;

namespace NullByte.Core.Formulas;

/// <summary>
/// Provides the game's formula subsystem as a single composition boundary.
///
/// The individual formula classes remain responsible for their own domain;
/// this class only groups them and ensures they are created from the same
/// balance configuration.
/// </summary>
public sealed class Formulas
{
    public Formulas(BalanceConfig balance)
    {
        ArgumentNullException.ThrowIfNull(balance);

        Skills = new SkillFormulas(balance);
        Hacking = new HackingFormulas(balance);
        Botnet = new BotnetFormulas(balance);
        Network = new NetworkFormulas(balance);
        Activities = new ActivityFormulas(balance);
    }

    public SkillFormulas Skills { get; }

    public HackingFormulas Hacking { get; }

    public BotnetFormulas Botnet { get; }

    public NetworkFormulas Network { get; }

    public ActivityFormulas Activities { get; }
}
