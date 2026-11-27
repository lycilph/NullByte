using NullByte.Core.Tests.Infrastructure;
using NSubstitute;
using NullByte.Core.Scripting;

namespace NullByte.Core.Tests.Scripting;

public sealed class NullByteCloudApiTests
{
    [Fact]
    public void GetServers_ReturnsOwnedCloudServersAsReadOnlyInfo()
    {
        var world = CreateWorld();
        world.CloudServerService.Purchase("cloud-b");
        var first = world.CloudServerService.Purchase("cloud-a").Server!;
        first.AllocateRam(3);

        var servers = world.CreateApi(Substitute.For<NullByte.Core.Services.IScriptOperations>()).Cloud.GetServers();

        Assert.Collection(servers,
            server =>
            {
                Assert.Equal("cloud-a", server.Hostname);
                Assert.Equal(8, server.MaximumRam);
                Assert.Equal(3, server.UsedRam);
            },
            server => Assert.Equal("cloud-b", server.Hostname));
    }

    [Fact]
    public void GetServer_ReturnsMatchingCloudServerOrNull()
    {
        var world = CreateWorld();
        world.CloudServerService.Purchase("cloud-one");
        var api = world.CreateApi(Substitute.For<NullByte.Core.Services.IScriptOperations>()).Cloud;

        Assert.Equal("cloud-one", api.GetServer("CLOUD-ONE")!.Hostname);
        Assert.Null(api.GetServer("missing"));
    }

    [Fact]
    public void GetPurchaseCost_DelegatesToCloudServerService()
    {
        var world = CreateWorld();
        var api = world.CreateApi(Substitute.For<NullByte.Core.Services.IScriptOperations>()).Cloud;

        Assert.Equal(world.CloudServerService.GetPurchaseCost(32), api.GetPurchaseCost(32));
    }

    [Fact]
    public void GetUpgradeCost_ReturnsCostOrNullForInvalidRequest()
    {
        var world = CreateWorld(money: 500_000);
        world.CloudServerService.Purchase("cloud-one");
        var api = world.CreateApi(Substitute.For<NullByte.Core.Services.IScriptOperations>()).Cloud;

        Assert.Equal(world.CloudServerService.GetUpgradeCost("cloud-one", 32), api.GetUpgradeCost("cloud-one", 32));
        Assert.Null(api.GetUpgradeCost("cloud-one", 8));
        Assert.Null(api.GetUpgradeCost("missing", 32));
    }

    [Fact]
    public void Purchase_ReportsResultAndCreatesCloudServer()
    {
        var world = CreateWorld(money: 500_000);
        var api = world.CreateApi(Substitute.For<NullByte.Core.Services.IScriptOperations>()).Cloud;

        var result = api.Purchase("cloud-one", 16);

        Assert.True(result.Succeeded);
        Assert.Empty(result.FailureMessage);
        Assert.True(result.Cost > 0);
        Assert.Equal("cloud-one", result.Server!.Hostname);
        Assert.Equal(16, result.Server.MaximumRam);
        Assert.Equal(1, world.CloudServerService.CloudServerCount);
    }

    [Fact]
    public void Purchase_WhenRejected_ReportsFailureWithoutChangingState()
    {
        var world = CreateWorld(money: 0);
        var api = world.CreateApi(Substitute.For<NullByte.Core.Services.IScriptOperations>()).Cloud;

        var result = api.Purchase("cloud-one");

        Assert.False(result.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(result.FailureMessage));
        Assert.Equal(0, result.Cost);
        Assert.Null(result.Server);
        Assert.Equal(0, world.CloudServerService.CloudServerCount);
        Assert.Equal(0, world.Player.Money);
    }

    [Fact]
    public void Upgrade_ReportsUpdatedServerAndCost()
    {
        var world = CreateWorld(money: 500_000);
        world.CloudServerService.Purchase("cloud-one");
        var api = world.CreateApi(Substitute.For<NullByte.Core.Services.IScriptOperations>()).Cloud;

        var result = api.Upgrade("cloud-one", 32);

        Assert.True(result.Succeeded);
        Assert.Equal(32, result.Server!.MaximumRam);
        Assert.Equal(150_000, result.Cost);
    }

    [Fact]
    public void Rename_ReportsNewHostnameAndPreservesServerIdentity()
    {
        var world = CreateWorld(money: 500_000);
        var original = world.CloudServerService.Purchase("cloud-one").Server!;
        var originalId = original.Id;
        var api = world.CreateApi(Substitute.For<NullByte.Core.Services.IScriptOperations>()).Cloud;

        var result = api.Rename("cloud-one", "renamed");

        Assert.True(result.Succeeded);
        Assert.Equal("renamed", result.Server!.Hostname);
        Assert.Equal(originalId, original.Id);
        Assert.Null(api.GetServer("cloud-one"));
        Assert.NotNull(api.GetServer("renamed"));
    }

    [Fact]
    public void Rename_OfUnknownServer_ReportsFailure()
    {
        var world = CreateWorld();
        var api = world.CreateApi(Substitute.For<NullByte.Core.Services.IScriptOperations>()).Cloud;

        var result = api.Rename("missing", "renamed");

        Assert.False(result.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(result.FailureMessage));
        Assert.Null(result.Server);
    }

    private static NullByte.Core.Tests.Infrastructure.CoreTestWorld CreateWorld(double money = 1_000)
        => CoreTestSetup.CreateWorld(new NullByte.Core.Tests.Infrastructure.CoreTestOptions { Money = money });
}
