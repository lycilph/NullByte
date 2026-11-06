namespace NullByte.Core.Domain;

/// <summary>
/// Represents the player's persistent game state.
/// </summary>
public sealed class Player
{
    private readonly HashSet<string> discoveredServerIds = new(StringComparer.OrdinalIgnoreCase);

    public Player(
        string homeServerId,
        double startingMoney = 0,
        double startingHackingExperience = 0)
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

        if (!double.IsFinite(startingHackingExperience) ||
            startingHackingExperience < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(startingHackingExperience),
                "Starting hacking experience must be a finite, " +
                "non-negative value.");
        }

        HomeServerId = homeServerId;
        CurrentServerId = homeServerId;
        Money = startingMoney;
        HackingExperience = startingHackingExperience;

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
    /// Gets the player's accumulated hacking experience.
    /// </summary>
    public double HackingExperience { get; private set; }

    /// <summary>
    /// Gets the IDs of servers known to the player.
    /// </summary>
    public IReadOnlySet<string> DiscoveredServerIds => discoveredServerIds;

    /// <summary>
    /// Adds hacking experience to the player.
    /// </summary>
    internal void AddHackingExperience(double amount)
    {
        if (!double.IsFinite(amount) || amount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Hacking experience must be a finite, non-negative value.");
        }

        HackingExperience += amount;
    }

    internal void AddMoney(double amount)
    {
        if (!double.IsFinite(amount) || amount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Hacking experience must be a finite, non-negative value.");
        }

        Money += amount;
    }
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