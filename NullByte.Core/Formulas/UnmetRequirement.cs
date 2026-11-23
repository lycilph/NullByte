namespace NullByte.Core.Formulas;

/// <summary>
/// One thing a player still lacks for a job rank.
/// </summary>
/// <param name="Name">What is lacking, e.g. <c>hacking level</c> or <c>reputation</c>.</param>
/// <param name="Required">How much is needed.</param>
/// <param name="Current">How much the player has.</param>
public sealed record UnmetRequirement(string Name, double Required, double Current);