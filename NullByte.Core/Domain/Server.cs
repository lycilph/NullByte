namespace NullByte.Core.Domain;

/// <summary>
/// Represents a server in the game network.
/// </summary>
public sealed class Server
{
    public Server(
        string id,
        string hostname,
        double maximumMoney,
        double minimumSecurity,
        double startingMoney,
        double startingSecurity)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException(
                "The server ID cannot be empty.",
                nameof(id));
        }

        if (string.IsNullOrWhiteSpace(hostname))
        {
            throw new ArgumentException(
                "The hostname cannot be empty.",
                nameof(hostname));
        }

        if (!double.IsFinite(maximumMoney) ||
            maximumMoney < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumMoney),
                "Maximum money must be a finite, " +
                "non-negative value.");
        }

        if (!double.IsFinite(startingMoney) ||
            startingMoney < 0 ||
            startingMoney > maximumMoney)
        {
            throw new ArgumentOutOfRangeException(
                nameof(startingMoney),
                "Starting money must be finite and between " +
                "zero and maximum money.");
        }

        if (!double.IsFinite(minimumSecurity) ||
            minimumSecurity < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumSecurity),
                "Minimum security must be a finite, " +
                "non-negative value.");
        }

        if (!double.IsFinite(startingSecurity) ||
            startingSecurity < minimumSecurity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(startingSecurity),
                "Starting security must be finite and greater " +
                "than or equal to minimum security.");
        }

        Id = id;
        Hostname = hostname;

        MaximumMoney = maximumMoney;
        Money = startingMoney;

        MinimumSecurity = minimumSecurity;
        Security = startingSecurity;
    }

    /// <summary>
    /// Gets the stable identifier of the server.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the hostname presented to the player.
    /// </summary>
    public string Hostname { get; }

    /// <summary>
    /// Gets the maximum amount of money the server can hold.
    /// </summary>
    public double MaximumMoney { get; }

    /// <summary>
    /// Gets the server's current available money.
    /// </summary>
    public double Money { get; internal set; }

    /// <summary>
    /// Gets the lowest security level the server can reach.
    /// </summary>
    public double MinimumSecurity { get; }

    /// <summary>
    /// Gets the server's current security level.
    /// </summary>
    public double Security { get; internal set; }
}