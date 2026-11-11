using System.Diagnostics.CodeAnalysis;

namespace NullByte.Core.Domain;

/// <summary>
/// Represents the servers and topology of the game network. A server is identified
/// by its hostname everywhere (case-insensitive), exactly as the player types it.
/// </summary>
public sealed class Network
{
    private readonly Dictionary<string, Server> servers = new(StringComparer.OrdinalIgnoreCase);

    // Hostname -> hostnames of the directly connected servers.
    private readonly Dictionary<string, HashSet<string>> connections = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets all servers in the network.
    /// </summary>
    public IReadOnlyCollection<Server> Servers => servers.Values;

    /// <summary>
    /// Adds a server to the network.
    /// </summary>
    public void AddServer(Server server)
    {
        ArgumentNullException.ThrowIfNull(server);

        if (!servers.TryAdd(server.Hostname, server))
            throw new InvalidOperationException($"A server with hostname '{server.Hostname}' already exists.");

        connections.Add(server.Hostname, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
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

        connections[first.Hostname].Add(second.Hostname);
        connections[second.Hostname].Add(first.Hostname);
    }

    /// <summary>
    /// Gets a server by hostname.
    /// </summary>
    public Server GetServer(string hostname)
    {
        if (string.IsNullOrWhiteSpace(hostname))
            throw new ArgumentException("The hostname cannot be empty.", nameof(hostname));

        return servers.TryGetValue(hostname, out var server)
            ? server
            : throw new KeyNotFoundException($"Server '{hostname}' does not exist.");
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

        return servers.TryGetValue(hostname, out server);
    }

    /// <summary>
    /// Gets all servers directly connected to the specified server.
    /// </summary>
    public IReadOnlyList<Server> GetNeighbors(string hostname)
    {
        var server = GetServer(hostname);

        return connections[server.Hostname]
            .Select(neighbor => servers[neighbor])
            .ToArray();
    }

    /// <summary>
    /// Determines whether two servers are directly connected.
    /// </summary>
    public bool AreConnected(string firstHostname, string secondHostname)
    {
        var first = GetServer(firstHostname);
        var second = GetServer(secondHostname);

        return connections[first.Hostname].Contains(second.Hostname);
    }
}