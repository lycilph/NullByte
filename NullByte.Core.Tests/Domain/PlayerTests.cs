using NullByte.Core.Domain;

namespace NullByte.Core.Tests.Domain;

public sealed class PlayerTests
{
    [Fact]
    public void Constructor_InitializesPlayer()
    {
        var homeId = ServerId.New();
        var player = new Player(homeId, startingMoney: 1_000, startingHackingExperience: 50);

        Assert.Equal(homeId, player.HomeServerId);
        Assert.Equal(homeId, player.CurrentServerId);
        Assert.Equal(1_000, player.Money);
        Assert.Equal(50, player.Skills.GetExperience(SkillKind.Hacking));
        Assert.Contains(homeId, player.DiscoveredServerIds);
    }

    [Fact]
    public void Constructor_WithDefaults_StartsWithZeroMoneyAndExperience()
    {
        var player = new Player(ServerId.New());
        Assert.Equal(0, player.Money);
        Assert.Equal(0, player.HackingExperience);
    }

    [Fact]
    public void Constructor_WithInvalidHomeId_Throws()
        => Assert.Throws<ArgumentException>(() => new Player(default));

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Constructor_WithInvalidStartingMoney_Throws(double startingMoney)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new Player(ServerId.New(), startingMoney: startingMoney));

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Constructor_WithInvalidStartingHackingExperience_Throws(double startingHackingExperience)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new Player(ServerId.New(), startingHackingExperience: startingHackingExperience));

    [Fact]
    public void DiscoverServer_AddsServer()
    {
        var player = new Player(ServerId.New());
        var serverId = ServerId.New();

        Assert.True(player.DiscoverServer(serverId));
        Assert.Contains(serverId, player.DiscoveredServerIds);
    }

    [Fact]
    public void DiscoverServer_WhenAlreadyDiscovered_ReturnsFalse()
    {
        var player = new Player(ServerId.New());
        var serverId = ServerId.New();

        player.DiscoverServer(serverId);
        Assert.False(player.DiscoverServer(serverId));
        Assert.Single(player.DiscoveredServerIds, id => id == serverId);
    }

    [Fact]
    public void DiscoverServer_WithInvalidId_Throws()
    {
        var player = new Player(ServerId.New());
        Assert.Throws<ArgumentException>(() => player.DiscoverServer(default));
    }
}
