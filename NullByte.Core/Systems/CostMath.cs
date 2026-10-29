using NullByte.Core;

namespace NullByte.ConsoleUI.Systems;

/// <summary>Classic idler cost curve: cost = base * growth^owned.</summary>
public static class CostMath
{
    public static double UpgradeCost(UpgradeDef def, int owned)
        => def.BaseCost * Math.Pow(def.GrowthRate, owned);
}