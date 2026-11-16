using NullByte.Core.Services;

namespace NullByte.Core.Scripting;

/// <summary>
/// Defines the API exposed to player-written scripts through the global
/// <c>nb</c> object.
/// </summary>
public interface INullByteApi
{
    INullByteFilesApi Files { get; }

    /// <summary>Writes a line to the script's output.</summary>
    [RamCost(0.1)]
    void Print(object? value);

    /// <summary>Waits for the specified amount of game time.</summary>
    [RamCost(0.1)]
    Task Sleep(TimeSpan duration);

    /// <summary>Attempts to steal money from a server.</summary>
    [RamCost(1.7)]
    Task<HackResult> Hack(string hostname);

    /// <summary>Increases the available money on a server.</summary>
    [RamCost(1.7)] 
    Task<GrowResult> Grow(string hostname);

    /// <summary>Reduces the security level of a server.</summary>
    [RamCost(1.7)] 
    Task<WeakenResult> Weaken(string hostname);

    /// <summary>Gives the player root access to a server.</summary>
    [RamCost(0.05)] 
    NukeResult Nuke(string hostname);
    
    /// <summary>Scan the network and returns the neighbors of a server.</summary>
    [RamCost(0.2)]
    IReadOnlyList<string> Scan(string hostname = "");

    /// <summary>Gets the hacking level of the player.</summary>
    [RamCost(0.1)]
    double GetHackingLevel();

    /// <summary>Gets the amount of money currently available on a server.</summary>
    [RamCost(0.1)]
    double GetServerMoneyAvailable(string hostname);

    /// <summary>Gets the maximum amount of money a server can hold.</summary>
    [RamCost(0.1)]
    double GetServerMaxMoney(string hostname);

    /// <summary>Gets the current security level of a server.</summary>
    [RamCost(0.1)]
    double GetServerSecurityLevel(string hostname);

    /// <summary>Gets the minimum security level of a server.</summary>
    [RamCost(0.1)]
    double GetServerMinSecurityLevel(string hostname);

    /// <summary>Gets the required hacking level of a server.</summary>
    [RamCost(0.1)]
    double GetServerRequiredHackingLevel(string hostname);

    /// <summary>Gets the maximum ram of a server.</summary>
    [RamCost(0.1)]
    double GetServerMaxRam(string hostname);

    /// <summary>Gets the currently used ram of a server.</summary>
    [RamCost(0.1)]
    double GetServerUsedRam(string hostname);

    /// <summary>Does the player have root access to a server.</summary>
    [RamCost(0.1)]
    bool HasRootAccess(string hostname);

    /// <summary>List running scripts on a server.</summary>
    [RamCost(0.2)]
    IReadOnlyList<ScriptProcessInfo> Ps(string hostname = "");
    
    /// <summary>
    /// Starts a script on the server running the current script.
    /// Returns the new process ID, or 0 if the script could not be started.
    /// </summary>
    [RamCost(1.0)]
    Task<int> RunAsync(string filename, params string[] args);

    /// <summary>
    /// Starts a script on the specified server.
    /// Returns the new process ID, or 0 if the script could not be started.
    /// </summary>
    [RamCost(1.3)]
    Task<int> ExecAsync(string filename, string hostname, params string[] args);

    /// <summary>Kills the specified process.</summary>
    [RamCost(0.5)]
    bool Kill(int pid);

    /// <summary>Kills all processes.</summary>
    [RamCost(0.5)]
    int KillAll();

    /// <summary>Check if the specified script is running on a server.</summary>
    [RamCost(1)]
    bool ScriptRunning(string filename, string hostname = "");

    /// <summary>Get the ram cost of a script.</summary>
    [RamCost(0.1)]
    double GetScriptRam(string filename, string hostname = "");
}