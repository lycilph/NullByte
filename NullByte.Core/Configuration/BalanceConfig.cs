using System.ComponentModel.DataAnnotations;

namespace NullByte.Core.Configuration;

public sealed class BalanceConfig
{
    public HacknetBalanceConfig Hacknet { get; init; } = new();
    public PlayerBalanceConfig Player { get; init; } = new();
}

public sealed class HacknetBalanceConfig
{
    [Range(0.000001, double.MaxValue)]
    public double ProductionMultiplier { get; init; } = 1.0;
    [Range(0.0, double.MaxValue)]
    public double BaseNodeCost { get; init; } = 1_000.0;
}

public sealed class PlayerBalanceConfig
{
    [Range(0.0, double.MaxValue)]
    public double StartingMoney { get; init; } = 1_000.0;
    [Range(0.0, double.MaxValue)]
    public double StartingHackingExperience { get; init; }
}
