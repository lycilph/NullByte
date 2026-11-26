using NullByte.Core.Domain;
using NullByte.Core.Formulas;
using NullByte.Core.Time;

namespace NullByte.Core.Services;

/// <summary>
/// Implements player-facing network navigation and discovery rules.
/// </summary>
public sealed class NetworkService
{
    private readonly GameState gameState;
    private readonly GameScheduler scheduler;
    private readonly NetworkFormulas networkFormulas;

    public NetworkService(GameState gameState, GameScheduler scheduler, NetworkFormulas networkFormulas)
    {
        this.gameState = gameState
            ?? throw new ArgumentNullException(nameof(gameState));
        this.scheduler = scheduler
            ?? throw new ArgumentNullException(nameof(scheduler));
        this.networkFormulas = networkFormulas
            ?? throw new ArgumentNullException(nameof(networkFormulas));
    }

    /// <summary>
    /// Gets the server the player is currently connected to.
    /// </summary>
    public Server GetCurrentServer() => gameState.Network.GetServer(gameState.Player.CurrentServerId);

    private void SetCurrentServer(ServerId serverId)
    {
        var player = gameState.Player;
        if (player.CurrentServerId != serverId)
            player.CurrentDirectory = "";
        player.CurrentServerId = serverId;
    }

    public Server GetServer(string hostname)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostname);

        gameState.Network.TryGetServer(hostname, out var server);

        return server ?? throw new InvalidOperationException($"Unknown server: {hostname}");
    }

    public bool IsServerKnown(string hostname)
    {
        return gameState.Network.TryGetServer(hostname, out var server) &&
               gameState.Player.DiscoveredServerIds.Contains(server.Id);
    }

    /// <summary>
    /// Discovers and returns all servers directly connected
    /// to the player's current server.
    /// </summary>
    public IReadOnlyList<Server> Scan(string hostname)
    {
        var server = ResolveTarget(hostname);

        if (!gameState.Player.DiscoveredServerIds.Contains(server.Id))
            return [];

        var neighbors = gameState.Network.GetNeighbors(server.Id);

        foreach (var neighbor in neighbors)
            gameState.Player.DiscoverServer(neighbor.Id);

        return neighbors;
    }

    /// <summary>
    /// Analyze a server and return information about that server.
    /// </summary>
    public async Task<NetworkAnalyzeResult> AnalyzeAsync(string hostname, CancellationToken cancellationToken = default)
    {
        var server = ResolveTarget(hostname);

        if (!IsServerKnown(server.Hostname))
            return NetworkAnalyzeResult.Failed($"Unknown server: {server.Hostname}");

        var duration = networkFormulas.CalculateAnalyzeDuration();
        await scheduler.DelayAsync(duration, cancellationToken);

        return new NetworkAnalyzeResult(
            true,
            "",
            server.Hostname,
            server.HasRootAccess,
            server.HasBackdoor,
            server.RequiredHackingLevel,
            server.Money,
            server.MaximumMoney,
            server.Security,
            server.MinimumSecurity,
            server.GrowthParameter,
            server.UsedRam,
            server.MaximumRam);
    }

    /// <summary>
    /// Attempts to connect the player to a discovered server that is directly
    /// adjacent to the current server, or that has a backdoor.
    /// </summary>
    public NetworkConnectionResult Connect(string serverName)
    {
        if (string.IsNullOrWhiteSpace(serverName))
            return NetworkConnectionResult.Failure("A server name is required.");
        if (!gameState.Network.TryGetServer(serverName, out var targetServer))
            return NetworkConnectionResult.Failure($"Unknown server: {serverName}");
        if (targetServer!.Id == gameState.Player.CurrentServerId)
            return NetworkConnectionResult.Failure($"Already connected to {targetServer.Hostname}.");
        if (!gameState.Player.DiscoveredServerIds.Contains(targetServer.Id))
            return NetworkConnectionResult.Failure($"Server '{targetServer.Hostname}' has not been discovered.");
        // A backdoor lets the player connect directly, so only other servers have to be adjacent.
        if (!targetServer.HasBackdoor && !gameState.Network.AreConnected(gameState.Player.CurrentServerId, targetServer.Id))
            return NetworkConnectionResult.Failure($"Server '{targetServer.Hostname}' is not directly connected to the current server.");

        SetCurrentServer(targetServer.Id);

        return NetworkConnectionResult.Success(targetServer);
    }

    /// <summary>
    /// Returns the player to their home server.
    /// </summary>
    public NetworkConnectionResult GoHome()
    {
        var homeServer = gameState.Network.GetServer(gameState.Player.HomeServerId);
        
        SetCurrentServer(homeServer.Id);

        return NetworkConnectionResult.Success(homeServer);
    }

    private Server ResolveTarget(string hostname)
    {
        if (string.IsNullOrWhiteSpace(hostname))
            throw new ArgumentException("The target hostname cannot be empty.", nameof(hostname));

        return gameState.Network.GetServer(hostname);
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