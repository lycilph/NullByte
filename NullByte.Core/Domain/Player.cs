namespace NullByte.Core.Domain;

/// <summary>
/// Represents the player's persistent game state.
/// </summary>
public sealed class Player
{
    private readonly HashSet<string> discoveredServerIds = new(StringComparer.OrdinalIgnoreCase);

    public Player(string homeServerId, double startingMoney = 0)
    {
        if (string.IsNullOrWhiteSpace(homeServerId))
        {
            throw new ArgumentException(
                "The home server ID cannot be empty.",
                nameof(homeServerId));
        }

        if (!double.IsFinite(startingMoney) ||
            startingMoney < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(startingMoney),
                "Starting money must be a finite, " +
                "non-negative value.");
        }

        HomeServerId = homeServerId;
        CurrentServerId = homeServerId;
        Money = startingMoney;

        DiscoverServer(homeServerId);
    }

    /// <summary>
    /// Gets the identifier of the player's home server.
    /// </summary>
    public string HomeServerId { get; }

    /// <summary>
    /// Gets the identifier of the server the player
    /// is currently connected to.
    /// </summary>
    public string CurrentServerId { get; internal set; }

    /// <summary>
    /// Gets the player's current money.
    /// </summary>
    public double Money { get; internal set; }

    /// <summary>
    /// Gets the IDs of servers known to the player.
    /// </summary>
    public IReadOnlySet<string> DiscoveredServerIds => discoveredServerIds;

    /// <summary>
    /// Marks a server as discovered.
    /// </summary>
    /// <returns>
    /// True if the server was newly discovered.
    /// </returns>
    internal bool DiscoverServer(string serverId)
    {
        if (string.IsNullOrWhiteSpace(serverId))
            throw new ArgumentException("The server ID cannot be empty.", nameof(serverId));
        return discoveredServerIds.Add(serverId);
    }
}