using NullByte.Core.Configuration;
using NullByte.Core.Domain;

namespace NullByte.Core.Formulas;

/// <summary>
/// Calculates botnet production and costs.
///
/// This class contains no mutable game state and does not modify
/// the botnet or the player.
/// </summary>
public sealed class BotnetFormulas
{
    private readonly BalanceConfig balance;

    public BotnetFormulas(BalanceConfig balance)
    {
        this.balance = balance
            ?? throw new ArgumentNullException(nameof(balance));
    }

    /// <summary>
    /// Calculates the money per second produced by a node.
    /// </summary>
    public double CalculateNodeProduction(BotnetNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return CalculateProduction(node.Level, node.Ram, node.Cores);
    }

    /// <summary>
    /// Calculates the money per second produced by a node with the given stats.
    /// </summary>
    public double CalculateProduction(int level, int ram, int cores)
    {
        if (level < 1)
            throw new ArgumentOutOfRangeException(nameof(level), "Level must be at least one.");
        if (ram < 1)
            throw new ArgumentOutOfRangeException(nameof(ram), "RAM must be at least one.");
        if (cores < 1)
            throw new ArgumentOutOfRangeException(nameof(cores), "Cores must be at least one.");

        var levelFactor = level * balance.BotnetProductionPerLevel;
        var ramFactor = Math.Pow(balance.BotnetRamProductionBase, ram - 1);
        var coreFactor = (cores + 5) / 6.0;

        return levelFactor * ramFactor * coreFactor * balance.BotnetProductionMultiplier;
    }

    /// <summary>
    /// Calculates the money per second produced by the whole botnet.
    /// </summary>
    public double CalculateTotalProduction(Botnet botnet)
    {
        ArgumentNullException.ThrowIfNull(botnet);

        var total = 0.0;

        foreach (var node in botnet.Nodes)
            total += CalculateNodeProduction(node);

        return total;
    }

    /// <summary>
    /// Calculates the cost of the next node.
    /// </summary>
    /// <param name="existingNodeCount">How many nodes the botnet already has.</param>
    public double CalculateNodePurchaseCost(int existingNodeCount)
    {
        if (existingNodeCount < 0)
            throw new ArgumentOutOfRangeException(nameof(existingNodeCount), "Node count cannot be negative.");

        return balance.BotnetNodeBaseCost 
            * Math.Pow(balance.BotnetNodeCostGrowth, existingNodeCount)
            * balance.BotnetPurchaseCostMultiplier;
    }

    /// <summary>
    /// Calculates the cost of raising a node from the given level to the next.
    /// </summary>
    public double CalculateLevelUpgradeCost(int currentLevel)
    {
        if (currentLevel < 1)
            throw new ArgumentOutOfRangeException(nameof(currentLevel), "Level must be at least one.");

        return balance.BotnetLevelBaseCost 
            * Math.Pow(balance.BotnetLevelCostGrowth, currentLevel - 1)
            * balance.BotnetPurchaseCostMultiplier;
    }

    /// <summary>
    /// Calculates the cost of doubling a node's RAM from the given amount.
    /// </summary>
    public double CalculateRamUpgradeCost(int currentRam)
    {
        if (currentRam < 1 || !int.IsPow2(currentRam))
            throw new ArgumentOutOfRangeException(nameof(currentRam), "RAM must be a power of two.");

        var upgradesSoFar = int.Log2(currentRam);

        return currentRam 
            * balance.BotnetRamBaseCost * Math.Pow(balance.BotnetRamCostGrowth, upgradesSoFar)
            * balance.BotnetPurchaseCostMultiplier;
    }

    /// <summary>
    /// Calculates the cost of adding one core to a node with the given cores.
    /// </summary>
    public double CalculateCoreUpgradeCost(int currentCores)
    {
        if (currentCores < 1)
            throw new ArgumentOutOfRangeException(nameof(currentCores), "Cores must be at least one.");

        return balance.BotnetCoreBaseCost 
            * Math.Pow(balance.BotnetCoreCostGrowth, currentCores - 1)
            * balance.BotnetPurchaseCostMultiplier;
    }

    /// <summary>
    /// Calculates the cost of an upgrade, or null if the node is already at
    /// the cap for that stat.
    /// </summary>
    public double? CalculateUpgradeCost(BotnetNode node, BotnetUpgradeKind kind)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (IsMaxed(node, kind))
            return null;

        return kind switch
        {
            BotnetUpgradeKind.Level => CalculateLevelUpgradeCost(node.Level),
            BotnetUpgradeKind.Ram => CalculateRamUpgradeCost(node.Ram),
            BotnetUpgradeKind.Cores => CalculateCoreUpgradeCost(node.Cores),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown botnet upgrade.")
        };
    }

    /// <summary>
    /// Returns whether the node has reached the cap for the given stat.
    /// </summary>
    public bool IsMaxed(BotnetNode node, BotnetUpgradeKind kind)
    {
        ArgumentNullException.ThrowIfNull(node);

        return kind switch
        {
            BotnetUpgradeKind.Level => node.Level >= balance.BotnetMaxLevel,
            BotnetUpgradeKind.Ram => node.Ram >= balance.BotnetMaxRam,
            BotnetUpgradeKind.Cores => node.Cores >= balance.BotnetMaxCores,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown botnet upgrade.")
        };
    }
}