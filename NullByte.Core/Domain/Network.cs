using System.Diagnostics.CodeAnalysis;

namespace NullByte.Core.Domain;

/// <summary>
/// Represents the servers and topology of the game network. Servers have immutable IDs
/// and are also indexed by their player-facing hostname (case-insensitive).
/// </summary>
public sealed class Network
{
    private readonly Dictionary<string, Server> serversByHostname = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<ServerId, Server> serversById = new();

    // Hostname -> hostnames of the directly connected servers. This remains hostname-based
    // until the internal-reference migration in the next implementation step.
    private readonly Dictionary<ServerId, HashSet<ServerId>> connections = [];

    /// <summary>
    /// Gets all servers in the network.
    /// </summary>
    public IReadOnlyCollection<Server> Servers => serversByHostname.Values;

    /// <summary>
    /// Adds a server to the network.
    /// </summary>
    public void AddServer(Server server)
    {
        ArgumentNullException.ThrowIfNull(server);

        if (serversById.ContainsKey(server.Id))
            throw new InvalidOperationException($"A server with ID '{server.Id}' is already registered.");

        if (!serversByHostname.TryAdd(server.Hostname, server))
            throw new InvalidOperationException($"A server with hostname '{server.Hostname}' already exists.");

        serversById.Add(server.Id, server);
        connections.Add(server.Id, []);
    }

    /// <summary>
    /// Creates a bidirectional connection between two servers.
    /// </summary>
    public void Connect(string firstHostname, string secondHostname)
    {
        var first = GetServer(firstHostname);
        var second = GetServer(secondHostname);

        if (ReferenceEquals(first, second))
            throw new InvalidOperationException("A server cannot be connected to itself.");

        connections[first.Id].Add(second.Id);
        connections[second.Id].Add(first.Id);
    }

    /// <summary>
    /// Gets a server by hostname.
    /// </summary>
    public Server GetServer(string hostname)
    {
        if (string.IsNullOrWhiteSpace(hostname))
            throw new ArgumentException("The hostname cannot be empty.", nameof(hostname));

        return serversByHostname.TryGetValue(hostname, out var server)
            ? server
            : throw new KeyNotFoundException($"Server '{hostname}' does not exist.");
    }

    /// <summary>
    /// Gets a server by its immutable ID.
    /// </summary>
    public Server GetServer(ServerId id)
    {
        return serversById.TryGetValue(id, out var server)
            ? server
            : throw new KeyNotFoundException($"Server with ID '{id}' does not exist.");
    }

    /// <summary>
    /// Attempts to get a server by hostname.
    /// </summary>
    public bool TryGetServer(string hostname, [NotNullWhen(true)] out Server? server)
    {
        if (string.IsNullOrWhiteSpace(hostname))
        {
            server = null;
            return false;
        }

        return serversByHostname.TryGetValue(hostname, out server);
    }

    /// <summary>
    /// Attempts to get a server by its immutable ID.
    /// </summary>
    public bool TryGetServer(ServerId id, [NotNullWhen(true)] out Server? server)
    {
        return serversById.TryGetValue(id, out server);
    }

    /// <summary>
    /// Gets all servers directly connected to the specified server.
    /// </summary>
    public IReadOnlyList<Server> GetNeighbors(string hostname)
    {
        var server = GetServer(hostname);

        return connections[server.Id]
            .Select(neighbor => serversById[neighbor])
            .OrderBy(server => server.Hostname, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Determines whether two servers are directly connected.
    /// </summary>
    public bool AreConnected(string firstHostname, string secondHostname)
    {
        var first = GetServer(firstHostname);
        var second = GetServer(secondHostname);

        return connections[first.Id].Contains(second.Id);
    }

    public IReadOnlyList<(string First, string Second)> GetConnections()
    {
        var result = new List<(string First, string Second)>();

        foreach (var connection in connections)
        {
            var hostname = serversById[connection.Key].Hostname;
            foreach (var target in connection.Value)
                result.Add((hostname, serversById[target].Hostname));
        }

        return result;
    }
}
