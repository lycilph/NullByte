using NullByte.Core.Configuration;
using NullByte.Core.Formulas;

namespace NullByte.Core.Tests.Formulas;

public sealed class CloudServerFormulasTests
{
    [Theory]
    [InlineData(0, 100_000)]
    [InlineData(1, 200_000)]
    [InlineData(2, 400_000)]
    public void CalculatePurchaseCost_DoublesForEachExistingServer(int existing, double expected)
    {
        Assert.Equal(expected, new CloudServerFormulas(new BalanceConfig()).CalculateServerCost(existing));
    }

    [Fact]
    public void CalculatePurchaseCost_UsesConfiguredValues()
    {
        var formulas = new CloudServerFormulas(new BalanceConfig
        {
            CloudServerBaseCost = 250,
            CloudServerCostGrowth = 3
        });

        Assert.Equal(2_250, formulas.CalculateServerCost(2));
    }

    [Theory]
    [InlineData(8, 16, 50_000)]
    [InlineData(16, 32, 100_000)]
    [InlineData(8, 32, 150_000)]
    [InlineData(8, 256, 1_550_000)]
    public void CalculateRamUpgradeCost_SumsEachIntermediateTier(int current, int next, double expected)
    {
        Assert.Equal(expected, new CloudServerFormulas(new BalanceConfig()).CalculateRamUpgradeCost(current, next));
    }

    [Fact]
    public void CalculateRamUpgradeCost_UsesConfiguredValues()
    {
        var formulas = new CloudServerFormulas(new BalanceConfig
        {
            CloudServerRamUpgradeBaseCost = 10,
            CloudServerRamUpgradeCostGrowth = 3
        });

        Assert.Equal(40, formulas.CalculateRamUpgradeCost(8, 32));
    }

    [Theory]
    [InlineData(4, 8)]
    [InlineData(8, 8)]
    [InlineData(8, 24)]
    [InlineData(256, 512)]
    public void CalculateRamUpgradeCost_RejectsUnsupportedOrNonIncreasingTiers(int current, int next)
    {
        var formulas = new CloudServerFormulas(new BalanceConfig());

        Assert.Throws<ArgumentOutOfRangeException>(() => formulas.CalculateRamUpgradeCost(current, next));
    }

    [Fact]
    public void Constructor_RejectsInvalidConfiguration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CloudServerFormulas(new BalanceConfig { CloudServerMaxCount = -1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CloudServerFormulas(new BalanceConfig { CloudServerCostGrowth = 0.5 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CloudServerFormulas(new BalanceConfig { CloudServerRamUpgradeBaseCost = double.NaN }));
    }
}
