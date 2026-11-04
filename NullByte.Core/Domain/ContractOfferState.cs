using System.Text.Json.Serialization;

namespace NullByte.Core.Domain;

/// <summary>A contract on the board, not yet accepted. Serialized with the save.</summary>
public sealed class ContractOfferState
{
    [JsonInclude] public string DefId { get; internal set; } = "";
}