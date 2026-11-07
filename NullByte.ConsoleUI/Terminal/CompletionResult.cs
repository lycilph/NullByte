namespace NullByte.ConsoleUI.Terminal;

/// <summary>
/// The outcome of a tab-completion request.
/// </summary>
/// <param name="ReplaceStart">Index in the input where the replaced text starts.</param>
/// <param name="ReplaceLength">Number of characters to replace.</param>
/// <param name="Replacement">
/// Text to put there: the full match when there is one candidate,
/// otherwise the longest prefix shared by all candidates.
/// </param>
/// <param name="Candidates">Every matching suggestion, sorted.</param>
public sealed record CompletionResult(
    int ReplaceStart,
    int ReplaceLength,
    string Replacement,
    IReadOnlyList<string> Candidates)
{
    public static CompletionResult None { get; } = new(0, 0, string.Empty, []);

    public bool HasCandidates => Candidates.Count > 0;
}