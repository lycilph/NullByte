using NullByte.Core.Domain;
using NullByte.Core.Formulas;

namespace NullByte.Core.Services;

/// <summary>
/// Implements the player's botnet purchases: new nodes and upgrades.
/// </summary>
/// <remarks>
/// Purchases are instant and are intended to be made on the game thread.
/// Passive income is not handled here: that is
/// <see cref="Systems.BotnetSystem"/>.
/// </remarks>
public sealed class BotnetService
{
    private readonly GameState gameState;
    private readonly BotnetFormulas formulas;

    public BotnetService(GameState gameState, BotnetFormulas formulas)
    {
        this.gameState = gameState
            ?? throw new ArgumentNullException(nameof(gameState));

        this.formulas = formulas
            ?? throw new ArgumentNullException(nameof(formulas));
    }

    /// <summary>
    /// Gets the cost of the next node.
    /// </summary>
    public double GetNextNodeCost()
        => formulas.CalculateNodePurchaseCost(gameState.Botnet.NodeCount);

    /// <summary>
    /// Gets the cost of an upgrade, or null if the node is at the cap or
    /// does not exist.
    /// </summary>
    public double? GetUpgradeCost(int nodeIndex, BotnetUpgradeKind kind)
    {
        var nodes = gameState.Botnet.Nodes;

        if (nodeIndex < 0 || nodeIndex >= nodes.Count)
            return null;

        return formulas.CalculateUpgradeCost(nodes[nodeIndex], kind);
    }

    /// <summary>
    /// Buys a new node if the player can afford it.
    /// </summary>
    public BotnetPurchaseResult PurchaseNode()
    {
        var cost = GetNextNodeCost();

        if (!gameState.Player.TrySpendMoney(cost))
            return BotnetPurchaseResult.Failed($"Not enough money. A new node costs ${cost:N2}.");

        gameState.Botnet.AddNode();

        return new BotnetPurchaseResult(
            Succeeded: true,
            FailureMessage: string.Empty,
            Cost: cost,
            NodeIndex: gameState.Botnet.NodeCount - 1);
    }

    /// <summary>
    /// Upgrades a node if it is not at the cap and the player can afford it.
    /// </summary>
    public BotnetPurchaseResult Upgrade(int nodeIndex, BotnetUpgradeKind kind)
    {
        var nodes = gameState.Botnet.Nodes;

        if (nodeIndex < 0 || nodeIndex >= nodes.Count)
            return BotnetPurchaseResult.Failed("There is no such botnet node.");

        var node = nodes[nodeIndex];
        var cost = formulas.CalculateUpgradeCost(node, kind);

        if (cost is null)
            return BotnetPurchaseResult.Failed("That upgrade is already at its maximum.");

        if (!gameState.Player.TrySpendMoney(cost.Value))
            return BotnetPurchaseResult.Failed($"Not enough money. That upgrade costs ${cost.Value:N2}.");

        node.ApplyUpgrade(kind);

        return new BotnetPurchaseResult(
            Succeeded: true,
            FailureMessage: string.Empty,
            Cost: cost.Value,
            NodeIndex: nodeIndex);
    }
}