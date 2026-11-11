namespace NullByte.Core.Domain;

/// <summary>
/// Represents the player's persistent game state.
/// </summary>
public sealed class Player
{
    private readonly HashSet<string> discoveredHostnames = new(StringComparer.OrdinalIgnoreCase);

    public Player(
        string homeHostname,
        double startingMoney = 0,
        double startingHackingExperience = 0)
    {
        if (string.IsNullOrWhiteSpace(homeHostname))
            throw new ArgumentException("The home hostname cannot be empty.", nameof(homeHostname));
        if (!double.IsFinite(startingMoney) || startingMoney < 0)
            throw new ArgumentOutOfRangeException(nameof(startingMoney), "Starting money must be a finite, non-negative value.");
        if (!double.IsFinite(startingHackingExperience) || startingHackingExperience < 0)
            throw new ArgumentOutOfRangeException(nameof(startingHackingExperience), "Starting hacking experience must be a finite, non-negative value.");

        HomeHostname = homeHostname;
        CurrentHostname = homeHostname;
        Money = startingMoney;
        HackingExperience = startingHackingExperience;

        DiscoverServer(homeHostname);
    }

    /// <summary>
    /// Gets the identifier of the player's home server.
    /// </summary>
    public string HomeHostname { get; }

    /// <summary>
    /// Gets the identifier of the server the player
    /// is currently connected to.
    /// </summary>
    public string CurrentHostname { get; internal set; }

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
    public IReadOnlySet<string> DiscoveredHostnames => discoveredHostnames;

    /// <summary>
    /// Adds hacking experience to the player.
    /// </summary>
    internal void AddHackingExperience(double amount)
    {
        if (!double.IsFinite(amount) || amount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount), "Hacking experience must be a finite, non-negative value.");
        }

        HackingExperience += amount;
    }

    internal void AddMoney(double amount)
    {
        if (!double.IsFinite(amount) || amount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount), "Money must be a finite, non-negative value.");
        }

        Money += amount;
    }

    /// <summary>
    /// Takes money from the player if they can afford it.
    /// </summary>
    /// <returns>
    /// True if the money was spent; false if the player has too little,
    /// in which case nothing changes.
    /// </returns>
    internal bool TrySpendMoney(double amount)
    {
        if (!double.IsFinite(amount) || amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Money must be a finite, non-negative value.");

        if (amount > Money)
            return false;

        Money -= amount;
        return true;
    }

    /// <summary>
    /// Marks a server as discovered.
    /// </summary>
    /// <returns>
    /// True if the server was newly discovered.
    /// </returns>
    internal bool DiscoverServer(string hostname)
    {
        if (string.IsNullOrWhiteSpace(hostname))
            throw new ArgumentException("The hostname cannot be empty.", nameof(hostname));
        return discoveredHostnames.Add(hostname);
    }
}