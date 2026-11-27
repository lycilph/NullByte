using NullByte.Core.Services;

namespace NullByte.Core.Scripting;

/// <summary>Implements the cloud-server scripting API by delegating to <see cref="CloudServerService"/>.</summary>
public sealed class NullByteCloudApi : INullByteCloudApi
{
    private readonly CloudServerService cloudServerService;

    public NullByteCloudApi(CloudServerService cloudServerService)
    {
        this.cloudServerService = cloudServerService ?? throw new ArgumentNullException(nameof(cloudServerService));
    }

    public IReadOnlyList<CloudServerInfo> GetServers()
        => cloudServerService.GetServers().Select(ToInfo).ToArray();

    public CloudServerInfo? GetServer(string hostname)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostname);
        var server = cloudServerService.GetServers()
            .FirstOrDefault(server => string.Equals(server.Hostname, hostname, StringComparison.OrdinalIgnoreCase));
        return server is null ? null : ToInfo(server);
    }

    public double GetPurchaseCost(int ram = 8) => cloudServerService.GetPurchaseCost(ram);

    public double? GetUpgradeCost(string hostname, int ram)
        => cloudServerService.GetUpgradeCost(hostname, ram);

    public CloudServerActionResult Purchase(string hostname, int ram = 8)
        => ToResult(cloudServerService.Purchase(hostname, ram));

    public CloudServerActionResult Upgrade(string hostname, int ram)
        => ToResult(cloudServerService.Upgrade(hostname, ram));

    public CloudServerActionResult Rename(string hostname, string newHostname)
        => ToResult(cloudServerService.Rename(hostname, newHostname));

    private static CloudServerActionResult ToResult(CloudServerOperationResult result)
        => new(result.Succeeded, result.FailureMessage, result.Cost,
            result.Server is null ? null : ToInfo(result.Server));

    private static CloudServerInfo ToInfo(NullByte.Core.Domain.Server server)
        => new(server.Hostname, server.MaximumRam, server.UsedRam);
}
