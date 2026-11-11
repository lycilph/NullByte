using NullByte.Core.Domain;

namespace NullByte.Core.Services;

/// <summary>
/// Implements player-facing network navigation and discovery rules.
/// </summary>
public sealed class NetworkService
{
    private readonly GameState gameState;

    public NetworkService(GameState gameState)
    {
        this.gameState = gameState
            ?? throw new ArgumentNullException(nameof(gameState));
    }

    /// <summary>
    /// Gets the server the player is currently connected to.
    /// </summary>
    public Server GetCurrentServer() => gameState.Network.GetServer(gameState.Player.CurrentHostname);
    
    public Server GetServer(string hostname)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostname);

        gameState.Network.TryGetServer(hostname, out var server);

        return server ?? throw new InvalidOperationException($"Unknown server: {hostname}");
    }

    public bool IsServerKnown(Server server)
        => gameState.Player.DiscoveredHostnames.Contains(server.Hostname);

    /// <summary>
    /// Discovers and returns all servers directly connected
    /// to the player's current server.
    /// </summary>
    public IReadOnlyList<Server> Scan()
    {
        var neighbors = gameState.Network.GetNeighbors(gameState.Player.CurrentHostname);

        foreach (var server in neighbors)
            gameState.Player.DiscoverServer(server.Hostname);

        return neighbors;
    }

    /// <summary>
    /// Attempts to connect the player to a discovered server
    /// directly adjacent to the current server.
    /// </summary>
    public NetworkConnectionResult Connect(string serverName)
    {
        if (string.IsNullOrWhiteSpace(serverName))
            return NetworkConnectionResult.Failure("A server name is required.");

        if (!gameState.Network.TryGetServer(serverName, out var targetServer))
            return NetworkConnectionResult.Failure($"Unknown server: {serverName}");

        if (string.Equals(
                targetServer!.Id,
                gameState.Player.CurrentHostname,
                StringComparison.OrdinalIgnoreCase))
        {
            return NetworkConnectionResult.Failure(
                $"Already connected to " +
                $"{targetServer.Hostname}.");
        }

        if (!gameState.Player.DiscoveredHostnames.Contains(targetServer.Id))
        {
            return NetworkConnectionResult.Failure(
                $"Server '{targetServer.Hostname}' " +
                "has not been discovered.");
        }

        if (!gameState.Network.AreConnected(gameState.Player.CurrentHostname, targetServer.Id))
        {
            return NetworkConnectionResult.Failure(
                $"Server '{targetServer.Hostname}' is not " +
                "directly connected to the current server.");
        }

        gameState.Player.CurrentHostname = targetServer.Id;

        return NetworkConnectionResult.Success(targetServer);
    }

    /// <summary>
    /// Returns the player to their home server.
    /// </summary>
    public NetworkConnectionResult GoHome()
    {
        var homeServer = gameState.Network.GetServer(gameState.Player.HomeHostname);
        
        gameState.Player.CurrentHostname = homeServer.Hostname;

        return NetworkConnectionResult.Success(homeServer);
    }
}

/// <summary>
/// Describes the result of a player connection attempt.
/// </summary>
public sealed record NetworkConnectionResult
{
    private NetworkConnectionResult(
        bool succeeded,
        Server? server,
        string? error)
    {
        Succeeded = succeeded;
        Server = server;
        Error = error;
    }

    public bool Succeeded { get; }

    public Server? Server { get; }

    public string? Error { get; }

    public static NetworkConnectionResult Success(Server server)
    {
        ArgumentNullException.ThrowIfNull(server);

        return new NetworkConnectionResult(
            succeeded: true,
            server,
            error: null);
    }

    public static NetworkConnectionResult Failure(string error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            throw new ArgumentException(
                "An error message is required.",
                nameof(error));
        }

        return new NetworkConnectionResult(
            succeeded: false,
            server: null,
            error);
    }
}