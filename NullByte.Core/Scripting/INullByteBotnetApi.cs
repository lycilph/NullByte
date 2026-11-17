using NullByte.Core.Services;

namespace NullByte.Core.Scripting;

/// <summary>
/// Defines the botnet sub-API exposed to player-written scripts through
/// <c>nb.Botnet</c>.
/// </summary>
public interface INullByteBotnetApi
{
    /// <summary>Gets the number of botnet nodes currently owned.</summary>
    [RamCost(0.5)]
    int NumberOfNodes();

    /// <summary>Purchases a new botnet node.</summary>
    [RamCost(0.5)]
    BotnetPurchaseResult PurchaseNode();

    /// <summary>Gets the stats (level, ram, cores and production) of a botnet node.</summary>
    [RamCost(0.5)]
    BotnetNodeInfo GetNodeStats(int nodeIndex);

    /// <summary>Gets the cost of the next botnet node.</summary>
    [RamCost(0.5)]
    double GetNextNodeCost();

    /// <summary>Gets the cost of upgrading a node's level.</summary>
    [RamCost(0.5)]
    double? GetLevelUpgradeCost(int nodeIndex);

    /// <summary>Gets the cost of upgrading a node's RAM.</summary>
    [RamCost(0.5)]
    double? GetRamUpgradeCost(int nodeIndex);

    /// <summary>Gets the cost of adding a core to a node.</summary>
    [RamCost(0.5)]
    double? GetCoreUpgradeCost(int nodeIndex);

    /// <summary>Upgrades a node's level.</summary>
    [RamCost(0.5)]
    BotnetPurchaseResult UpgradeLevel(int nodeIndex);

    /// <summary>Doubles a node's RAM.</summary>
    [RamCost(0.5)]
    BotnetPurchaseResult UpgradeRam(int nodeIndex);

    /// <summary>Adds one core to a node.</summary>
    [RamCost(0.5)]
    BotnetPurchaseResult UpgradeCore(int nodeIndex);
}