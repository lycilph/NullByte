namespace NullByte.Core.Services;

/// <summary>
/// Describes the outcome of a completed hack operation.
/// </summary>
/// <param name="Succeeded">
/// Whether the hacking attempt succeeded.
/// </param>
/// <param name="MoneyStolen">
/// The amount of money actually removed from the target server.
/// </param>
/// <param name="ExperienceGained">
/// The hacking experience awarded to the player.
/// </param>
/// <param name="SecurityIncrease">
/// The amount by which the target server's security increased.
/// </param>
/// <param name="Duration">
/// The amount of game time taken by the operation.
/// </param>
public sealed record HackResult(
    bool Succeeded,
    double MoneyStolen,
    double ExperienceGained,
    double SecurityIncrease,
    TimeSpan Duration);

/// <summary>
/// Describes the outcome of a completed grow operation.
/// </summary>
/// <param name="MoneyGrown">
/// The amount of money actually added to the target server.
/// </param>
/// <param name="ExperienceGained">
/// The hacking experience awarded to the player.
/// </param>
/// <param name="SecurityIncrease">
/// The amount by which the target server's security increased.
/// </param>
/// <param name="Duration">
/// The amount of game time taken by the operation.
/// </param>
public sealed record GrowResult(
    double MoneyGrown,
    double ExperienceGained,
    double SecurityIncrease,
    TimeSpan Duration);

/// <summary>
/// Describes the outcome of a completed weaken operation.
/// </summary>
/// <param name="SecurityDecrease">
/// The amount by which the target server's security was actually
/// reduced.
/// </param>
/// <param name="ExperienceGained">
/// The hacking experience awarded to the player.
/// </param>
/// <param name="Duration">
/// The amount of game time taken by the operation.
/// </param>
public sealed record WeakenResult(
    double SecurityDecrease,
    double ExperienceGained,
    TimeSpan Duration);