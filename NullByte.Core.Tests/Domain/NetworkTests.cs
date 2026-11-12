using NullByte.Core.Domain;

namespace NullByte.Core.Tests.Domain;

public sealed class NetworkTests
{
    [Fact]
    public void AddServer_AddsServerToNetwork()
    {
        var network = new Network();
        var server = CreateServer("home");

        network.AddServer(server);

        Assert.Single(network.Servers);

        Assert.Same(server, network.GetServer(server.Hostname));
    }

    [Fact]
    public void AddServer_DuplicateHostnameThrows()
    {
        var network = new Network();

        network.AddServer(CreateServer("n00dles"));

        var duplicate = CreateServer("n00dles");

        var exception =Assert.Throws<InvalidOperationException>(() => network.AddServer(duplicate));

        Assert.Contains("n00dles", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(network.Servers);
    }

    [Fact]
    public void AddServer_DuplicateHostnameIsCaseInsensitive()
    {
        var network = new Network();

        network.AddServer(CreateServer(hostname: "N00DLES"));

        Assert.Throws<InvalidOperationException>(() => network.AddServer(CreateServer(hostname: "n00dles")));
    }

    [Fact]
    public void GetServer_ReturnsServerByHostnameCaseInsensitively()
    {
        var network = new Network();

        var server = CreateServer(hostname: "n00dles");

        network.AddServer(server);

        var result = network.GetServer("N00DLES");

        Assert.Same(server, result);
    }

    [Fact]
    public void GetServer_UnknownHostnameThrows()
    {
        var network = new Network();

        var exception = Assert.Throws<KeyNotFoundException>(() => network.GetServer("does-not-exist"));

        Assert.Contains("does-not-exist", exception.Message,StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryGetServer_UnknownHostnameReturnsFalse()
    {
        var network = new Network();

        var found = network.TryGetServer("does-not-exist",out var server);

        Assert.False(found);
        Assert.Null(server);
    }

    [Fact]
    public void TryFindServer_FindsServerByHostname()
    {
        var network = new Network();

        var server = CreateServer(hostname: "n00dles");

        network.AddServer(server);

        var found = network.TryGetServer("N00DLES", out var result);

        Assert.True(found);
        Assert.Same(server, result);
    }

    [Fact]
    public void Connect_CreatesBidirectionalConnection()
    {
        var network = new Network();

        var home = CreateServer(hostname: "home");
        var n00dles = CreateServer(hostname: "n00dles");

        network.AddServer(home);
        network.AddServer(n00dles);

        network.Connect(home.Hostname, n00dles.Hostname);

        Assert.True(network.AreConnected(home.Hostname, n00dles.Hostname));
        Assert.True(network.AreConnected(n00dles.Hostname, home.Hostname));
    }

    [Fact]
    public void Connect_DuplicateConnectionDoesNotCreateDuplicateNeighbor()
    {
        var network = new Network();

        var home = CreateServer(hostname: "home");
        var n00dles = CreateServer(hostname: "n00dles");

        network.AddServer(home);
        network.AddServer(n00dles);

        network.Connect(home.Hostname, n00dles.Hostname);
        network.Connect(home.Hostname, n00dles.Hostname);

        var neighbors = network.GetNeighbors(home.Hostname);

        var neighbor = Assert.Single(neighbors);

        Assert.Same(n00dles, neighbor);
    }

    [Fact]
    public void Connect_ServerToItselfThrows()
    {
        var network = new Network();

        var home = CreateServer(hostname: "home");

        network.AddServer(home);

        var exception =
            Assert.Throws<InvalidOperationException>(
                () => network.Connect(home.Hostname, home.Hostname));

        Assert.Contains(
            "cannot be connected to itself",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Connect_UnknownServerThrows()
    {
        var network = new Network();

        var home = CreateServer(hostname: "home");

        network.AddServer(home);

        Assert.Throws<KeyNotFoundException>(
            () => network.Connect(home.Hostname, "does-not-exist"));
    }

    [Fact]
    public void GetNeighbors_ReturnsDirectNeighborsOnly()
    {
        var network =
            CreateLinearNetwork(
                out var home,
                out var n00dles,
                out var sigmaCosmetics);

        var neighbors = network.GetNeighbors(n00dles.Hostname);

        Assert.Equal(2, neighbors.Count);
        Assert.Contains(home, neighbors);
        Assert.Contains(sigmaCosmetics, neighbors);
    }

    [Fact]
    public void GetNeighbors_DoesNotReturnIndirectNeighbors()
    {
        var network =
            CreateLinearNetwork(
                out var home,
                out _,
                out var sigmaCosmetics);

        var neighbors = network.GetNeighbors(home.Hostname);

        Assert.DoesNotContain(sigmaCosmetics, neighbors);
    }

    [Fact]
    public void GetNeighbors_UnconnectedServerReturnsEmptyCollection()
    {
        var network = new Network();

        var home = CreateServer(hostname: "home");

        network.AddServer(home);

        var neighbors = network.GetNeighbors(home.Hostname);

        Assert.Empty(neighbors);
    }

    [Fact]
    public void AreConnected_UnconnectedServersReturnsFalse()
    {
        var network = new Network();

        var first = CreateServer(hostname: "first");

        var second = CreateServer(hostname: "second");

        network.AddServer(first);
        network.AddServer(second);

        Assert.False(network.AreConnected(first.Hostname, second.Hostname));
    }

    private static Server CreateServer(string hostname)
    {
        return new Server(
            hostname,
            maximumMoney: 100_000,
            startingMoney: 50_000,
            minimumSecurity: 1,
            startingSecurity: 5,
            requiredHackingLevel: 1,
            growthParameter: 1.1,
            maximumRam: 32);
    }

    private static Network CreateLinearNetwork(
        out Server home,
        out Server n00dles,
        out Server sigmaCosmetics)
    {
        var network = new Network();

        home = CreateServer(hostname: "home");
        n00dles = CreateServer(hostname: "n00dles");
        sigmaCosmetics = CreateServer(hostname: "sigma-cosmetics");

        network.AddServer(home);
        network.AddServer(n00dles);
        network.AddServer(sigmaCosmetics);

        network.Connect(
            home.Hostname,
            n00dles.Hostname);

        network.Connect(
            n00dles.Hostname,
            sigmaCosmetics.Hostname);

        return network;
    }
}