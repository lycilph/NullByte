using NullByte.Core.Domain;
using NullByte.Core.Services;

namespace NullByte.Core.Tests.Services;

public sealed class NetworkServiceTests
{
    [Fact]
    public void NewPlayer_HomeServerIsDiscovered()
    {
        var fixture = CreateFixture();

        Assert.Contains(
            fixture.Home.Hostname,
            fixture.Player.DiscoveredHostnames);

        Assert.Single(fixture.Player.DiscoveredHostnames);
    }

    [Fact]
    public void Scan_ReturnsOnlyImmediateNeighbors()
    {
        var fixture = CreateFixture();

        var result = fixture.Service.Scan();

        Assert.Equal(2, result.Count);

        Assert.Contains(
            result,
            server => server.Hostname == fixture.N00dles.Hostname);

        Assert.Contains(
            result,
            server => server.Hostname == fixture.FoodNStuff.Hostname);

        Assert.DoesNotContain(
            result,
            server => server.Hostname == fixture.SigmaCosmetics.Hostname);
    }

    [Fact]
    public void Scan_DiscoversImmediateNeighbors()
    {
        var fixture = CreateFixture();

        fixture.Service.Scan();

        Assert.Contains(
            fixture.Home.Hostname,
            fixture.Player.DiscoveredHostnames);

        Assert.Contains(
            fixture.N00dles.Hostname,
            fixture.Player.DiscoveredHostnames);

        Assert.Contains(
            fixture.FoodNStuff.Hostname,
            fixture.Player.DiscoveredHostnames);

        Assert.DoesNotContain(
            fixture.SigmaCosmetics.Hostname,
            fixture.Player.DiscoveredHostnames);
    }

    [Fact]
    public void Scan_RepeatedScanDoesNotDuplicateDiscovery()
    {
        var fixture = CreateFixture();

        fixture.Service.Scan();
        fixture.Service.Scan();

        Assert.Equal(3, fixture.Player.DiscoveredHostnames.Count);
    }

    [Fact]
    public void Connect_UnknownServerFails()
    {
        var fixture = CreateFixture();

        var result = fixture.Service.Connect("does-not-exist");

        Assert.False(result.Succeeded);
        Assert.Null(result.Server);

        Assert.Contains(
            "Unknown server",
            result.Error,
            StringComparison.OrdinalIgnoreCase);

        Assert.Equal(fixture.Home.Hostname, fixture.Player.CurrentHostname);
    }

    [Fact]
    public void Connect_UndiscoveredServerFails()
    {
        var fixture = CreateFixture();

        var result = fixture.Service.Connect(fixture.N00dles.Hostname);

        Assert.False(result.Succeeded);
        Assert.Null(result.Server);

        Assert.Contains(
            "not been discovered",
            result.Error,
            StringComparison.OrdinalIgnoreCase);

        Assert.Equal(fixture.Home.Hostname, fixture.Player.CurrentHostname);
    }

    [Fact]
    public void Connect_DiscoveredAdjacentServerSucceeds()
    {
        var fixture = CreateFixture();

        fixture.Service.Scan();

        var result =
            fixture.Service.Connect(
                fixture.N00dles.Hostname);

        Assert.True(result.Succeeded);
        Assert.Same(
            fixture.N00dles,
            result.Server);

        Assert.Null(result.Error);

        Assert.Equal(
            fixture.N00dles.Hostname,
            fixture.Player.CurrentHostname);
    }

    [Fact]
    public void Connect_CurrentServerFails()
    {
        var fixture = CreateFixture();

        var result =
            fixture.Service.Connect(
                fixture.Home.Hostname);

        Assert.False(result.Succeeded);
        Assert.Null(result.Server);

        Assert.Contains(
            "Already connected",
            result.Error,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Connect_DiscoveredButNonAdjacentServerFails()
    {
        var fixture = CreateFixture();

        /*
         * Scan home:
         *
         * home
         *  | \
         *  |  foodnstuff
         *  |
         * n00dles
         *  |
         * sigma-cosmetics
         */
        fixture.Service.Scan();

        var firstConnection = fixture.Service.Connect(fixture.N00dles.Hostname);

        Assert.True(firstConnection.Succeeded);

        fixture.Service.Scan();

        Assert.Contains(fixture.SigmaCosmetics.Hostname, fixture.Player.DiscoveredHostnames);

        /*
         * Return directly home. Sigma remains discovered,
         * but it is not adjacent to home.
         */
        fixture.Service.GoHome();

        var result = fixture.Service.Connect(fixture.SigmaCosmetics.Hostname);

        Assert.False(result.Succeeded);
        Assert.Null(result.Server);

        Assert.Contains(
            "not directly connected",
            result.Error,
            StringComparison.OrdinalIgnoreCase);

        Assert.Equal(fixture.Home.Hostname, fixture.Player.CurrentHostname);
    }

    [Fact]
    public void Connect_IsCaseInsensitive()
    {
        var fixture = CreateFixture();

        fixture.Service.Scan();

        var result = fixture.Service.Connect(fixture.N00dles.Hostname.ToUpperInvariant());

        Assert.True(result.Succeeded);

        Assert.Equal(fixture.N00dles.Hostname, fixture.Player.CurrentHostname);
    }

    [Fact]
    public void GetCurrentServer_ReturnsPlayersCurrentServer()
    {
        var fixture = CreateFixture();

        fixture.Service.Scan();

        var connection = fixture.Service.Connect(fixture.N00dles.Hostname);

        Assert.True(connection.Succeeded);

        var currentServer = fixture.Service.GetCurrentServer();

        Assert.Same(fixture.N00dles, currentServer);
    }

    [Fact]
    public void GoHome_ReturnsPlayerDirectlyToHome()
    {
        var fixture = CreateFixture();

        fixture.Service.Scan();

        var connection = fixture.Service.Connect(fixture.N00dles.Hostname);

        Assert.True(connection.Succeeded);

        var result = fixture.Service.GoHome();

        Assert.True(result.Succeeded);
        Assert.Same(fixture.Home, result.Server);
        Assert.Null(result.Error);
        Assert.Equal(fixture.Home.Hostname, fixture.Player.CurrentHostname);
    }

    [Fact]
    public void GoHome_WhenAlreadyHomeSucceedsWithoutChangingState()
    {
        var fixture = CreateFixture();

        var result = fixture.Service.GoHome();

        Assert.True(result.Succeeded);
        Assert.Same(fixture.Home, result.Server);
        Assert.Null(result.Error);
        Assert.Equal(fixture.Home.Hostname, fixture.Player.CurrentHostname);
    }

    private static NetworkFixture CreateFixture()
    {
        var home =
            new Server(
                hostname: "home",
                maximumMoney: 0,
                startingMoney: 0,
                minimumSecurity: 0,
                startingSecurity: 0,
                requiredHackingLevel: 1,
                growthParameter: 1.1,
                maximumRam: 32,
                hasRootAccess: true);

        var n00dles =
            new Server(
                hostname: "n00dles",
                maximumMoney: 50_000,
                startingMoney: 25_000,
                minimumSecurity: 1,
                startingSecurity: 5,
                requiredHackingLevel: 1,
                growthParameter: 1.1,
                maximumRam: 32);

        var foodNStuff =
            new Server(
                hostname: "foodnstuff",
                maximumMoney: 100_000,
                minimumSecurity: 2,
                startingMoney: 60_000,
                startingSecurity: 8,
                requiredHackingLevel: 1,
                growthParameter: 1.1,
                maximumRam: 32);

        var sigmaCosmetics =
            new Server(
                hostname: "sigma-cosmetics",
                maximumMoney: 150_000,
                minimumSecurity: 3,
                startingMoney: 75_000,
                startingSecurity: 10,
                requiredHackingLevel: 1,
                growthParameter: 1.1,
                maximumRam: 32);

        var network =
            new Network();

        network.AddServer(home);
        network.AddServer(n00dles);
        network.AddServer(foodNStuff);
        network.AddServer(sigmaCosmetics);

        network.Connect(home.Hostname, n00dles.Hostname);
        network.Connect(home.Hostname, foodNStuff.Hostname);
        network.Connect(n00dles.Hostname, sigmaCosmetics.Hostname);

        var player = new Player(homeHostname: home.Hostname);
        var gameState = new GameState(player, network);
        var service = new NetworkService(gameState);

        return new NetworkFixture(
            Player: player,
            Service: service,
            Home: home,
            N00dles: n00dles,
            FoodNStuff: foodNStuff,
            SigmaCosmetics: sigmaCosmetics);
    }

    private sealed record NetworkFixture(
        Player Player,
        NetworkService Service,
        Server Home,
        Server N00dles,
        Server FoodNStuff,
        Server SigmaCosmetics);
}
