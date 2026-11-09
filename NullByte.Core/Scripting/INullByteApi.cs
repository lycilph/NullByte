using NullByte.Core.Services;

namespace NullByte.Core.Scripting;

/// <summary>
/// Defines the API exposed to player-written scripts through the global
/// <c>nb</c> object.
/// </summary>
public interface INullByteApi
{
    /// <summary>
    /// Writes a line to the script's output.
    /// </summary>
    void Print(object? value);

    /// <summary>
    /// Waits for the specified amount of game time.
    /// </summary>
    Task Sleep(TimeSpan duration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to steal money from a server.
    /// </summary>
    Task<HackResult> Hack(string hostname, CancellationToken cancellationToken = default);

    /// <summary>
    /// Increases the available money on a server.
    /// </summary>
    Task<GrowResult> Grow(string hostname, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reduces the security level of a server.
    /// </summary>
    Task<WeakenResult> Weaken(string hostname, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the amount of money currently available on a server.
    /// </summary>
    double GetServerMoneyAvailable(string hostname);

    /// <summary>
    /// Gets the maximum amount of money a server can hold.
    /// </summary>
    double GetServerMaxMoney(string hostname);

    /// <summary>
    /// Gets the current security level of a server.
    /// </summary>
    double GetServerSecurityLevel(string hostname);

    /// <summary>
    /// Gets the minimum security level of a server.
    /// </summary>
    double GetServerMinSecurityLevel(string hostname);
}