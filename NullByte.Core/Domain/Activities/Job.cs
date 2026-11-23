namespace NullByte.Core.Domain.Activities;

/// <summary>
/// A job the player holds at one company: which career path, and how far up it.
/// </summary>
/// <param name="Career">The name of the career path at the company.</param>
/// <param name="RankIndex">The position on the path, counting from zero at the entry rank.</param>
public sealed record Job(string Career, int RankIndex);