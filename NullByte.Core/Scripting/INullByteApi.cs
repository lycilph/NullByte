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
    [RamCost(0.1)]
    void Print(object? value);

    /// <summary>
    /// Waits for the specified amount of game time.
    /// </summary>
    [RamCost(0.1)]
    Task Sleep(TimeSpan duration);

    /// <summary>
    /// Attempts to steal money from a server.
    /// </summary>
    [RamCost(1.7)]
    Task<HackResult> Hack(string hostname);

    /// <summary>
    /// Increases the available money on a server.
    /// </summary>
    [RamCost(1.7)] 
    Task<GrowResult> Grow(string hostname);

    /// <summary>
    /// Reduces the security level of a server.
    /// </summary>
    [RamCost(1.7)] 
    Task<WeakenResult> Weaken(string hostname);

    /// <summary>
    /// Gets the amount of money currently available on a server.
    /// </summary>
    [RamCost(0.1)]
    double GetServerMoneyAvailable(string hostname);

    /// <summary>
    /// Gets the maximum amount of money a server can hold.
    /// </summary>
    [RamCost(0.1)]
    double GetServerMaxMoney(string hostname);

    /// <summary>
    /// Gets the current security level of a server.
    /// </summary>
    [RamCost(0.1)]
    double GetServerSecurityLevel(string hostname);

    /// <summary>
    /// Gets the minimum security level of a server.
    /// </summary>
    [RamCost(0.1)]
    double GetServerMinSecurityLevel(string hostname);
}