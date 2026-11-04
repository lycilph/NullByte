using System.Text.Json.Serialization;

namespace NullByte.Core.Domain;

/// <summary>Lifetime counters; serialized with the save.</summary>
public sealed class GameStatistics
{
    [JsonInclude] public int TotalHacks { get; internal set; }
    [JsonInclude] public double MoneyEarned { get; internal set; }
    [JsonInclude] public int UpgradesBought { get; internal set; }

    /// <summary>Completed hacks per target id, e.g. ["city-bank"] = 12.</summary>
    [JsonInclude] public Dictionary<string, int> HacksPerTarget { get; internal set; } = [];

    [JsonInclude] public int ContractsCompleted { get; internal set; }
    [JsonInclude] public int ContractsFailed { get; internal set; }

    [JsonInclude] public int PrestigeCount { get; internal set; }
}
