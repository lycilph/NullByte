using NullByte.Core.Configuration;

namespace NullByte.Core.Formulas;

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

        Skills = new SkillFormulas(balance);
        ServerOperations = new ServerOperationFormulas(balance, Skills);
        Botnet = new BotnetFormulas(balance);
        CloudServers = new CloudServerFormulas(balance);
        Network = new NetworkFormulas(balance);
        Activities = new ActivityFormulas(balance);
    }

    public SkillFormulas Skills { get; }

    public ServerOperationFormulas ServerOperations { get; }

    public BotnetFormulas Botnet { get; }

    public CloudServerFormulas CloudServers { get; }

    public NetworkFormulas Network { get; }

    public ActivityFormulas Activities { get; }
}
