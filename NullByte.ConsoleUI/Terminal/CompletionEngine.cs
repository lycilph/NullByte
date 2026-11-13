namespace NullByte.ConsoleUI.Terminal;

/// <summary>
/// Pure completion logic: given a snapshot, the input and a cursor
/// position, work out what Tab should do.
///
/// No I/O, game-state access, or knowledge of individual commands.
/// </summary>
public static class CompletionEngine
{
    public static CompletionResult Complete(CompletionSnapshot snapshot, string input, int cursorIndex)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(input);

        cursorIndex = Math.Clamp(cursorIndex, 0, input.Length);

        var beforeCursor = input[..cursorIndex];

        /*
         * Quoted and escaped completion is deliberately not
         * supported yet.
         */
        if (beforeCursor.Contains('"') || beforeCursor.Contains('\\'))
            return CompletionResult.None;

        var tokenStart = FindTokenStart(beforeCursor);
        var token = beforeCursor[tokenStart..];

        var previousTokens = CommandLineParser.Parse(beforeCursor[..tokenStart]);

        var pool = SubcommandWords(snapshot, previousTokens)
            .Concat(ArgumentValues(snapshot, previousTokens))
            .ToArray();

        var candidates = pool
            .Where(name => name.StartsWith(token, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (candidates.Length == 0)
            return CompletionResult.None;

        string replacement;

        if (candidates.Length == 1)
        {
            replacement = candidates[0];

            var spaceAlreadyFollows =
                cursorIndex < input.Length &&
                char.IsWhiteSpace(input[cursorIndex]);

            if (!spaceAlreadyFollows)
                replacement += " ";
        }
        else
        {
            replacement =
                LongestCommonPrefix(candidates);
        }

        return new CompletionResult(
            tokenStart,
            token.Length,
            replacement,
            candidates);
    }

    // The next word of every command name that starts with the tokens typed so far.
    // With nothing typed this yields every command's first word, so plain
    // command-name completion needs no special case.
    private static IEnumerable<string> SubcommandWords(CompletionSnapshot snapshot, IReadOnlyList<string> typed)
    {
        foreach (var command in snapshot.Commands.Where(command => !command.Hidden))
        {
            if (command.Words.Count > typed.Count && PrefixMatches(command.Words, typed, typed.Count))
                yield return command.Words[typed.Count];
        }
    }

    // Argument values for the longest registered command that the typed tokens start with.
    private static IEnumerable<string> ArgumentValues(CompletionSnapshot snapshot, IReadOnlyList<string> typed)
    {
        CommandCompletionInfo? best = null;

        foreach (var command in snapshot.Commands.Where(command => !command.Hidden))
        {
            if (command.Words.Count <= typed.Count &&
                PrefixMatches(command.Words, typed, command.Words.Count) &&
                (best is null || command.Words.Count > best.Words.Count))
            {
                best = command;
            }
        }

        if (best is null)
            return [];

        var argumentIndex = typed.Count - best.Words.Count;
        var completion = best.Completions.FirstOrDefault(c => c.ArgumentIndex == argumentIndex);

        return completion is null ? [] : GetCompletionValues(snapshot, completion);
    }

    private static bool PrefixMatches(IReadOnlyList<string> words, IReadOnlyList<string> typed, int count)
    {
        for (var i = 0; i < count; i++)
        {
            if (!words[i].Equals(typed[i], StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return true;
    }

    private static IReadOnlyList<string> GetCompletionValues(CompletionSnapshot snapshot, ArgumentCompletion completion)
    {
        if (completion.Values.Count > 0)
            return completion.Values;

        if (completion.Kind == CompletionKind.None)
            return [];

        return snapshot.GetValues(completion.Kind);
    }

    private static int FindTokenStart(string text)
    {
        var index = text.Length;

        while (index > 0 && !char.IsWhiteSpace(text[index - 1]))
            index--;

        return index;
    }

    private static string LongestCommonPrefix(IReadOnlyList<string> values)
    {
        var first = values[0];
        var length = first.Length;

        for (var i = 1; i < values.Count && length > 0; i++)
        {
            var common = 0;

            var limit = Math.Min(length, values[i].Length);

            while (
                common < limit &&
                char.ToUpperInvariant(
                    first[common]) ==
                char.ToUpperInvariant(
                    values[i][common]))
            {
                common++;
            }

            length = common;
        }

        return first[..length];
    }
}
