using NullByte.Core.Configuration;

namespace NullByte.Core.Formulas;

/// <summary>
/// Calculates network-related game mechanics.
///
/// This class contains no mutable game state and does not modify
/// players or servers.
/// </summary>
public sealed class NetworkFormulas
{
    private readonly BalanceConfig balance;

    public NetworkFormulas(BalanceConfig balance)
    {
        this.balance = balance
            ?? throw new ArgumentNullException(nameof(balance));
    }

    /// <summary>
    /// Calculates the duration of an analyze operation.
    /// </summary>
    public TimeSpan CalculateAnalyzeDuration()
    {
        return TimeSpan.FromSeconds(balance.AnalyzeDuration);
    }
}
