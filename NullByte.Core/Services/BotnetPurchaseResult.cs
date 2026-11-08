namespace NullByte.Core.Services;

/// <summary>
/// Describes the outcome of buying a botnet node or an upgrade.
/// </summary>
/// <param name="Succeeded">
/// Whether the purchase went through.
/// </param>
/// <param name="FailureMessage">
/// Why the purchase failed. Empty on success.
/// </param>
/// <param name="Cost">
/// The money spent. Zero on failure.
/// </param>
/// <param name="NodeIndex">
/// The zero-based index of the node that was bought or upgraded, or -1 on failure.
/// </param>
public sealed record BotnetPurchaseResult(bool Succeeded, string FailureMessage, double Cost, int NodeIndex)
{
    public static BotnetPurchaseResult Failed(string message) => new(false, message, 0, -1);
}