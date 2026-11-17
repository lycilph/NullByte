using NullByte.Core.Configuration;
using NullByte.Core.Domain;
using NullByte.Core.Formulas;
using NullByte.Core.Scripting;
using NullByte.Core.Services;

namespace NullByte.Core.Tests.Scripting;

public sealed class NullByteBotnetApiTests
{
    [Fact]
    public void NumNodes_ReturnsNumberOfNodes()
    {
        var harness = CreateHarness();

        Assert.Equal(0, harness.Api.NumberOfNodes());

        harness.State.Botnet.AddNode();
        harness.State.Botnet.AddNode();

        Assert.Equal(2, harness.Api.NumberOfNodes());
    }

    [Fact]
    public void NodeStats_ReturnNodeInformation()
    {
        var harness = CreateHarness();

        harness.State.Botnet.AddNode();
        var node = harness.Api.GetNodeStats(0);

        Assert.Equal(1, node.Level);
        Assert.Equal(1, node.Ram);
        Assert.Equal(1, node.Cores);
    }

    [Fact]
    public void GetNodeProduction_ReturnsNodeProduction()
    {
        var harness = CreateHarness();

        harness.State.Botnet.AddNode();

        var expected = harness.Formulas.CalculateNodeProduction(harness.State.Botnet.Nodes[0]);

        Assert.Equal(expected, harness.Api.GetNodeStats(0).Production);
    }

    [Fact]
    public void GetNextNodeCost_ReturnsCostForNextNode()
    {
        var harness = CreateHarness();

        var expected = harness.Formulas.CalculateNodePurchaseCost(harness.State.Botnet.NodeCount);

        Assert.Equal(expected, harness.Api.GetNextNodeCost());

        harness.State.Botnet.AddNode();

        expected = harness.Formulas.CalculateNodePurchaseCost(harness.State.Botnet.NodeCount);

        Assert.Equal(expected, harness.Api.GetNextNodeCost());
    }

    [Fact]
    public void GetUpgradeCosts_ReturnCurrentCosts()
    {
        var harness = CreateHarness();

        harness.State.Botnet.AddNode();

        var node = harness.State.Botnet.Nodes[0];

        Assert.Equal(harness.Formulas.CalculateUpgradeCost(node, BotnetUpgradeKind.Level), harness.Api.GetLevelUpgradeCost(0));
        Assert.Equal(harness.Formulas.CalculateUpgradeCost(node, BotnetUpgradeKind.Ram), harness.Api.GetRamUpgradeCost(0));
        Assert.Equal(harness.Formulas.CalculateUpgradeCost(node,BotnetUpgradeKind.Cores), harness.Api.GetCoreUpgradeCost(0));
    }

    [Fact]
    public void GetUpgradeCosts_ReturnNullForUnknownNode()
    {
        var harness = CreateHarness();

        Assert.Null(harness.Api.GetLevelUpgradeCost(0));
        Assert.Null(harness.Api.GetRamUpgradeCost(0));
        Assert.Null(harness.Api.GetCoreUpgradeCost(0));
    }

    [Fact]
    public void PurchaseNode_BuysNode()
    {
        var harness = CreateHarness(money: 10_000);

        var cost = harness.Api.GetNextNodeCost();

        var result = harness.Api.PurchaseNode();

        Assert.True(result.Succeeded);
        Assert.Equal(cost, result.Cost);
        Assert.Equal(0, result.NodeIndex);

        Assert.Equal(1, harness.Api.NumberOfNodes());
        Assert.Equal(10_000 - cost, harness.State.Player.Money);
    }

    [Fact]
    public void PurchaseNode_ReturnsCorrectNodeIndex()
    {
        var harness = CreateHarness(money: 100_000);

        harness.State.Botnet.AddNode();

        var result = harness.Api.PurchaseNode();

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.NodeIndex);
        Assert.Equal(2, harness.Api.NumberOfNodes());
    }

    [Fact]
    public void PurchaseNode_WithTooLittleMoney_ChangesNothing()
    {
        var harness = CreateHarness(money: 0);

        var result = harness.Api.PurchaseNode();

        Assert.False(result.Succeeded);
        Assert.Equal(-1, result.NodeIndex);
        Assert.Equal(0, result.Cost);
        Assert.False(string.IsNullOrWhiteSpace(result.FailureMessage));

        Assert.Equal(0, harness.Api.NumberOfNodes());
        Assert.Equal(0, harness.State.Player.Money);
    }

    [Fact]
    public void UpgradeLevel_ChangesLevelAndSpendsMoney()
    {
        var harness = CreateHarness(money: 10_000);

        harness.State.Botnet.AddNode();

        var cost = harness.Api.GetLevelUpgradeCost(0);

        Assert.NotNull(cost);

        var result = harness.Api.UpgradeLevel(0);

        Assert.True(result.Succeeded);
        Assert.Equal(cost.Value, result.Cost);
        Assert.Equal(0, result.NodeIndex);

        Assert.Equal(2, harness.Api.GetNodeStats(0).Level);
        Assert.Equal(10_000 - cost.Value, harness.State.Player.Money);
    }

    [Fact]
    public void UpgradeRam_ChangesRamAndSpendsMoney()
    {
        var harness = CreateHarness(money: 100_000);

        harness.State.Botnet.AddNode();

        var cost = harness.Api.GetRamUpgradeCost(0);

        Assert.NotNull(cost);

        var result = harness.Api.UpgradeRam(0);

        Assert.True(result.Succeeded);
        Assert.Equal(cost.Value, result.Cost);
        Assert.Equal(0, result.NodeIndex);

        Assert.Equal(2, harness.Api.GetNodeStats(0).Ram);
        Assert.Equal(100_000 - cost.Value, harness.State.Player.Money);
    }

    [Fact]
    public void UpgradeCore_ChangesCoresAndSpendsMoney()
    {
        var harness = CreateHarness(money: 1_000_000);

        harness.State.Botnet.AddNode();

        var cost = harness.Api.GetCoreUpgradeCost(0);

        Assert.NotNull(cost);

        var result = harness.Api.UpgradeCore(0);

        Assert.True(result.Succeeded);
        Assert.Equal(cost.Value, result.Cost);
        Assert.Equal(0, result.NodeIndex);

        Assert.Equal(2, harness.Api.GetNodeStats(0).Cores);
        Assert.Equal(1_000_000 - cost.Value, harness.State.Player.Money);
    }

    [Fact]
    public void Upgrade_OnlyChangesChosenNode()
    {
        var harness = CreateHarness(money: 1_000_000);

        harness.State.Botnet.AddNode();
        harness.State.Botnet.AddNode();

        var result = harness.Api.UpgradeLevel(1);

        Assert.True(result.Succeeded);

        Assert.Equal(1, harness.Api.GetNodeStats(0).Level);
        Assert.Equal(2, harness.Api.GetNodeStats(1).Level);
    }

    [Fact]
    public void Upgrade_WithTooLittleMoney_ChangesNothing()
    {
        var harness = CreateHarness(money: 499);

        harness.State.Botnet.AddNode();

        var result = harness.Api.UpgradeLevel(0);

        Assert.False(result.Succeeded);
        Assert.Equal(1, harness.Api.GetNodeStats(0).Level);
        Assert.Equal(499, harness.State.Player.Money);
    }

    [Theory]
    [InlineData(BotnetUpgradeKind.Level)]
    [InlineData(BotnetUpgradeKind.Ram)]
    [InlineData(BotnetUpgradeKind.Cores)]
    public void Upgrade_AtTheCap_FailsWithoutSpendingMoney(
        BotnetUpgradeKind kind)
    {
        var balance = new BalanceConfig
        {
            BotnetMaxLevel = 1,
            BotnetMaxRam = 1,
            BotnetMaxCores = 1
        };

        var harness = CreateHarness(
            money: 1_000_000,
            balance: balance);

        harness.State.Botnet.AddNode();

        var result = kind switch
        {
            BotnetUpgradeKind.Level => harness.Api.UpgradeLevel(0),
            BotnetUpgradeKind.Ram => harness.Api.UpgradeRam(0),
            BotnetUpgradeKind.Cores => harness.Api.UpgradeCore(0),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        Assert.False(result.Succeeded);
        Assert.Contains("maximum", result.FailureMessage);
        Assert.Equal(1_000_000, harness.State.Player.Money);

        Assert.Null(harness.Formulas.CalculateUpgradeCost(harness.State.Botnet.Nodes[0], kind));
    }

    [Fact]
    public void UpgradeLevel_WithInvalidNode_Throws()
    {
        var harness = CreateHarness();

        var result = harness.Api.UpgradeLevel(0);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void UpgradeRam_WithInvalidNode_Throws()
    {
        var harness = CreateHarness();

        var result = harness.Api.UpgradeRam(0);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void UpgradeCore_WithInvalidNode_Throws()
    {
        var harness = CreateHarness();

        var result = harness.Api.UpgradeCore(0);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void GetNodeLevel_WithInvalidNode_Throws()
    {
        var harness = CreateHarness();

        Assert.Throws<ArgumentOutOfRangeException>(() => harness.Api.GetNodeStats(0).Level);
    }

    [Fact]
    public void GetNodeRam_WithInvalidNode_Throws()
    {
        var harness = CreateHarness();

        Assert.Throws<ArgumentOutOfRangeException>(() => harness.Api.GetNodeStats(0).Ram);
    }

    [Fact]
    public void GetNodeCores_WithInvalidNode_Throws()
    {
        var harness = CreateHarness();

        Assert.Throws<ArgumentOutOfRangeException>(() => harness.Api.GetNodeStats(0).Cores);
    }

    [Fact]
    public void GetNodeProduction_WithInvalidNode_Throws()
    {
        var harness = CreateHarness();

        Assert.Throws<ArgumentOutOfRangeException>(() => harness.Api.GetNodeStats(0).Production);
    }

    private static Harness CreateHarness(double money = 1_000, BalanceConfig? balance = null)
    {
        balance ??= new BalanceConfig();

        var network = new Network();
        var home = CreateServer(
                hostname: "home",
                maximumMoney: 0,
                startingMoney: 0,
                minimumSecurity: 1,
                startingSecurity: 1);
        network.AddServer(home);

        var player = new Player(homeHostname: "home", startingMoney: money);
        var gameState = new GameState(player, network);
        var formulas = new BotnetFormulas(balance);
        var service = new BotnetService(gameState, formulas);
        var api = new NullByteBotnetApi(gameState.Botnet, service, formulas);

        return new Harness(
            State: gameState,
            Formulas: formulas,
            Service: service,
            Api: api);
    }
    private static Server CreateServer(
            string hostname,
            double maximumMoney,
            double startingMoney,
            double minimumSecurity,
            double startingSecurity,
            bool hasRootAcess = true)
    {
        return new Server(
            hostname,
            maximumMoney,
            startingMoney,
            baseDifficulty: 5,
            minimumSecurity: minimumSecurity,
            startingSecurity: startingSecurity,
            requiredHackingLevel: 1,
            growthParameter: 30,
            maximumRam: 8,
            hasRootAccess: hasRootAcess);
    }
    private sealed record Harness(
        GameState State,
        BotnetFormulas Formulas,
        BotnetService Service,
        NullByteBotnetApi Api);
}