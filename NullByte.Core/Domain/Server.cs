namespace NullByte.Core.Domain;

/// <summary>
/// Represents a server in the game network.
/// </summary>
public sealed class Server
{
    private double money;
    private double security;
    private double usedRam;

    public Server(
        string hostname,
        double maximumMoney,
        double startingMoney,
        double baseDifficulty,
        double minimumSecurity,
        double startingSecurity,
        int requiredHackingLevel,
        double growthParameter,
        double maximumRam,
        bool hasRootAccess = false)
    {
        if (string.IsNullOrWhiteSpace(hostname))
            throw new ArgumentException("Host name cannot be empty.", nameof(hostname));
        if (!double.IsFinite(maximumMoney) || maximumMoney < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumMoney), "Maximum money must be finite and non-negative.");
        if (!double.IsFinite(startingMoney) || startingMoney < 0 || startingMoney > maximumMoney)
            throw new ArgumentOutOfRangeException(nameof(startingMoney), "Starting money must be finite and between zero and maximum money.");
        if (!double.IsFinite(baseDifficulty) || baseDifficulty < minimumSecurity)
            throw new ArgumentOutOfRangeException(nameof(baseDifficulty), "Base difficulty must be finite and at least minimum security.");
        if (!double.IsFinite(minimumSecurity) || minimumSecurity <= 0)
            throw new ArgumentOutOfRangeException(nameof(minimumSecurity), "Minimum security must be finite and non-negative.");
        if (!double.IsFinite(startingSecurity) || startingSecurity < minimumSecurity)
            throw new ArgumentOutOfRangeException(nameof(startingSecurity), "Starting security must be finite and at least minimum security.");
        if (requiredHackingLevel < 1)
            throw new ArgumentOutOfRangeException(nameof(requiredHackingLevel), "Required hacking level must be at least 1.");
        if (!double.IsFinite(growthParameter) || growthParameter < 0)
            throw new ArgumentOutOfRangeException(nameof(growthParameter), "Growth parameter must be finite and non-negative.");
        if (!double.IsFinite(maximumRam) || maximumRam < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumRam), "Maximum RAM must be finite and non-negative.");

        Hostname = hostname;

        MaximumMoney = maximumMoney;
        money = startingMoney;

        BaseDifficulty = baseDifficulty;

        MinimumSecurity = minimumSecurity;
        security = startingSecurity;

        RequiredHackingLevel = requiredHackingLevel;
        GrowthParameter = growthParameter;

        MaximumRam = maximumRam;

        HasRootAccess = hasRootAccess;
    }

    public string Hostname { get; }
    public double MaximumMoney { get; }
    public double Money => money;
    public double BaseDifficulty { get; }
    public double MinimumSecurity { get; }
    public double Security => security;
    public int RequiredHackingLevel { get; }
    public double GrowthParameter { get; }
    public bool HasRootAccess { get; private set; }
    public double MaximumRam { get; }
    public double UsedRam => usedRam;
    public double AvailableRam => MaximumRam - usedRam;

    public bool TryGainRootAccess(int hackingLevel)
    {
        if (HasRootAccess)
            return true;

        if (hackingLevel < RequiredHackingLevel)
            return false;

        GrantRootAccess();
        return true;
    }

    internal void GrantRootAccess()
    {
        HasRootAccess = true;
    }

    internal double RemoveMoney(double amount)
    {
        ValidateNonNegativeFinite(amount, nameof(amount));

        var removed = Math.Min(amount, money);

        money -= removed;

        return removed;
    }

    internal double AddMoney(double amount)
    {
        ValidateNonNegativeFinite(amount, nameof(amount));

        var availableCapacity = MaximumMoney - money;
        var added = Math.Min(amount, availableCapacity);

        money += added;

        return added;
    }

    internal void IncreaseSecurity(double amount)
    {
        ValidateNonNegativeFinite(amount, nameof(amount));

        security += amount;
    }

    internal double DecreaseSecurity(double amount)
    {
        ValidateNonNegativeFinite(amount, nameof(amount));

        var removable = security - MinimumSecurity;
        var removed = Math.Min(amount, removable);

        security -= removed;

        return removed;
    }

    internal void AllocateRam(double amount)
    {
        ValidateNonNegativeFinite(amount, nameof(amount));

        if (amount > AvailableRam)
        {
            throw new InvalidOperationException(
                $"Server '{Hostname}' does not have enough available RAM.");
        }

        usedRam += amount;
    }

    internal void ReleaseRam(double amount)
    {
        ValidateNonNegativeFinite(amount, nameof(amount));

        if (amount > usedRam)
        {
            throw new InvalidOperationException(
                $"Cannot release more RAM than is currently allocated on server '{Hostname}'.");
        }

        usedRam -= amount;
    }

    private static void ValidateNonNegativeFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value < 0)
            throw new ArgumentOutOfRangeException(parameterName, "Value must be finite and non-negative.");
    }
}