using NullByte.Core.Domain;

namespace NullByte.Core.Tests.Domain;

public sealed class PlayerTests
{
    [Fact]
    public void Constructor_InitializesPlayer()
    {
        var homeId = ServerId.New();
        var player = new Player(
            homeServerId: homeId,
            startingMoney: 1_000,
            startingHackingExperience: 50);

        Assert.Equal(homeId, player.HomeServerId);
        Assert.Equal(homeId, player.CurrentServerId);
        Assert.Equal(1_000, player.Money);
        Assert.Equal(50, player.HackingExperience);
        Assert.Contains(homeId, player.DiscoveredServerIds);
    }

    [Fact]
    public void Constructor_WithDefaults_StartsWithZeroMoneyAndExperience()
    {
        var player = new Player(ServerId.New());

        Assert.Equal(0, player.Money);
        Assert.Equal(0, player.HackingExperience);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidHomeHostname_Throws(string homeHostname)
    {
        Assert.Throws<ArgumentException>(
            () => new Player(homeHostname));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Constructor_WithInvalidStartingMoney_Throws(
        double startingMoney)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Player(
                ServerId.New(),
                startingMoney: startingMoney));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Constructor_WithInvalidStartingHackingExperience_Throws(
        double startingHackingExperience)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Player(
                ServerId.New(),
                startingHackingExperience:
                    startingHackingExperience));
    }

    [Fact]
    public void AddHackingExperience_IncreasesExperience()
    {
        var player = new Player(
            ServerId.New(),
            startingHackingExperience: 10);

        player.AddHackingExperience(25);

        Assert.Equal(35, player.HackingExperience);
    }

    [Fact]
    public void AddHackingExperience_WithZero_DoesNotChangeExperience()
    {
        var player = new Player(
            ServerId.New(),
            startingHackingExperience: 10);

        player.AddHackingExperience(0);

        Assert.Equal(10, player.HackingExperience);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void AddHackingExperience_WithInvalidAmount_Throws(
        double amount)
    {
        var player = new Player(ServerId.New());

        Assert.Throws<ArgumentOutOfRangeException>(
            () => player.AddHackingExperience(amount));
    }

    [Fact]
    public void DiscoverServer_AddsServer()
    {
        var player = new Player(ServerId.New());

        var discovered = player.DiscoverServer(ServerId.New());

        Assert.True(discovered);
        Assert.Contains(
            "foodnstuff",
            player.DiscoveredHostnames);
    }

    [Fact]
    public void DiscoverServer_WhenAlreadyDiscovered_ReturnsFalse()
    {
        var player = new Player(ServerId.New());

        player.DiscoverServer(ServerId.New());

        var discoveredAgain =
            player.DiscoverServer(ServerId.New());

        Assert.False(discoveredAgain);
        Assert.Single(
            player.DiscoveredHostnames,
            id => string.Equals(
                id,
                "foodnstuff",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DiscoverServer_IsCaseInsensitive()
    {
        var player = new Player(ServerId.New());

        player.DiscoverServer(ServerId.New());

        var discoveredAgain =
            player.DiscoverServer(ServerId.New());

        Assert.False(discoveredAgain);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void DiscoverServer_WithInvalidHostname_Throws(string hostname)
    {
        var player = new Player(ServerId.New());

        Assert.Throws<ArgumentException>(
            () => player.DiscoverServer(hostname));
    }
}