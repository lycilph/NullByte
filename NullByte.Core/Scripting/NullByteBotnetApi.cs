using NullByte.Core.Domain;
using NullByte.Core.Formulas;
using NullByte.Core.Services;

namespace NullByte.Core.Scripting;

public sealed class NullByteBotnetApi : INullByteBotnetApi
{
    private readonly BotnetService botnetService;
    private readonly BotnetFormulas formulas;
    private readonly Botnet botnet;
    private readonly NullByte.Core.Domain.NarrativeState? narrative;

    public NullByteBotnetApi(
        Botnet botnet,
        BotnetService botnetService,
        BotnetFormulas formulas,
        NullByte.Core.Domain.NarrativeState? narrative = null)
    {
        ArgumentNullException.ThrowIfNull(botnet);
        ArgumentNullException.ThrowIfNull(botnetService);
        ArgumentNullException.ThrowIfNull(formulas);

        this.botnet = botnet;
        this.botnetService = botnetService;
        this.formulas = formulas;
        this.narrative = narrative;
    }

    public int NumberOfNodes() { Require("botnet.inspect"); return botnet.NodeCount; }

    public BotnetPurchaseResult PurchaseNode() { Require("botnet.purchase"); return botnetService.PurchaseNode(); }

    public BotnetNodeInfo GetNodeStats(int nodeIndex)
    {
        Require("botnet.inspect");
        var node = GetNode(nodeIndex);
        var production = formulas.CalculateNodeProduction(node);
        return new BotnetNodeInfo(node.Level, node.Ram, node.Cores, production);
    }

    public double GetNextNodeCost() { Require("botnet.inspect"); return botnetService.GetNextNodeCost(); }

    public double? GetLevelUpgradeCost(int nodeIndex) { Require("botnet.inspect"); return botnetService.GetUpgradeCost(nodeIndex, BotnetUpgradeKind.Level); }
    public double? GetRamUpgradeCost(int nodeIndex) { Require("botnet.inspect"); return botnetService.GetUpgradeCost(nodeIndex, BotnetUpgradeKind.Ram); }
    public double? GetCoreUpgradeCost(int nodeIndex) { Require("botnet.inspect"); return botnetService.GetUpgradeCost(nodeIndex, BotnetUpgradeKind.Cores); }

    public BotnetPurchaseResult UpgradeLevel(int nodeIndex) { Require("botnet.upgrade"); return botnetService.Upgrade(nodeIndex, BotnetUpgradeKind.Level); }
    public BotnetPurchaseResult UpgradeRam(int nodeIndex) { Require("botnet.upgrade"); return botnetService.Upgrade(nodeIndex, BotnetUpgradeKind.Ram); }
    public BotnetPurchaseResult UpgradeCore(int nodeIndex) { Require("botnet.upgrade"); return botnetService.Upgrade(nodeIndex, BotnetUpgradeKind.Cores); }

    private void Require(string capability) => narrative?.EnsureCapability(capability);

    private BotnetNode GetNode(int nodeIndex)
    {
        if (nodeIndex < 0 || nodeIndex >= botnet.Nodes.Count)
            throw new ArgumentOutOfRangeException(nameof(nodeIndex), nodeIndex, "There is no such botnet node.");

        return botnet.Nodes[nodeIndex];
    }
}