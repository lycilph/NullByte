namespace NullByte.Core.Configuration;

/// <summary>
/// Contains tunable values that control game balance and simulation behavior.
/// </summary>
public sealed record BalanceConfig
{
    /// <summary>
    /// Controls how quickly game time advances relative to real time.
    /// A value of 1.0 means one second of real time equals one second
    /// of game time.
    /// </summary>
    public double GameSpeedMultiplier { get; init; } = 1.0;
}