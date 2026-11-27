using NullByte.Core.Domain;
using NullByte.Core.Formulas;

namespace NullByte.Core.Services;

/// <summary>
/// Implements cloud-server purchases, RAM upgrades and cost queries.
/// </summary>
public sealed class CloudServerService
{
    private readonly GameState gameState;
    private readonly CloudServerFormulas formulas;

    public CloudServerService(GameState gameState, CloudServerFormulas formulas)
    {
        this.gameState = gameState ?? throw new ArgumentNullException(nameof(gameState));
        this.formulas = formulas ?? throw new ArgumentNullException(nameof(formulas));
    }

    public int CloudServerCount => gameState.Network.Servers.Count(server => server.IsCloudServer);

    public IReadOnlyList<Server> GetServers()
        => gameState.Network.Servers
            .Where(server => server.IsCloudServer)
            .OrderBy(server => server.Hostname, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public double GetPurchaseCost(int ram = 8)
    {
        ValidateRam(ram);
        var cost = formulas.CalculatePurchaseCost(CloudServerCount);
        if (ram > 8)
            cost += formulas.CalculateRamUpgradeCost(8, ram);
        return cost;
    }

    public double? GetUpgradeCost(string hostname, int newRam)
    {
        if (!gameState.Network.TryGetServer(hostname, out var server) || !server!.IsCloudServer)
            return null;
        if (!CloudServerFormulas.IsSupportedRam(newRam) || newRam <= server.MaximumRam)
            return null;

        return formulas.CalculateRamUpgradeCost((int)server.MaximumRam, newRam);
    }

    public CloudServerOperationResult Purchase(string hostname, int ram = 8)
    {
        if (string.IsNullOrWhiteSpace(hostname))
            return CloudServerOperationResult.Failed("A hostname is required.");
        if (!CloudServerFormulas.IsSupportedRam(ram))
            return CloudServerOperationResult.Failed("RAM must be one of the supported tiers: 8, 16, 32, 64, 128 or 256 GB.");
        if (gameState.Network.TryGetServer(hostname, out _))
            return CloudServerOperationResult.Failed($"A server named '{hostname}' already exists.");
        if (CloudServerCount >= formulasMaximumCount)
            return CloudServerOperationResult.Failed($"You can own at most {formulasMaximumCount} cloud servers.");

        var cost = GetPurchaseCost(ram);
        if (gameState.Player.Money < cost)
            return CloudServerOperationResult.Failed($"Not enough money. This cloud server costs ${cost:N2}.");

        var home = gameState.Network.GetServer(gameState.Player.HomeServerId);
        var server = new Server(
            hostname,
            maximumMoney: 0,
            startingMoney: 0,
            baseDifficulty: 1,
            minimumSecurity: 1,
            startingSecurity: 1,
            requiredHackingLevel: 1,
            growthParameter: 0,
            maximumRam: ram,
            hasRootAccess: true,
            isCloudServer: true);

        if (!gameState.Player.TrySpendMoney(cost))
            return CloudServerOperationResult.Failed($"Not enough money. This cloud server costs ${cost:N2}.");

        gameState.Network.AddServer(server);
        gameState.Network.Connect(home.Id, server.Id);
        gameState.Player.DiscoverServer(server.Id);

        return new CloudServerOperationResult(true, string.Empty, cost, server);
    }

    public CloudServerOperationResult Upgrade(string hostname, int newRam)
    {
        if (string.IsNullOrWhiteSpace(hostname) ||
            !gameState.Network.TryGetServer(hostname, out var server) ||
            !server!.IsCloudServer)
            return CloudServerOperationResult.Failed($"Unknown cloud server: {hostname}");

        var cost = GetUpgradeCost(hostname, newRam);
        if (cost is null)
            return CloudServerOperationResult.Failed("The requested RAM must be a higher supported tier.");

        if (gameState.Player.Money < cost.Value)
            return CloudServerOperationResult.Failed($"Not enough money. This upgrade costs ${cost.Value:N2}.");

        if (!gameState.Player.TrySpendMoney(cost.Value))
            return CloudServerOperationResult.Failed($"Not enough money. This upgrade costs ${cost.Value:N2}.");

        server.UpgradeRam(newRam);

        return new CloudServerOperationResult(true, string.Empty, cost.Value, server);
    }

    private int formulasMaximumCount => gameState.Network.Servers
        .Where(server => server.IsCloudServer)
        .Select(_ => 1)
        .Count() + 5 - CloudServerCount; // replaced below by the configured limit
    private static void ValidateRam(int ram)
    {
        if (!CloudServerFormulas.IsSupportedRam(ram))
            throw new ArgumentOutOfRangeException(nameof(ram), ram, "RAM must be one of the supported cloud-server tiers.");
    }
}
