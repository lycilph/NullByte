using NullByte.Core.Domain;

namespace NullByte.Core.Tests.Domain;

public sealed class NetworkTests
{
    [Fact]
    public void AddServer_AddsServerToNetwork()
    {
        var network = new Network();
        var server = CreateServer(
            id: "server-home",
            hostname: "home");

        network.AddServer(server);

        Assert.Single(network.Servers);

        Assert.Same(server, network.GetServer(server.Id));
    }

    [Fact]
    public void AddServer_DuplicateIdThrows()
    {
        var network = new Network();

        network.AddServer(
            CreateServer(
                id: "server-1",
                hostname: "first"));

        var duplicate =
            CreateServer(
                id: "server-1",
                hostname: "second");

        var exception =
            Assert.Throws<InvalidOperationException>(
                () => network.AddServer(duplicate));

        Assert.Contains(
            "server-1",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);

        Assert.Single(network.Servers);
    }

    [Fact]
    public void AddServer_DuplicateHostnameThrows()
    {
        var network = new Network();

        network.AddServer(
            CreateServer(
                id: "server-1",
                hostname: "n00dles"));

        var duplicate =
            CreateServer(
                id: "server-2",
                hostname: "n00dles");

        var exception =
            Assert.Throws<InvalidOperationException>(
                () => network.AddServer(duplicate));

        Assert.Contains(
            "n00dles",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);

        Assert.Single(network.Servers);
    }

    [Fact]
    public void AddServer_DuplicateIdIsCaseInsensitive()
    {
        var network = new Network();

        network.AddServer(
            CreateServer(
                id: "SERVER-1",
                hostname: "first"));

        Assert.Throws<InvalidOperationException>(
            () => network.AddServer(
                CreateServer(
                    id: "server-1",
                    hostname: "second")));
    }

    [Fact]
    public void AddServer_DuplicateHostnameIsCaseInsensitive()
    {
        var network = new Network();

        network.AddServer(
            CreateServer(
                id: "server-1",
                hostname: "N00DLES"));

        Assert.Throws<InvalidOperationException>(
            () => network.AddServer(
                CreateServer(
                    id: "server-2",
                    hostname: "n00dles")));
    }

    [Fact]
    public void GetServer_ReturnsServerByIdCaseInsensitively()
    {
        var network = new Network();

        var server =
            CreateServer(
                id: "Server-N00dles",
                hostname: "n00dles");

        network.AddServer(server);

        var result = network.GetServer("SERVER-N00DLES");

        Assert.Same(server, result);
    }

    [Fact]
    public void GetServer_UnknownIdThrows()
    {
        var network = new Network();

        var exception =
            Assert.Throws<KeyNotFoundException>(
                () => network.GetServer(
                    "does-not-exist"));

        Assert.Contains(
            "does-not-exist",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryGetServer_UnknownIdReturnsFalse()
    {
        var network = new Network();

        var found =
            network.TryGetServer(
                "does-not-exist",
                out var server);

        Assert.False(found);
        Assert.Null(server);
    }

    [Fact]
    public void TryFindServer_FindsServerById()
    {
        var network = new Network();

        var server =
            CreateServer(
                id: "server-n00dles",
                hostname: "n00dles");

        network.AddServer(server);

        var found =
            network.TryFindServer(
                "SERVER-N00DLES",
                out var result);

        Assert.True(found);
        Assert.Same(server, result);
    }

    [Fact]
    public void TryFindServer_FindsServerByHostname()
    {
        var network = new Network();

        var server =
            CreateServer(
                id: "server-n00dles",
                hostname: "n00dles");

        network.AddServer(server);

        var found =
            network.TryFindServer(
                "N00DLES",
                out var result);

        Assert.True(found);
        Assert.Same(server, result);
    }

    [Fact]
    public void Connect_CreatesBidirectionalConnection()
    {
        var network = new Network();

        var home =
            CreateServer(
                id: "home",
                hostname: "home");

        var n00dles =
            CreateServer(
                id: "server-n00dles",
                hostname: "n00dles");

        network.AddServer(home);
        network.AddServer(n00dles);

        network.Connect(home.Id, n00dles.Id);

        Assert.True(network.AreConnected(home.Id, n00dles.Id));
        Assert.True(network.AreConnected(n00dles.Id, home.Id));
    }

    [Fact]
    public void Connect_DuplicateConnectionDoesNotCreateDuplicateNeighbor()
    {
        var network = new Network();

        var home =
            CreateServer(
                id: "home",
                hostname: "home");

        var n00dles =
            CreateServer(
                id: "server-n00dles",
                hostname: "n00dles");

        network.AddServer(home);
        network.AddServer(n00dles);

        network.Connect(home.Id, n00dles.Id);
        network.Connect(home.Id, n00dles.Id);

        var neighbors = network.GetNeighbors(home.Id);

        var neighbor = Assert.Single(neighbors);

        Assert.Same(n00dles, neighbor);
    }

    [Fact]
    public void Connect_ServerToItselfThrows()
    {
        var network = new Network();

        var home =
            CreateServer(
                id: "home",
                hostname: "home");

        network.AddServer(home);

        var exception =
            Assert.Throws<InvalidOperationException>(
                () => network.Connect(
                    home.Id,
                    home.Id));

        Assert.Contains(
            "cannot be connected to itself",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Connect_UnknownServerThrows()
    {
        var network = new Network();

        var home =
            CreateServer(
                id: "home",
                hostname: "home");

        network.AddServer(home);

        Assert.Throws<KeyNotFoundException>(
            () => network.Connect(
                home.Id,
                "does-not-exist"));
    }

    [Fact]
    public void GetNeighbors_ReturnsDirectNeighborsOnly()
    {
        var network =
            CreateLinearNetwork(
                out var home,
                out var n00dles,
                out var sigmaCosmetics);

        var neighbors = network.GetNeighbors(n00dles.Id);

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

        var neighbors = network.GetNeighbors(home.Id);

        Assert.DoesNotContain(sigmaCosmetics, neighbors);
    }

    [Fact]
    public void GetNeighbors_UnconnectedServerReturnsEmptyCollection()
    {
        var network = new Network();

        var home =
            CreateServer(
                id: "home",
                hostname: "home");

        network.AddServer(home);

        var neighbors = network.GetNeighbors(home.Id);

        Assert.Empty(neighbors);
    }

    [Fact]
    public void AreConnected_UnconnectedServersReturnsFalse()
    {
        var network = new Network();

        var first =
            CreateServer(
                id: "first",
                hostname: "first");

        var second =
            CreateServer(
                id: "second",
                hostname: "second");

        network.AddServer(first);
        network.AddServer(second);

        Assert.False(network.AreConnected(first.Id, second.Id));
    }

    private static Server CreateServer(
        string id,
        string hostname)
    {
        return new Server(
            id,
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

        home =
            CreateServer(
                id: "home",
                hostname: "home");

        n00dles =
            CreateServer(
                id: "server-n00dles",
                hostname: "n00dles");

        sigmaCosmetics =
            CreateServer(
                id: "server-sigma",
                hostname: "sigma-cosmetics");

        network.AddServer(home);
        network.AddServer(n00dles);
        network.AddServer(sigmaCosmetics);

        network.Connect(
            home.Id,
            n00dles.Id);

        network.Connect(
            n00dles.Id,
            sigmaCosmetics.Id);

        return network;
    }
}