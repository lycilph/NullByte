namespace NullByte.Core.Domain;

/// <summary>
/// A ladder of ranks at one company, e.g. <c>software</c> or <c>security</c>.
/// Ranks are ordered from entry level upwards, and each is at least as
/// demanding and as well paid as the one before.
/// </summary>
/// <param name="Name">Name unique within the company.</param>
/// <param name="Ranks">The ranks, lowest first. Never empty.</param>
public sealed record CareerPath(string Name, IReadOnlyList<JobRank> Ranks);