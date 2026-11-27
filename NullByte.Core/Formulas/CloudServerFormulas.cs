using NullByte.Core.Configuration;

namespace NullByte.Core.Formulas;

/// <summary>
/// Calculates cloud-server purchase and RAM-upgrade costs.
/// </summary>
public sealed class CloudServerFormulas
{
    private static readonly int[] ramTierValues = [8, 16, 32, 64, 128, 256];
    public static IReadOnlyList<int> RamTiers { get; } = Array.AsReadOnly(ramTierValues);

    public int MaximumCount => balance.CloudServerMaxCount;

    private readonly BalanceConfig balance;

    public CloudServerFormulas(BalanceConfig balance)
    {
        this.balance = balance ?? throw new ArgumentNullException(nameof(balance));

        if (balance.CloudServerMaxCount < 0)
            throw new ArgumentOutOfRangeException(nameof(balance.CloudServerMaxCount));
        ValidateCost(balance.CloudServerBaseCost, nameof(balance.CloudServerBaseCost));
        ValidateGrowth(balance.CloudServerCostGrowth, nameof(balance.CloudServerCostGrowth));
        ValidateCost(balance.CloudServerRamUpgradeBaseCost, nameof(balance.CloudServerRamUpgradeBaseCost));
        ValidateGrowth(balance.CloudServerRamUpgradeCostGrowth, nameof(balance.CloudServerRamUpgradeCostGrowth));
    }

    public double CalculatePurchaseCost(int existingCloudServerCount)
    {
        if (existingCloudServerCount < 0)
            throw new ArgumentOutOfRangeException(nameof(existingCloudServerCount));

        return balance.CloudServerBaseCost * Math.Pow(balance.CloudServerCostGrowth, existingCloudServerCount);
    }

    /// <summary>Calculates the combined cost of moving between two supported RAM tiers.</summary>
    public double CalculateRamUpgradeCost(int currentRam, int newRam)
    {
        var currentIndex = GetRamTierIndex(currentRam);
        var newIndex = GetRamTierIndex(newRam);

        if (newIndex <= currentIndex)
            throw new ArgumentOutOfRangeException(nameof(newRam), "New RAM must be a higher supported tier.");

        var total = 0.0;
        for (var tierIndex = currentIndex; tierIndex < newIndex; tierIndex++)
            total += balance.CloudServerRamUpgradeBaseCost
                * Math.Pow(balance.CloudServerRamUpgradeCostGrowth, tierIndex);

        return total;
    }

    public static bool IsSupportedRam(int ram) => RamTiers.Contains(ram);

    private static int GetRamTierIndex(int ram)
    {
        var index = Array.IndexOf(ramTierValues, ram);
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(ram), ram, "RAM must be one of the supported cloud-server tiers.");

        return index;
    }

    private static void ValidateCost(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0)
            throw new ArgumentOutOfRangeException(name, "Cost must be finite and non-negative.");
    }

    private static void ValidateGrowth(double value, string name)
    {
        if (!double.IsFinite(value) || value < 1)
            throw new ArgumentOutOfRangeException(name, "Cost growth must be finite and at least one.");
    }
}
