using System.Text.Json.Serialization;

namespace NullByte.Core.Domain;

/// <summary>
/// An accepted contract: deadline countdown plus the stat baseline
/// snapshot taken at acceptance (progress = current - baseline).
/// Serialized with the save.
/// </summary>
public sealed class ActiveContractState
{
    [JsonInclude] public string DefId { get; internal set; } = "";
    [JsonInclude] public int DeadlineRemaining { get; internal set; }
    [JsonInclude] public double StartStat { get; internal set; }
}