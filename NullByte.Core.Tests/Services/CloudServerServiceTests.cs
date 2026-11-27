using NullByte.Core.Configuration;
using NullByte.Core.Domain;
using NullByte.Core.Tests.Infrastructure;

namespace NullByte.Core.Tests.Services;

public sealed class CloudServerServiceTests
{
    [Fact]
    public void Purchase_CreatesDiscoverableRootedServerConnectedToHome()
    {
        var world = Create(money: 200_000);

        var result = world.CloudServerService.Purchase("cloud-one");

        Assert.True(result.Succeeded);
        Assert.Equal(100_000, result.Cost);
        Assert.Equal(100_000, world.Player.Money);

        var server = Assert.IsType<Server>(result.Server);
        Assert.True(server.IsCloudServer);
        Assert.Equal(8.0, server.MaximumRam);
        Assert.Equal(0.0, server.MaximumMoney);
        Assert.Equal(0.0, server.Money);
        Assert.Equal(server.MinimumSecurity, server.Security);
        Assert.Equal(1, server.RequiredHackingLevel);
        Assert.Equal(0, server.GrowthParameter);
        Assert.True(server.HasRootAccess);
        Assert.False(server.HasBackdoor);
        Assert.True(world.Player.DiscoveredServerIds.Contains(server.Id));
        Assert.True(world.Network.AreConnected(world.Player.HomeServerId, server.Id));
        Assert.True(world.NetworkService.IsServerKnown(server.Hostname));
    }

    [Fact]
    public void Purchase_AtHigherTier_IncludesIntermediateUpgradeCosts()
    {
        var world = Create(money: 300_000);

        var result = world.CloudServerService.Purchase("cloud-large", 32);

        Assert.True(result.Succeeded);
        Assert.Equal(250_000, result.Cost);
        Assert.Equal(50_000, world.Player.Money);
        Assert.Equal(32.0, result.Server!.MaximumRam);
    }

    [Fact]
    public void Purchase_UsesIncreasingServerCost()
    {
        var world = Create(money: 1_000_000);

        Assert.True(world.CloudServerService.Purchase("cloud-one").Succeeded);
        Assert.Equal(200_000, world.CloudServerService.GetPurchaseCost());
        Assert.True(world.CloudServerService.Purchase("cloud-two").Succeeded);
        Assert.Equal(400_000, world.CloudServerService.GetPurchaseCost());
    }

    [Fact]
    public void Purchase_WithDuplicateHostname_FailsWithoutSpendingMoney()
    {
        var world = Create(money: 200_000);

        Assert.True(world.CloudServerService.Purchase("cloud-one").Succeeded);
        var remainingMoney = world.Player.Money;
        var serverCount = world.Network.Servers.Count;

        var result = world.CloudServerService.Purchase("CLOUD-ONE");

        Assert.False(result.Succeeded);
        Assert.Equal(remainingMoney, world.Player.Money);
        Assert.Equal(serverCount, world.Network.Servers.Count);
    }

    [Fact]
    public void Purchase_WithUnsupportedRam_FailsWithoutChangingState()
    {
        var world = Create(money: 200_000);

        var result = world.CloudServerService.Purchase("cloud-one", 24);

        Assert.False(result.Succeeded);
        Assert.Equal(200_000, world.Player.Money);
        Assert.Single(world.Network.Servers);
    }

    [Fact]
    public void Purchase_WhenUnaffordable_FailsWithoutChangingState()
    {
        var world = Create(money: 99_999);

        var result = world.CloudServerService.Purchase("cloud-one");

        Assert.False(result.Succeeded);
        Assert.Equal(99_999, world.Player.Money);
        Assert.Equal(1, world.Network.Servers.Count);
    }

    [Fact]
    public void Purchase_EnforcesConfiguredServerLimit()
    {
        var world = Create(
            money: 1_000_000,
            balance: new BalanceConfig { CloudServerMaxCount = 1 });

        Assert.True(world.CloudServerService.Purchase("cloud-one").Succeeded);
        var money = world.Player.Money;

        var result = world.CloudServerService.Purchase("cloud-two");

        Assert.False(result.Succeeded);
        Assert.Equal(money, world.Player.Money);
        Assert.Equal(1, world.CloudServerService.CloudServerCount);
    }

    [Fact]
    public void Purchase_CostAndLimitAreConfigurable()
    {
        var world = Create(
            money: 500,
            balance: new BalanceConfig
            {
                CloudServerMaxCount = 2,
                CloudServerBaseCost = 250,
                CloudServerCostGrowth = 3
            });

        Assert.Equal(250, world.CloudServerService.GetPurchaseCost());
        Assert.True(world.CloudServerService.Purchase("cloud-one").Succeeded);
        Assert.Equal(750, world.CloudServerService.GetPurchaseCost());
    }

    [Fact]
    public void Upgrade_PreservesIdentityStateAndExistingRamAllocation()
    {
        var world = Create(money: 500_000);
        var purchased = world.CloudServerService.Purchase("cloud-one");
        var server = purchased.Server!;
        server.AllocateRam(6);
        var originalId = server.Id;

        var result = world.CloudServerService.Upgrade("cloud-one", 32);

        Assert.True(result.Succeeded);
        Assert.Equal(150_000, result.Cost);
        Assert.Same(server, result.Server);
        Assert.Equal(originalId, server.Id);
        Assert.Equal(32.0, server.MaximumRam);
        Assert.Equal(6.0, server.UsedRam);
        Assert.Equal(26.0, server.AvailableRam);
        Assert.True(world.Network.AreConnected(world.Player.HomeServerId, server.Id));
        Assert.True(world.Player.DiscoveredServerIds.Contains(server.Id));
    }

    [Fact]
    public void Upgrade_WithInsufficientMoney_DoesNotChangeServerOrPlayer()
    {
        var world = Create(money: 100_000);
        var server = world.CloudServerService.Purchase("cloud-one").Server!;
        var money = world.Player.Money;

        var result = world.CloudServerService.Upgrade("cloud-one", 16);

        Assert.False(result.Succeeded);
        Assert.Equal(money, world.Player.Money);
        Assert.Equal(8.0, server.MaximumRam);
    }

    [Fact]
    public void Upgrade_RejectsNonCloudServersAndInvalidTiers()
    {
        var world = Create(money: 500_000);
        var normalServer = TestServers.Create("ordinary");
        world.Network.AddServer(normalServer);

        Assert.False(world.CloudServerService.Upgrade("ordinary", 16).Succeeded);
        Assert.False(world.CloudServerService.Upgrade("missing", 16).Succeeded);
        var cloud = world.CloudServerService.Purchase("cloud-one").Server!;
        Assert.False(world.CloudServerService.Upgrade("cloud-one", 8).Succeeded);
        Assert.False(world.CloudServerService.Upgrade("cloud-one", 24).Succeeded);
        Assert.Equal(8.0, cloud.MaximumRam);
    }

    [Fact]
    public void GetServers_ReturnsOnlyCloudServers()
    {
        var world = Create(money: 500_000);
        world.Network.AddServer(TestServers.Create("ordinary"));
        world.CloudServerService.Purchase("cloud-b");
        world.CloudServerService.Purchase("cloud-a");

        Assert.Equal(new[] { "cloud-a", "cloud-b" }, world.CloudServerService.GetServers().Select(server => server.Hostname));
    }

    private static CoreTestWorld Create(double money, BalanceConfig? balance = null)
        => CoreTestSetup.CreateWorld(new CoreTestOptions { Money = money, Balance = balance });
}
