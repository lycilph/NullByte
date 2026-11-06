namespace NullByte.Core.Domain;

/// <summary>
/// Represents the servers and topology of the game network.
/// </summary>
public sealed class Network
{
    private readonly Dictionary<string, Server> serversById =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, Server> serversByHostname =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, HashSet<string>> connections =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets all servers in the network.
    /// </summary>
    public IReadOnlyCollection<Server> Servers => serversById.Values;

    /// <summary>
    /// Adds a server to the network.
    /// </summary>
    public void AddServer(Server server)
    {
        ArgumentNullException.ThrowIfNull(server);

        if (serversById.ContainsKey(server.Id))
        {
            throw new InvalidOperationException(
                $"A server with ID '{server.Id}' " +
                "already exists.");
        }

        if (serversByHostname.ContainsKey(server.Hostname))
        {
            throw new InvalidOperationException(
                $"A server with hostname '{server.Hostname}' " +
                "already exists.");
        }

        serversById.Add(server.Id, server);
        serversByHostname.Add(server.Hostname, server);

        connections.Add(server.Id, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Creates a bidirectional connection between two servers.
    /// </summary>
    public void Connect(string firstServerId, string secondServerId)
    {
        var firstServer = GetServer(firstServerId);
        var secondServer = GetServer(secondServerId);

        if (ReferenceEquals(firstServer, secondServer))
            throw new InvalidOperationException("A server cannot be connected to itself.");

        connections[firstServer.Id].Add(secondServer.Id);
        connections[secondServer.Id].Add(firstServer.Id);
    }

    /// <summary>
    /// Gets a server by its stable ID.
    /// </summary>
    public Server GetServer(string serverId)
    {
        if (string.IsNullOrWhiteSpace(serverId))
            throw new ArgumentException("The server ID cannot be empty.", nameof(serverId));

        if (!serversById.TryGetValue(serverId, out var server))
            throw new KeyNotFoundException($"Server '{serverId}' does not exist.");

        return server;
    }

    /// <summary>
    /// Attempts to get a server by its stable ID.
    /// </summary>
    public bool TryGetServer(string serverId, out Server? server)
    {
        if (string.IsNullOrWhiteSpace(serverId))
        {
            server = null;
            return false;
        }

        return serversById.TryGetValue(serverId, out server);
    }

    /// <summary>
    /// Attempts to find a server by either its ID or hostname.
    /// </summary>
    public bool TryFindServer(string name, out Server? server)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            server = null;
            return false;
        }

        if (serversById.TryGetValue(name, out server))
            return true;

        return serversByHostname.TryGetValue(name, out server);
    }

    /// <summary>
    /// Gets all servers directly connected to the specified server.
    /// </summary>
    public IReadOnlyList<Server> GetNeighbors(string serverId)
    {
        var server = GetServer(serverId);

        return connections[server.Id]
            .Select(GetServer)
            .ToArray();
    }

    /// <summary>
    /// Determines whether two servers are directly connected.
    /// </summary>
    public bool AreConnected(string firstServerId, string secondServerId)
    {
        var firstServer = GetServer(firstServerId);
        var secondServer = GetServer(secondServerId);

        return connections[firstServer.Id]
            .Contains(secondServer.Id);
    }
}