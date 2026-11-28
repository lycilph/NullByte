using NullByte.Core.Domain;

namespace NullByte.Scripting.Tests.Infrastructure;

/// <summary>Servers with sensible defaults, so a test only spells out what it cares about.</summary>
internal static class TestServers
{
    public static Server Create(
        string hostname,
        double maximumMoney = 0,
        double startingMoney = 0,
        double growthParameter = 0,
        double maximumRam = 8,
        bool hasRootAccess = true,
        bool isCloudServer = false)
        => new(
            hostname,
            maximumMoney,
            startingMoney,
            baseDifficulty: 1,
            minimumSecurity: 1,
            startingSecurity: 1,
            requiredHackingLevel: 1,
            growthParameter,
            maximumRam,
            hasRootAccess: hasRootAccess,
            isCloudServer: isCloudServer);
}
