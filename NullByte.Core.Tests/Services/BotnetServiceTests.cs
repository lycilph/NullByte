using NullByte.Core.Configuration;
using NullByte.Core.Domain;
using NullByte.Core.Formulas;
using NullByte.Core.Services;

namespace NullByte.Core.Tests.Services;

public sealed class BotnetServiceTests
{
    [Fact]
    public void PurchaseNode_WithEnoughMoney_AddsANodeAndSpendsTheCost()
    {
        var (state, service) = Create(money: 1_500);

        var result = service.PurchaseNode();

        Assert.True(result.Succeeded);
        Assert.Equal(1_000.0, result.Cost);
        Assert.Equal(0, result.NodeIndex);
        Assert.Equal(1, state.Botnet.NodeCount);
        Assert.Equal(500.0, state.Player.Money);
    }

    [Fact]
    public void PurchaseNode_NewNodeStartsAtLevelOneWithOneRamAndOneCore()
    {
        var (state, service) = Create(money: 1_000);

        service.PurchaseNode();

        var node = Assert.Single(state.Botnet.Nodes);
        Assert.Equal(1, node.Level);
        Assert.Equal(1, node.Ram);
        Assert.Equal(1, node.Cores);
    }

    [Fact]
    public void PurchaseNode_WithExactlyEnoughMoney_Succeeds()
    {
        var (state, service) = Create(money: 1_000);

        Assert.True(service.PurchaseNode().Succeeded);
        Assert.Equal(0.0, state.Player.Money);
    }

    [Fact]
    public void PurchaseNode_WithTooLittleMoney_ChangesNothing()
    {
        var (state, service) = Create(money: 999.99);

        var result = service.PurchaseNode();

        Assert.False(result.Succeeded);
        Assert.Contains("Not enough money", result.FailureMessage);
        Assert.Equal(-1, result.NodeIndex);
        Assert.Equal(0, state.Botnet.NodeCount);
        Assert.Equal(999.99, state.Player.Money);
    }

    [Fact]
    public void PurchaseNode_EachNodeCostsMoreThanTheLast()
    {
        var (_, service) = Create(money: 10_000);

        Assert.Equal(1_000.0, service.GetNextNodeCost());
        service.PurchaseNode();
        Assert.Equal(1_850.0, service.GetNextNodeCost());
        service.PurchaseNode();
        Assert.Equal(3_422.5, service.GetNextNodeCost(), 6);
    }

    [Theory]
    [InlineData(BotnetUpgradeKind.Level, 500.0, 2, 1, 1)]
    [InlineData(BotnetUpgradeKind.Ram, 30_000.0, 1, 2, 1)]
    [InlineData(BotnetUpgradeKind.Cores, 500_000.0, 1, 1, 2)]
    public void Upgrade_AppliesTheUpgradeAndSpendsTheCost(
        BotnetUpgradeKind kind, double cost, int level, int ram, int cores)
    {
        var (state, service) = Create(money: 1_000_000);
        state.Botnet.AddNode();

        var result = service.Upgrade(0, kind);

        Assert.True(result.Succeeded);
        Assert.Equal(cost, result.Cost);
        Assert.Equal(0, result.NodeIndex);
        Assert.Equal(1_000_000 - cost, state.Player.Money);

        var node = state.Botnet.Nodes[0];
        Assert.Equal(level, node.Level);
        Assert.Equal(ram, node.Ram);
        Assert.Equal(cores, node.Cores);
    }

    [Fact]
    public void Upgrade_OnlyChangesTheChosenNode()
    {
        var (state, service) = Create(money: 1_000_000);
        state.Botnet.AddNode();
        state.Botnet.AddNode();

        service.Upgrade(1, BotnetUpgradeKind.Level);

        Assert.Equal(1, state.Botnet.Nodes[0].Level);
        Assert.Equal(2, state.Botnet.Nodes[1].Level);
    }

    [Fact]
    public void Upgrade_RamKeepsDoubling()
    {
        var (state, service) = Create(money: 100_000_000);
        state.Botnet.AddNode();

        for (var i = 0; i < 6; i++)
            Assert.True(service.Upgrade(0, BotnetUpgradeKind.Ram).Succeeded);

        Assert.Equal(64, state.Botnet.Nodes[0].Ram);
        Assert.False(service.Upgrade(0, BotnetUpgradeKind.Ram).Succeeded);
        Assert.Equal(64, state.Botnet.Nodes[0].Ram);
    }

    [Fact]
    public void Upgrade_WithTooLittleMoney_ChangesNothing()
    {
        var (state, service) = Create(money: 499);
        state.Botnet.AddNode();

        var result = service.Upgrade(0, BotnetUpgradeKind.Level);

        Assert.False(result.Succeeded);
        Assert.Equal(1, state.Botnet.Nodes[0].Level);
        Assert.Equal(499.0, state.Player.Money);
    }

    [Theory]
    [InlineData(BotnetUpgradeKind.Level)]
    [InlineData(BotnetUpgradeKind.Ram)]
    [InlineData(BotnetUpgradeKind.Cores)]
    public void Upgrade_AtTheCap_FailsWithoutSpendingMoney(BotnetUpgradeKind kind)
    {
        var balance = new BalanceConfig { BotnetMaxLevel = 1, BotnetMaxRam = 1, BotnetMaxCores = 1 };
        var (state, service) = Create(money: 1_000_000_000, balance);
        state.Botnet.AddNode();

        var result = service.Upgrade(0, kind);

        Assert.False(result.Succeeded);
        Assert.Contains("maximum", result.FailureMessage);
        Assert.Equal(1_000_000_000.0, state.Player.Money);
        Assert.Null(service.GetUpgradeCost(0, kind));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(3)]
    public void Upgrade_OfAMissingNode_Fails(int index)
    {
        var (state, service) = Create(money: 1_000_000);

        var result = service.Upgrade(index, BotnetUpgradeKind.Level);

        Assert.False(result.Succeeded);
        Assert.Equal(1_000_000.0, state.Player.Money);
        Assert.Null(service.GetUpgradeCost(index, BotnetUpgradeKind.Level));
    }

    private static (GameState State, BotnetService Service) Create(double money, BalanceConfig? balance = null)
    {
        var home = new Server(
            id: "home",
            hostname: "home",
            maximumMoney: 0,
            startingMoney: 0,
            minimumSecurity: 1,
            startingSecurity: 1,
            requiredHackingLevel: 1,
            growthParameter: 0,
            maximumRam: 8,
            hasRootAccess: true);

        var network = new Network();
        network.AddServer(home);

        var state = new GameState(new Player("home", startingMoney: money), network, new Botnet());
        var service = new BotnetService(state, new BotnetFormulas(balance ?? new BalanceConfig()));

        return (state, service);
    }
}