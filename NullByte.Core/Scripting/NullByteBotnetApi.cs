using NullByte.Core.Domain;
using NullByte.Core.Formulas;
using NullByte.Core.Services;

namespace NullByte.Core.Scripting;

public sealed class NullByteBotnetApi : INullByteBotnetApi
{
    private readonly BotnetService botnetService;
    private readonly BotnetFormulas formulas;
    private readonly Botnet botnet;

    public NullByteBotnetApi(
        Botnet botnet,
        BotnetService botnetService,
        BotnetFormulas formulas)
    {
        ArgumentNullException.ThrowIfNull(botnet);
        ArgumentNullException.ThrowIfNull(botnetService);
        ArgumentNullException.ThrowIfNull(formulas);

        this.botnet = botnet;
        this.botnetService = botnetService;
        this.formulas = formulas;
    }

    public int NumberOfNodes() => botnet.NodeCount;

    public BotnetPurchaseResult PurchaseNode() => botnetService.PurchaseNode();

    public BotnetNodeInfo GetNodeStats(int nodeIndex)
    {
        var node = GetNode(nodeIndex);
        var production = formulas.CalculateNodeProduction(node);
        return new BotnetNodeInfo(node.Level, node.Ram, node.Cores, production);
    }

    public double GetNextNodeCost() => botnetService.GetNextNodeCost();

    public double? GetLevelUpgradeCost(int nodeIndex) => botnetService.GetUpgradeCost(nodeIndex, BotnetUpgradeKind.Level);
    public double? GetRamUpgradeCost(int nodeIndex) => botnetService.GetUpgradeCost(nodeIndex, BotnetUpgradeKind.Ram);
    public double? GetCoreUpgradeCost(int nodeIndex) => botnetService.GetUpgradeCost(nodeIndex, BotnetUpgradeKind.Cores);

    public BotnetPurchaseResult UpgradeLevel(int nodeIndex) => botnetService.Upgrade(nodeIndex, BotnetUpgradeKind.Level);
    public BotnetPurchaseResult UpgradeRam(int nodeIndex) => botnetService.Upgrade(nodeIndex, BotnetUpgradeKind.Ram);
    public BotnetPurchaseResult UpgradeCore(int nodeIndex) => botnetService.Upgrade(nodeIndex, BotnetUpgradeKind.Cores);

    private BotnetNode GetNode(int nodeIndex)
    {
        if (nodeIndex < 0 || nodeIndex >= botnet.Nodes.Count)
            throw new ArgumentOutOfRangeException(nameof(nodeIndex), nodeIndex, "There is no such botnet node.");

        return botnet.Nodes[nodeIndex];
    }
}