namespace NullByte.Core.Scripting.Activities;

/// <summary>
/// A job the player holds, as seen by a script.
/// </summary>
/// <param name="Company">The company.</param>
/// <param name="Career">The career path, e.g. <c>software</c>.</param>
/// <param name="Title">The title of the current rank, e.g. <c>Junior Developer</c>.</param>
/// <param name="Rank">The current rank, counting from 1 at the entry rank.</param>
/// <param name="Ranks">How many ranks the career has.</param>
public sealed record JobInfo(string Company, string Career, string Title, int Rank, int Ranks);