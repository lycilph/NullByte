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
        Assert.Same(server, network.GetServer(server.Id));
    }

    [Fact]
    public void AddServer_DuplicateHostnameThrows()
    {
        var network = new Network();
        network.AddServer(CreateServer("n00dles"));
        var duplicate = CreateServer("n00dles");
        var exception = Assert.Throws<InvalidOperationException>(() => network.AddServer(duplicate));
        Assert.Contains("n00dles", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(network.Servers);
    }

    [Fact]
    public void AddServer_DuplicateHostnameIsCaseInsensitive()
    {
        var network = new Network();
        network.AddServer(CreateServer("N00DLES"));
        Assert.Throws<InvalidOperationException>(() => network.AddServer(CreateServer("n00dles")));
    }

    [Fact]
    public void AddServer_DuplicateIdThrows()
    {
        var network = new Network();
        var id = ServerId.New();
        network.AddServer(CreateServer("first", id));
        var duplicate = CreateServer("second", id);

        var exception = Assert.Throws<InvalidOperationException>(() => network.AddServer(duplicate));

        Assert.Contains(id.ToString(), exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(network.Servers);
    }

    [Fact]
    public void RenameServer_CloudServerUpdatesHostnameLookupAndPreservesIdentityAndConnections()
    {
        var network = new Network();
        var home = CreateServer("home");
        var cloud = CreateServer("cloud-old", isCloudServer: true);
        network.AddServer(home);
        network.AddServer(cloud);
        network.Connect(home.Id, cloud.Id);
        cloud.AllocateRam(3);
        cloud.InstallBackdoor();

        network.RenameServer(cloud.Id, "cloud-new");

        Assert.Equal("cloud-new", cloud.Hostname);
        Assert.Same(cloud, network.GetServer("cloud-new"));
        Assert.Same(cloud, network.GetServer(cloud.Id));
        Assert.Throws<KeyNotFoundException>(() => network.GetServer("cloud-old"));
        Assert.Equal(cloud.Id, network.GetServer("cloud-new").Id);
        Assert.True(network.AreConnected(home.Id, cloud.Id));
        Assert.Equal(3, cloud.UsedRam);
        Assert.True(cloud.HasBackdoor);
    }

    [Fact]
    public void RenameServer_NonCloudServerThrowsWithoutChangingHostname()
    {
        var network = new Network();
        var server = CreateServer("n00dles");
        network.AddServer(server);

        Assert.Throws<InvalidOperationException>(() => network.RenameServer(server.Id, "renamed"));

        Assert.Equal("n00dles", server.Hostname);
        Assert.Same(server, network.GetServer("n00dles"));
    }

    [Fact]
    public void RenameServer_DuplicateHostnameThrowsWithoutChangingIndexes()
    {
        var network = new Network();
        var cloud = CreateServer("cloud-old", isCloudServer: true);
        var existing = CreateServer("existing");
        network.AddServer(cloud);
        network.AddServer(existing);

        Assert.Throws<InvalidOperationException>(() => network.RenameServer(cloud.Id, "EXISTING"));

        Assert.Equal("cloud-old", cloud.Hostname);
        Assert.Same(cloud, network.GetServer("cloud-old"));
        Assert.Same(existing, network.GetServer("existing"));
    }

    [Fact]
    public void RenameServer_CaseOnlyRenameUpdatesHostnameIndex()
    {
        var network = new Network();
        var cloud = CreateServer("cloud-old", isCloudServer: true);
        network.AddServer(cloud);

        network.RenameServer(cloud.Id, "CLOUD-OLD");

        Assert.Equal("CLOUD-OLD", cloud.Hostname);
        Assert.Same(cloud, network.GetServer("cloud-old"));
    }

    [Fact]
    public void RenameServer_EmptyHostnameThrowsWithoutChangingHostname()
    {
        var network = new Network();
        var cloud = CreateServer("cloud-old", isCloudServer: true);
        network.AddServer(cloud);

        Assert.Throws<ArgumentException>(() => network.RenameServer(cloud.Id, "  "));

        Assert.Equal("cloud-old", cloud.Hostname);
        Assert.Same(cloud, network.GetServer("cloud-old"));
    }

    [Fact]
    public void GetServer_ReturnsServerByHostnameCaseInsensitively()
    {
        var network = new Network();
        var server = CreateServer("n00dles");
        network.AddServer(server);
        Assert.Same(server, network.GetServer("N00DLES"));
    }

    [Fact]
    public void GetServer_ReturnsServerById()
    {
        var network = new Network();
        var server = CreateServer("n00dles");
        network.AddServer(server);
        Assert.Same(server, network.GetServer(server.Id));
    }

    [Fact]
    public void GetServer_UnknownHostnameThrows()
    {
        var network = new Network();
        var exception = Assert.Throws<KeyNotFoundException>(() => network.GetServer("does-not-exist"));
        Assert.Contains("does-not-exist", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetServer_UnknownIdThrows()
    {
        var network = new Network();
        Assert.Throws<KeyNotFoundException>(() => network.GetServer(ServerId.New()));
    }

    [Fact]
    public void TryGetServer_UnknownHostnameReturnsFalse()
    {
        var network = new Network();
        var found = network.TryGetServer("does-not-exist", out var server);
        Assert.False(found);
        Assert.Null(server);
    }

    [Fact]
    public void TryGetServer_UnknownIdReturnsFalse()
    {
        var network = new Network();
        var found = network.TryGetServer(ServerId.New(), out var server);
        Assert.False(found);
        Assert.Null(server);
    }

    [Fact]
    public void Connect_CreatesBidirectionalConnection()
    {
        var network = new Network();
        var home = CreateServer("home");
        var n00dles = CreateServer("n00dles");
        network.AddServer(home);
        network.AddServer(n00dles);
        network.Connect(home.Hostname, n00dles.Hostname);
        Assert.True(network.AreConnected(home.Hostname, n00dles.Hostname));
        Assert.True(network.AreConnected(n00dles.Hostname, home.Hostname));
    }

    [Fact]
    public void GetNeighbors_ByServerIdReturnsDirectNeighbors()
    {
        var network = CreateLinearNetwork(out var home, out _, out _);

        var neighbor = Assert.Single(network.GetNeighbors(home.Id));

        Assert.Equal("n00dles", neighbor.Hostname);
    }

    [Fact]
    public void AreConnected_ByServerIdReturnsConnectionState()
    {
        var network = CreateLinearNetwork(out var home, out var n00dles, out var sigmaCosmetics);

        Assert.True(network.AreConnected(home.Id, n00dles.Id));
        Assert.False(network.AreConnected(home.Id, sigmaCosmetics.Id));
    }

    [Fact]
    public void Connect_DuplicateConnectionDoesNotCreateDuplicateNeighbor()
    {
        var network = new Network();
        var home = CreateServer("home");
        var n00dles = CreateServer("n00dles");
        network.AddServer(home);
        network.AddServer(n00dles);
        network.Connect(home.Hostname, n00dles.Hostname);
        network.Connect(home.Hostname, n00dles.Hostname);
        var neighbor = Assert.Single(network.GetNeighbors(home.Hostname));
        Assert.Same(n00dles, neighbor);
    }

    [Fact]
    public void Connect_ServerToItselfThrows()
    {
        var network = new Network();
        var home = CreateServer("home");
        network.AddServer(home);
        var exception = Assert.Throws<InvalidOperationException>(() => network.Connect(home.Hostname, home.Hostname));
        Assert.Contains("cannot be connected to itself", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Connect_UnknownServerThrows()
    {
        var network = new Network();
        var home = CreateServer("home");
        network.AddServer(home);
        Assert.Throws<KeyNotFoundException>(() => network.Connect(home.Hostname, "does-not-exist"));
    }

    [Fact]
    public void GetNeighbors_ReturnsDirectNeighborsOnly()
    {
        var network = CreateLinearNetwork(out _, out var n00dles, out var sigmaCosmetics);
        var neighbors = network.GetNeighbors(n00dles.Hostname);
        Assert.Equal(2, neighbors.Count);
        Assert.Contains(neighbors, server => server.Hostname == "home");
        Assert.Contains(sigmaCosmetics, neighbors);
    }

    [Fact]
    public void GetNeighbors_DoesNotReturnIndirectNeighbors()
    {
        var network = CreateLinearNetwork(out var home, out _, out var sigmaCosmetics);
        Assert.DoesNotContain(sigmaCosmetics, network.GetNeighbors(home.Hostname));
    }

    [Fact]
    public void GetNeighbors_UnconnectedServerReturnsEmptyCollection()
    {
        var network = new Network();
        var home = CreateServer("home");
        network.AddServer(home);
        Assert.Empty(network.GetNeighbors(home.Hostname));
    }

    [Fact]
    public void AreConnected_UnconnectedServersReturnsFalse()
    {
        var network = new Network();
        var first = CreateServer("first");
        var second = CreateServer("second");
        network.AddServer(first);
        network.AddServer(second);
        Assert.False(network.AreConnected(first.Hostname, second.Hostname));
    }

    private static Server CreateServer(string hostname, ServerId? id = null, bool isCloudServer = false)
    {
        return new Server(hostname, 100_000, 50_000, 5, 1, 5, 1, 1.1, 32, id: id, isCloudServer: isCloudServer);
    }

    private static Network CreateLinearNetwork(out Server home, out Server n00dles, out Server sigmaCosmetics)
    {
        var network = new Network();
        home = CreateServer("home");
        n00dles = CreateServer("n00dles");
        sigmaCosmetics = CreateServer("sigma-cosmetics");
        network.AddServer(home);
        network.AddServer(n00dles);
        network.AddServer(sigmaCosmetics);
        network.Connect(home.Hostname, n00dles.Hostname);
        network.Connect(n00dles.Hostname, sigmaCosmetics.Hostname);
        return network;
    }
}
