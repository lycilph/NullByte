using System.Diagnostics;

namespace NullByte.Core.Domain;

/// <summary>
/// Represents the player's persistent game state. Server relationships are retained by immutable IDs;
/// hostnames are resolved through the attached network for player-facing access.
/// </summary>
[DebuggerDisplay("Home: {HomeHostname}, Money: {Money}, Experience: {HackingExperience}")]
public sealed class Player
{
    private readonly HashSet<string> discoveredHostnames = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<ServerId> discoveredServerIds = [];
    private Network? network;
    private string homeHostname;
    private string currentHostname;

    public event Action<string>? ServerDiscovered;

    public Player(string homeHostname, double startingMoney = 0, double startingHackingExperience = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(homeHostname);
        if (!double.IsFinite(startingMoney) || startingMoney < 0)
            throw new ArgumentOutOfRangeException(nameof(startingMoney), "Starting money must be a finite, non-negative value.");
        if (!double.IsFinite(startingHackingExperience) || startingHackingExperience < 0)
            throw new ArgumentOutOfRangeException(nameof(startingHackingExperience), "Starting hacking experience must be a finite, non-negative value.");

        this.homeHostname = homeHostname;
        currentHostname = homeHostname;
        Money = startingMoney;
        Skills.AddExperience(SkillKind.Hacking, startingHackingExperience);
        DiscoverServer(homeHostname);
    }

    /// <summary>Gets the immutable identity of the player's home server.</summary>
    public ServerId HomeServerId { get; private set; }

    /// <summary>Gets the immutable identity of the server the player is currently connected to.</summary>
    public ServerId CurrentServerId { get; internal set; }

    public string HomeHostname => network is null ? homeHostname : network.GetServer(HomeServerId).Hostname;

    public string CurrentHostname
    {
        get => network is null ? currentHostname : network.GetServer(CurrentServerId).Hostname;
        internal set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            var serverId = network?.GetServer(value).Id;
            currentHostname = value;
            if (serverId.HasValue)
                CurrentServerId = serverId.Value;
        }
    }

    public IReadOnlySet<string> DiscoveredHostnames => network is null
        ? discoveredHostnames
        : discoveredServerIds.Select(id => network.GetServer(id).Hostname)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the immutable identities of servers known to the player.</summary>
    public IReadOnlySet<ServerId> DiscoveredServerIds => discoveredServerIds;

    public string CurrentDirectory { get; internal set; } = "";
    public double Money { get; private set; }
    public PlayerSkills Skills { get; } = new();
    public double HackingExperience => Skills.GetExperience(SkillKind.Hacking);

    internal void BindNetwork(Network value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (network is not null && !ReferenceEquals(network, value))
            throw new InvalidOperationException("The player is already bound to a different network.");

        var home = value.GetServer(homeHostname);
        var current = value.GetServer(currentHostname);
        var discovered = discoveredHostnames.Select(hostname => value.GetServer(hostname).Id).ToArray();

        network = value;
        HomeServerId = home.Id;
        CurrentServerId = current.Id;
        discoveredServerIds.Clear();
        foreach (var id in discovered)
            discoveredServerIds.Add(id);
    }

    public void AddHackingExperience(double amount) => Skills.AddExperience(SkillKind.Hacking, amount);

    public void AddMoney(double amount)
    {
        if (!double.IsFinite(amount) || amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Money must be a finite, non-negative value.");
        Money += amount;
    }

    public bool TrySpendMoney(double amount)
    {
        if (!double.IsFinite(amount) || amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Money must be a finite, non-negative value.");
        if (amount > Money)
            return false;
        Money -= amount;
        return true;
    }

    public bool DiscoverServer(string hostname)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostname);
        if (network is null)
        {
            if (!discoveredHostnames.Add(hostname))
                return false;
            ServerDiscovered?.Invoke(hostname);
            return true;
        }

        return DiscoverServer(network.GetServer(hostname).Id);
    }

    internal bool DiscoverServer(ServerId serverId)
    {
        if (network is null)
            throw new InvalidOperationException("The player must be bound to a network before discovering a server by ID.");

        var hostname = network.GetServer(serverId).Hostname;
        if (!discoveredServerIds.Add(serverId))
            return false;

        ServerDiscovered?.Invoke(hostname);
        return true;
    }
}
