using NullByte.Core.Configuration;
using NullByte.Core.Domain;
using NullByte.Core.Formulas;

namespace NullByte.Core.Tests.Formulas;

public sealed class BotnetFormulasTests
{
    private static BotnetFormulas CreateFormulas(BalanceConfig? balance = null)
        => new(balance ?? new BalanceConfig());

    [Theory]
    [InlineData(1, 1, 1, 1.5)]
    [InlineData(10, 4, 2, 19.402562812499994)]
    [InlineData(100, 16, 4, 376.9534869192359)]
    [InlineData(200, 64, 16, 9171.309212672853)]
    public void CalculateProduction_FollowsLevelRamAndCoreFormula(int level, int ram, int cores, double expected)
    {
        var production = CreateFormulas().CalculateProduction(level, ram, cores);

        Assert.Equal(expected, production, 6);
    }

    [Fact]
    public void CalculateProduction_AppliesGlobalMultiplier()
    {
        var normal = CreateFormulas().CalculateProduction(10, 4, 2);
        var doubled = CreateFormulas(new BalanceConfig { BotnetProductionMultiplier = 2.0 })
            .CalculateProduction(10, 4, 2);

        Assert.Equal(normal * 2.0, doubled, 9);
    }

    [Fact]
    public void CalculateNodeProduction_UsesTheNodesStats()
    {
        var node = new BotnetNode(level: 10, ram: 4, cores: 2);

        Assert.Equal(19.402562812499994, CreateFormulas().CalculateNodeProduction(node), 6);
    }

    [Fact]
    public void CalculateTotalProduction_OfEmptyBotnet_IsZero()
    {
        Assert.Equal(0.0, CreateFormulas().CalculateTotalProduction(new Botnet()));
    }

    [Fact]
    public void CalculateTotalProduction_SumsAllNodes()
    {
        var botnet = new Botnet();
        botnet.AddNode();
        botnet.AddNode();
        botnet.Nodes[1].ApplyUpgrade(BotnetUpgradeKind.Level);

        // 1.5 for a level 1 node plus 3.0 for a level 2 node.
        Assert.Equal(4.5, CreateFormulas().CalculateTotalProduction(botnet), 9);
    }

    [Theory]
    [InlineData(0, 1000.0)]
    [InlineData(1, 1850.0)]
    [InlineData(2, 3422.5)]
    [InlineData(3, 6331.625)]
    public void CalculateNodePurchaseCost_GrowsGeometricallyWithNodeCount(int existingNodes, double expected)
    {
        Assert.Equal(expected, CreateFormulas().CalculateNodePurchaseCost(existingNodes), 6);
    }

    [Theory]
    [InlineData(1, 500.0)]
    [InlineData(2, 520.0)]
    [InlineData(3, 540.8)]
    [InlineData(100, 24281.225088591145)]
    [InlineData(199, 1179155.7836056561)]
    public void CalculateLevelUpgradeCost_GrowsGeometricallyWithLevel(int level, double expected)
    {
        Assert.Equal(expected, CreateFormulas().CalculateLevelUpgradeCost(level), 3);
    }

    [Theory]
    [InlineData(1, 30000.0)]
    [InlineData(2, 76800.0)]
    [InlineData(4, 196608.0)]
    [InlineData(8, 503316.48)]
    [InlineData(32, 3298534.883328)]
    public void CalculateRamUpgradeCost_ScalesWithCurrentRamAndUpgradeCount(int ram, double expected)
    {
        Assert.Equal(expected, CreateFormulas().CalculateRamUpgradeCost(ram), 3);
    }

    [Theory]
    [InlineData(1, 500000.0)]
    [InlineData(2, 740000.0)]
    [InlineData(3, 1095200.0)]
    [InlineData(15, 120957839.17451853)]
    public void CalculateCoreUpgradeCost_GrowsGeometricallyWithCores(int cores, double expected)
    {
        Assert.Equal(expected, CreateFormulas().CalculateCoreUpgradeCost(cores), 3);
    }

    [Theory]
    [InlineData(BotnetUpgradeKind.Level)]
    [InlineData(BotnetUpgradeKind.Ram)]
    [InlineData(BotnetUpgradeKind.Cores)]
    public void CalculateUpgradeCost_AtTheCap_IsNull(BotnetUpgradeKind kind)
    {
        var balance = new BalanceConfig { BotnetMaxLevel = 1, BotnetMaxRam = 1, BotnetMaxCores = 1 };
        var node = new BotnetNode();
        var formulas = CreateFormulas(balance);

        Assert.True(formulas.IsMaxed(node, kind));
        Assert.Null(formulas.CalculateUpgradeCost(node, kind));
    }

    [Fact]
    public void CalculateUpgradeCost_BelowTheCap_MatchesTheSpecificFormula()
    {
        var formulas = CreateFormulas();
        var node = new BotnetNode(level: 3, ram: 4, cores: 2);

        Assert.Equal(formulas.CalculateLevelUpgradeCost(3), formulas.CalculateUpgradeCost(node, BotnetUpgradeKind.Level));
        Assert.Equal(formulas.CalculateRamUpgradeCost(4), formulas.CalculateUpgradeCost(node, BotnetUpgradeKind.Ram));
        Assert.Equal(formulas.CalculateCoreUpgradeCost(2), formulas.CalculateUpgradeCost(node, BotnetUpgradeKind.Cores));
    }

    [Fact]
    public void DefaultCaps_MatchTheDesignDocument()
    {
        var balance = new BalanceConfig();

        Assert.Equal(200, balance.BotnetMaxLevel);
        Assert.Equal(64, balance.BotnetMaxRam);
        Assert.Equal(16, balance.BotnetMaxCores);
    }

    [Fact]
    public void InvalidArguments_Throw()
    {
        var formulas = CreateFormulas();

        Assert.Throws<ArgumentOutOfRangeException>(() => formulas.CalculateProduction(0, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => formulas.CalculateProduction(1, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => formulas.CalculateProduction(1, 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => formulas.CalculateNodePurchaseCost(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => formulas.CalculateLevelUpgradeCost(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => formulas.CalculateRamUpgradeCost(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => formulas.CalculateCoreUpgradeCost(0));
    }
}