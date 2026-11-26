using NullByte.Core.Domain;

namespace NullByte.Core.Tests.Infrastructure;

/// <summary>
/// Servers with valid, round defaults, so a test only spells out the values it actually cares about.
/// </summary>
internal static class TestServers
{
    public const string HomeHostname = "home";

    /// <param name="startingMoney">Defaults to <paramref name="maximumMoney"/>.</param>
    public static Server Create(
        string hostname,
        double maximumMoney = 0,
        double? startingMoney = null,
        double baseDifficulty = 5,
        double minimumSecurity = 1,
        double startingSecurity = 1,
        int requiredHackingLevel = 1,
        double growthParameter = 0,
        double maximumRam = 8,
        bool hasRootAccess = false,
        bool hasBackdoor = false)
        => new(
            hostname,
            maximumMoney,
            startingMoney ?? maximumMoney,
            baseDifficulty,
            minimumSecurity,
            startingSecurity,
            requiredHackingLevel,
            growthParameter,
            maximumRam,
            hasRootAccess,
            hasBackdoor);

    /// <summary>The player's home server: rooted, with no money to steal.</summary>
    public static Server Home(string hostname = HomeHostname) => Create(hostname, hasRootAccess: true);
}
