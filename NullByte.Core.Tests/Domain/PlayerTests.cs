using NullByte.Core.Domain;

namespace NullByte.Core.Tests.Domain;

public sealed class PlayerTests
{
    [Fact]
    public void Constructor_InitializesPlayer()
    {
        var player = new Player(
            homeHostname: "home",
            startingMoney: 1_000,
            startingHackingExperience: 50);

        Assert.Equal("home", player.HomeHostname);
        Assert.Equal("home", player.CurrentHostname);
        Assert.Equal(1_000, player.Money);
        Assert.Equal(50, player.HackingExperience);
        Assert.Contains("home", player.DiscoveredHostnames);
    }

    [Fact]
    public void Constructor_WithDefaults_StartsWithZeroMoneyAndExperience()
    {
        var player = new Player("home");

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
                "home",
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
                "home",
                startingHackingExperience:
                    startingHackingExperience));
    }

    [Fact]
    public void AddHackingExperience_IncreasesExperience()
    {
        var player = new Player(
            "home",
            startingHackingExperience: 10);

        player.AddHackingExperience(25);

        Assert.Equal(35, player.HackingExperience);
    }

    [Fact]
    public void AddHackingExperience_WithZero_DoesNotChangeExperience()
    {
        var player = new Player(
            "home",
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
        var player = new Player("home");

        Assert.Throws<ArgumentOutOfRangeException>(
            () => player.AddHackingExperience(amount));
    }

    [Fact]
    public void DiscoverServer_AddsServer()
    {
        var player = new Player("home");

        var discovered = player.DiscoverServer("foodnstuff");

        Assert.True(discovered);
        Assert.Contains(
            "foodnstuff",
            player.DiscoveredHostnames);
    }

    [Fact]
    public void DiscoverServer_WhenAlreadyDiscovered_ReturnsFalse()
    {
        var player = new Player("home");

        player.DiscoverServer("foodnstuff");

        var discoveredAgain =
            player.DiscoverServer("foodnstuff");

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
        var player = new Player("home");

        player.DiscoverServer("FoodNStuff");

        var discoveredAgain =
            player.DiscoverServer("foodnstuff");

        Assert.False(discoveredAgain);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void DiscoverServer_WithInvalidHostname_Throws(string hostname)
    {
        var player = new Player("home");

        Assert.Throws<ArgumentException>(
            () => player.DiscoverServer(hostname));
    }
}