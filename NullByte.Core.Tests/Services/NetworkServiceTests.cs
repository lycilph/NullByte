using NullByte.Core.Domain;
using NullByte.Core.Services;
using NullByte.Core.Tests.Infrastructure;

namespace NullByte.Core.Tests.Services;

public sealed class NetworkServiceTests
{
    [Fact]
    public void NewPlayer_HomeServerIsDiscovered()
    {
        var fixture = CreateFixture();

        Assert.Contains(
            fixture.Home.Hostname,
            fixture.Player.DiscoveredServerIds.Select(id => fixture.Service.GetServer(id).Hostname).ToHashSet(StringComparer.OrdinalIgnoreCase));

        Assert.Single(fixture.Player.DiscoveredServerIds.Select(id => fixture.Service.GetServer(id).Hostname).ToHashSet(StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void Scan_ReturnsOnlyImmediateNeighbors()
    {
        var fixture = CreateFixture();

        var result = fixture.Service.Scan(fixture.Home.Hostname);

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

        fixture.Service.Scan(fixture.Home.Hostname);

        Assert.Contains(
            fixture.Home.Hostname,
            fixture.Player.DiscoveredServerIds.Select(id => fixture.Service.GetServer(id).Hostname).ToHashSet(StringComparer.OrdinalIgnoreCase));

        Assert.Contains(
            fixture.N00dles.Hostname,
            fixture.Player.DiscoveredServerIds.Select(id => fixture.Service.GetServer(id).Hostname).ToHashSet(StringComparer.OrdinalIgnoreCase));

        Assert.Contains(
            fixture.FoodNStuff.Hostname,
            fixture.Player.DiscoveredServerIds.Select(id => fixture.Service.GetServer(id).Hostname).ToHashSet(StringComparer.OrdinalIgnoreCase));

        Assert.DoesNotContain(
            fixture.SigmaCosmetics.Hostname,
            fixture.Player.DiscoveredServerIds.Select(id => fixture.Service.GetServer(id).Hostname).ToHashSet(StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void Scan_RepeatedScanDoesNotDuplicateDiscovery()
    {
        var fixture = CreateFixture();

        fixture.Service.Scan(fixture.Home.Hostname);
        fixture.Service.Scan(fixture.Home.Hostname);

        Assert.Equal(3, fixture.Player.DiscoveredServerIds.Select(id => fixture.Service.GetServer(id).Hostname).ToHashSet(StringComparer.OrdinalIgnoreCase).Count);
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

        Assert.Equal(fixture.Home.Hostname, fixture.Service.GetCurrentServer().Hostname);
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

        Assert.Equal(fixture.Home.Hostname, fixture.Service.GetCurrentServer().Hostname);
    }

    [Fact]
    public void Connect_DiscoveredAdjacentServerSucceeds()
    {
        var fixture = CreateFixture();

        fixture.Service.Scan(fixture.Home.Hostname);

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
            fixture.Service.GetCurrentServer().Hostname);
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
        fixture.Service.Scan(fixture.Home.Hostname);

        var firstConnection = fixture.Service.Connect(fixture.N00dles.Hostname);

        Assert.True(firstConnection.Succeeded);

        fixture.Service.Scan(fixture.N00dles.Hostname);

        Assert.Contains(fixture.SigmaCosmetics.Hostname, fixture.Player.DiscoveredServerIds.Select(id => fixture.Service.GetServer(id).Hostname).ToHashSet(StringComparer.OrdinalIgnoreCase));

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

        Assert.Equal(fixture.Home.Hostname, fixture.Service.GetCurrentServer().Hostname);
    }

    [Fact]
    public void Connect_BackdooredNonAdjacentServerSucceeds()
    {
        var fixture = CreateFixture();

        fixture.Service.Scan(fixture.Home.Hostname);
        fixture.Service.Connect(fixture.N00dles.Hostname);
        fixture.Service.Scan(fixture.N00dles.Hostname);
        fixture.SigmaCosmetics.InstallBackdoor();

        // Sigma is discovered but not adjacent to home: only the backdoor lets the player connect.
        fixture.Service.GoHome();

        var result = fixture.Service.Connect(fixture.SigmaCosmetics.Hostname);

        Assert.True(result.Succeeded);
        Assert.Same(fixture.SigmaCosmetics, result.Server);
        Assert.Null(result.Error);
        Assert.Equal(fixture.SigmaCosmetics.Hostname, fixture.Service.GetCurrentServer().Hostname);
    }

    [Fact]
    public void Connect_BackdooredButUndiscoveredServerFails()
    {
        var fixture = CreateFixture();
        fixture.SigmaCosmetics.InstallBackdoor();

        var result = fixture.Service.Connect(fixture.SigmaCosmetics.Hostname);

        Assert.False(result.Succeeded);
        Assert.Contains("not been discovered", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(fixture.Home.Hostname, fixture.Service.GetCurrentServer().Hostname);
    }

    [Fact]
    public void Connect_BackdooredCurrentServerStillFails()
    {
        var fixture = CreateFixture();
        fixture.Home.InstallBackdoor();

        var result = fixture.Service.Connect(fixture.Home.Hostname);

        Assert.False(result.Succeeded);
        Assert.Contains("Already connected", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Connect_IsCaseInsensitive()
    {
        var fixture = CreateFixture();

        fixture.Service.Scan(fixture.Home.Hostname);

        var result = fixture.Service.Connect(fixture.N00dles.Hostname.ToUpperInvariant());

        Assert.True(result.Succeeded);

        Assert.Equal(fixture.N00dles.Hostname, fixture.Service.GetCurrentServer().Hostname);
    }

    [Fact]
    public void GetCurrentServer_ReturnsPlayersCurrentServer()
    {
        var fixture = CreateFixture();

        fixture.Service.Scan(fixture.Home.Hostname);

        var connection = fixture.Service.Connect(fixture.N00dles.Hostname);

        Assert.True(connection.Succeeded);

        var currentServer = fixture.Service.GetCurrentServer();

        Assert.Same(fixture.N00dles, currentServer);
    }

    [Fact]
    public void GoHome_ReturnsPlayerDirectlyToHome()
    {
        var fixture = CreateFixture();

        fixture.Service.Scan(fixture.Home.Hostname);

        var connection = fixture.Service.Connect(fixture.N00dles.Hostname);

        Assert.True(connection.Succeeded);

        var result = fixture.Service.GoHome();

        Assert.True(result.Succeeded);
        Assert.Same(fixture.Home, result.Server);
        Assert.Null(result.Error);
        Assert.Equal(fixture.Home.Hostname, fixture.Service.GetCurrentServer().Hostname);
    }

    [Fact]
    public void GoHome_WhenAlreadyHomeSucceedsWithoutChangingState()
    {
        var fixture = CreateFixture();

        var result = fixture.Service.GoHome();

        Assert.True(result.Succeeded);
        Assert.Same(fixture.Home, result.Server);
        Assert.Null(result.Error);
        Assert.Equal(fixture.Home.Hostname, fixture.Service.GetCurrentServer().Hostname);
    }

    private static NetworkFixture CreateFixture()
    {
        var home = TestServers.Home();
        var n00dles = TestServers.Create(
            "n00dles", maximumMoney: 50_000, startingMoney: 25_000, startingSecurity: 5);
        var foodNStuff = TestServers.Create(
            "foodnstuff", maximumMoney: 100_000, startingMoney: 60_000, minimumSecurity: 2, startingSecurity: 8);
        var sigmaCosmetics = TestServers.Create(
            "sigma-cosmetics", maximumMoney: 150_000, startingMoney: 75_000, minimumSecurity: 3, startingSecurity: 10);

        // home - n00dles - sigma-cosmetics, and home - foodnstuff.
        var world = CoreTestSetup.CreateWorld(new CoreTestOptions
        {
            Servers = [home, n00dles, foodNStuff, sigmaCosmetics],
            Connections =
            [
                (home.Hostname, n00dles.Hostname),
                (home.Hostname, foodNStuff.Hostname),
                (n00dles.Hostname, sigmaCosmetics.Hostname)
            ]
        });

        return new NetworkFixture(
            Player: world.Player,
            Service: world.NetworkService,
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
