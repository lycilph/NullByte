using NullByte.Core.Domain;
using NullByte.Core.Scripting;
using NullByte.Core.Services;
using NullByte.Core.Tests.Infrastructure;

namespace NullByte.Core.Tests.Scripting;

/// <summary>
/// The botnet API only delegates to <see cref="BotnetService"/>, whose rules (caps, costs, rejected purchases) are
/// covered by <c>BotnetServiceTests</c>. These tests check that the API reports the same state and results.
/// </summary>
public sealed class NullByteBotnetApiTests
{
    [Fact]
    public void NumberOfNodes_CountsTheNodes()
    {
        var harness = CreateHarness();

        Assert.Equal(0, harness.Api.NumberOfNodes());

        harness.World.State.Botnet.AddNode();
        harness.World.State.Botnet.AddNode();

        Assert.Equal(2, harness.Api.NumberOfNodes());
    }

    [Fact]
    public void GetNodeStats_ReturnsTheNodeLevelRamCoresAndProduction()
    {
        var harness = CreateHarness();
        harness.World.State.Botnet.AddNode();

        var node = harness.Api.GetNodeStats(0);

        Assert.Equal(1, node.Level);
        Assert.Equal(1, node.Ram);
        Assert.Equal(1, node.Cores);
        Assert.Equal(harness.World.BotnetFormulas.CalculateNodeProduction(harness.World.State.Botnet.Nodes[0]), node.Production);
    }

    [Fact]
    public void GetNodeStats_WithInvalidNode_Throws()
    {
        var harness = CreateHarness();

        Assert.Throws<ArgumentOutOfRangeException>(() => harness.Api.GetNodeStats(0));
    }

    [Fact]
    public void GetNextNodeCost_FollowsTheNumberOfNodes()
    {
        var harness = CreateHarness();
        var formulas = harness.World.BotnetFormulas;

        Assert.Equal(formulas.CalculateNodePurchaseCost(0), harness.Api.GetNextNodeCost());

        harness.World.State.Botnet.AddNode();

        Assert.Equal(formulas.CalculateNodePurchaseCost(1), harness.Api.GetNextNodeCost());
    }

    [Theory]
    [InlineData(BotnetUpgradeKind.Level)]
    [InlineData(BotnetUpgradeKind.Ram)]
    [InlineData(BotnetUpgradeKind.Cores)]
    public void GetUpgradeCost_ReturnsTheCurrentCostOrNullForAnUnknownNode(BotnetUpgradeKind kind)
    {
        var harness = CreateHarness();

        Assert.Null(GetUpgradeCost(harness.Api, kind, 0));

        harness.World.State.Botnet.AddNode();

        var expected = harness.World.BotnetFormulas.CalculateUpgradeCost(harness.World.State.Botnet.Nodes[0], kind);

        Assert.Equal(expected, GetUpgradeCost(harness.Api, kind, 0));
    }

    [Fact]
    public void PurchaseNode_BuysTheNextNode()
    {
        var harness = CreateHarness(money: 10_000);
        var cost = harness.Api.GetNextNodeCost();

        var result = harness.Api.PurchaseNode();

        Assert.True(result.Succeeded);
        Assert.Equal(cost, result.Cost);
        Assert.Equal(0, result.NodeIndex);
        Assert.Equal(1, harness.Api.NumberOfNodes());
        Assert.Equal(10_000 - cost, harness.World.Player.Money);
    }

    [Fact]
    public void PurchaseNode_WithExistingNodes_ReturnsTheNewNodesIndex()
    {
        var harness = CreateHarness(money: 100_000);
        harness.World.State.Botnet.AddNode();

        var result = harness.Api.PurchaseNode();

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.NodeIndex);
        Assert.Equal(2, harness.Api.NumberOfNodes());
    }

    [Fact]
    public void PurchaseNode_WithTooLittleMoney_ReturnsTheFailureAndChangesNothing()
    {
        var harness = CreateHarness(money: 0);

        var result = harness.Api.PurchaseNode();

        Assert.False(result.Succeeded);
        Assert.Equal(-1, result.NodeIndex);
        Assert.Equal(0, result.Cost);
        Assert.False(string.IsNullOrWhiteSpace(result.FailureMessage));
        Assert.Equal(0, harness.Api.NumberOfNodes());
        Assert.Equal(0, harness.World.Player.Money);
    }

    [Theory]
    [InlineData(BotnetUpgradeKind.Level)]
    [InlineData(BotnetUpgradeKind.Ram)]
    [InlineData(BotnetUpgradeKind.Cores)]
    public void Upgrade_ImprovesTheNodeAndSpendsTheCost(BotnetUpgradeKind kind)
    {
        var harness = CreateHarness(money: 1_000_000);
        harness.World.State.Botnet.AddNode();

        var cost = GetUpgradeCost(harness.Api, kind, 0);

        Assert.NotNull(cost);

        var result = Upgrade(harness.Api, kind, 0);

        Assert.True(result.Succeeded);
        Assert.Equal(cost.Value, result.Cost);
        Assert.Equal(0, result.NodeIndex);
        Assert.Equal(2, GetStat(harness.Api, kind, 0));
        Assert.Equal(1_000_000 - cost.Value, harness.World.Player.Money);
    }

    [Theory]
    [InlineData(BotnetUpgradeKind.Level)]
    [InlineData(BotnetUpgradeKind.Ram)]
    [InlineData(BotnetUpgradeKind.Cores)]
    public void Upgrade_OfAMissingNode_Fails(BotnetUpgradeKind kind)
    {
        var harness = CreateHarness();

        var result = Upgrade(harness.Api, kind, 0);

        Assert.False(result.Succeeded);
    }

    private static double? GetUpgradeCost(NullByteBotnetApi api, BotnetUpgradeKind kind, int nodeIndex) => kind switch
    {
        BotnetUpgradeKind.Level => api.GetLevelUpgradeCost(nodeIndex),
        BotnetUpgradeKind.Ram => api.GetRamUpgradeCost(nodeIndex),
        BotnetUpgradeKind.Cores => api.GetCoreUpgradeCost(nodeIndex),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static BotnetPurchaseResult Upgrade(NullByteBotnetApi api, BotnetUpgradeKind kind, int nodeIndex) => kind switch
    {
        BotnetUpgradeKind.Level => api.UpgradeLevel(nodeIndex),
        BotnetUpgradeKind.Ram => api.UpgradeRam(nodeIndex),
        BotnetUpgradeKind.Cores => api.UpgradeCore(nodeIndex),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static int GetStat(NullByteBotnetApi api, BotnetUpgradeKind kind, int nodeIndex)
    {
        var node = api.GetNodeStats(nodeIndex);

        return kind switch
        {
            BotnetUpgradeKind.Level => node.Level,
            BotnetUpgradeKind.Ram => node.Ram,
            BotnetUpgradeKind.Cores => node.Cores,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    private static Harness CreateHarness(double money = 1_000)
    {
        var world = CoreTestSetup.CreateWorld(new CoreTestOptions { Money = money });
        var api = new NullByteBotnetApi(world.State.Botnet, world.BotnetService, world.BotnetFormulas);

        return new Harness(world, api);
    }

    private sealed record Harness(CoreTestWorld World, NullByteBotnetApi Api);
}
