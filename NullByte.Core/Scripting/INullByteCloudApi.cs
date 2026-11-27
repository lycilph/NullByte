using NullByte.Core.Services;

namespace NullByte.Core.Scripting;

/// <summary>Defines cloud-server management exposed through <c>nb.Cloud</c>.</summary>
public interface INullByteCloudApi
{
    /// <summary>Lists the cloud servers owned by the player.</summary>
    [RamCost(0.5)]
    IReadOnlyList<CloudServerInfo> GetServers();

    /// <summary>Gets a cloud server by hostname, or null if it does not exist.</summary>
    [RamCost(0.2)]
    CloudServerInfo? GetServer(string hostname);

    /// <summary>Gets the cost of buying a cloud server with the requested RAM tier.</summary>
    [RamCost(0.2)]
    double GetPurchaseCost(int ram = 8);

    /// <summary>Gets the cost of upgrading a cloud server, or null if the upgrade is invalid.</summary>
    [RamCost(0.2)]
    double? GetUpgradeCost(string hostname, int ram);

    /// <summary>Purchases a cloud server.</summary>
    [RamCost(1.0)]
    CloudServerActionResult Purchase(string hostname, int ram = 8);

    /// <summary>Upgrades a cloud server's RAM.</summary>
    [RamCost(1.0)]
    CloudServerActionResult Upgrade(string hostname, int ram);

    /// <summary>Renames a cloud server without changing its identity.</summary>
    [RamCost(0.5)]
    CloudServerActionResult Rename(string hostname, string newHostname);
}
