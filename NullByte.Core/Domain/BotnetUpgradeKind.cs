namespace NullByte.Core.Domain;

/// <summary>
/// The ways a botnet node can be upgraded.
/// </summary>
public enum BotnetUpgradeKind
{
    /// <summary>Adds one level.</summary>
    Level,

    /// <summary>Doubles the node's RAM.</summary>
    Ram,

    /// <summary>Adds one core.</summary>
    Cores
}