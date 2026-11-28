using NullByte.Core.Services;

namespace NullByte.Core.Scripting;

/// <summary>Implements the cloud-server scripting API by delegating to <see cref="CloudServerService"/>.</summary>
public sealed class NullByteCloudApi : INullByteCloudApi
{
    private readonly CloudServerService cloudServerService;
    private readonly NullByte.Core.Domain.NarrativeState? narrative;

    public NullByteCloudApi(CloudServerService cloudServerService, NullByte.Core.Domain.NarrativeState? narrative = null)
    {
        this.cloudServerService = cloudServerService ?? throw new ArgumentNullException(nameof(cloudServerService));
        this.narrative = narrative;
    }

    public IReadOnlyList<CloudServerInfo> GetServers()
    {
        Require("cloud.inspect");
        return cloudServerService.GetServers().Select(ToInfo).ToArray();
    }

    public CloudServerInfo? GetServer(string hostname)
    {
        Require("cloud.inspect");
        ArgumentException.ThrowIfNullOrWhiteSpace(hostname);
        var server = cloudServerService.GetServers()
            .FirstOrDefault(server => string.Equals(server.Hostname, hostname, StringComparison.OrdinalIgnoreCase));
        return server is null ? null : ToInfo(server);
    }

    public double GetServerCost(int ram = 8) { Require("cloud.inspect"); return cloudServerService.GetServerCost(ram); }

    public double? GetUpgradeCost(string hostname, int ram) { Require("cloud.inspect"); return cloudServerService.GetUpgradeCost(hostname, ram); }

    public CloudServerActionResult Purchase(string hostname, int ram = 8) { Require("cloud.purchase"); return ToResult(cloudServerService.Purchase(hostname, ram)); }

    public CloudServerActionResult Upgrade(string hostname, int ram) { Require("cloud.manage"); return ToResult(cloudServerService.Upgrade(hostname, ram)); }

    public CloudServerActionResult Rename(string hostname, string newHostname) { Require("cloud.manage"); return ToResult(cloudServerService.Rename(hostname, newHostname)); }

    private void Require(string capability) => narrative?.EnsureCapability(capability);

    private static CloudServerActionResult ToResult(CloudServerOperationResult result)
        => new(result.Succeeded, result.FailureMessage, result.Cost,
            result.Server is null ? null : ToInfo(result.Server));

    private static CloudServerInfo ToInfo(NullByte.Core.Domain.Server server)
        => new(server.Hostname, server.MaximumRam, server.UsedRam);
}
