using System.Text.Json.Serialization;

namespace NullByte.Core.Domain;

/// <summary>
/// Live state of one target: a drainable money pool and a heat gauge
/// (Phase 4.5). The pool only recovers via 'grow' (an active op) — heat
/// decays passively when no job runs on the mark. Wiped on identity burn.
/// </summary>
public sealed class TargetState
{
    [JsonInclude] public double CurrentMoney { get; internal set; }
    [JsonInclude] public double Heat { get; internal set; }
}
