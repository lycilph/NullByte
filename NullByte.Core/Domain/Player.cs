using System.Diagnostics;

namespace NullByte.Core.Domain;

/// <summary>Represents the player's persistent game state.</summary>
[DebuggerDisplay("Home: {HomeServerId}, Current: {CurrentServerId}, Money: {Money}, Experience: {HackingExperience}")]
public sealed class Player
{
    private readonly HashSet<ServerId> discoveredServerIds = [];

    public event Action<ServerId>? ServerDiscovered;

    public Player(ServerId homeServerId, double startingMoney = 0, double startingHackingExperience = 0)
    {
        if (!homeServerId.IsValid)
            throw new ArgumentException("The home server ID must be valid.", nameof(homeServerId));
        if (!double.IsFinite(startingMoney) || startingMoney < 0)
            throw new ArgumentOutOfRangeException(nameof(startingMoney), "Starting money must be a finite, non-negative value.");
        if (!double.IsFinite(startingHackingExperience) || startingHackingExperience < 0)
            throw new ArgumentOutOfRangeException(nameof(startingHackingExperience), "Starting hacking experience must be a finite, non-negative value.");

        HomeServerId = homeServerId;
        CurrentServerId = homeServerId;
        Money = startingMoney;
        Skills.AddExperience(SkillKind.Hacking, startingHackingExperience);
        DiscoverServer(homeServerId);
    }

    public ServerId HomeServerId { get; }
    public ServerId CurrentServerId { get; internal set; }
    public IReadOnlySet<ServerId> DiscoveredServerIds => discoveredServerIds;
    public string CurrentDirectory { get; internal set; } = "";
    public double Money { get; private set; }
    public PlayerSkills Skills { get; } = new();
    public double HackingExperience => Skills.GetExperience(SkillKind.Hacking);

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

    internal bool DiscoverServer(ServerId serverId)
    {
        if (!serverId.IsValid)
            throw new ArgumentException("The server ID must be valid.", nameof(serverId));
        if (!discoveredServerIds.Add(serverId))
            return false;

        ServerDiscovered?.Invoke(serverId);
        return true;
    }
}
