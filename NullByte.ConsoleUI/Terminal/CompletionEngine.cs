namespace NullByte.ConsoleUI.Terminal;

/// <summary>
/// Pure completion logic: given a snapshot, the input and a cursor position,
/// work out what Tab should do. No I/O and no shared state.
/// </summary>
public static class CompletionEngine
{
    public static CompletionResult Complete(CompletionSnapshot snapshot, string input, int cursorIndex)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(input);

        cursorIndex = Math.Clamp(cursorIndex, 0, input.Length);

        var beforeCursor = input[..cursorIndex];

        // Quoted or escaped text is left alone for now.
        if (beforeCursor.Contains('"') || beforeCursor.Contains('\\'))
            return CompletionResult.None;

        var tokenStart = FindTokenStart(beforeCursor);
        var token = beforeCursor[tokenStart..];
        var previousTokens = CommandLineParser.Parse(beforeCursor[..tokenStart]);

        IReadOnlyList<string> pool;
        var appendSpaceWhenUnique = false;

        if (previousTokens.Count == 0)
        {
            pool = snapshot.Commands
                .Where(command => !command.Hidden)
                .Select(command => command.Name)
                .ToArray();
            appendSpaceWhenUnique = true;
        }
        else if (previousTokens.Count == 1)
        {
            var command = snapshot.Commands.FirstOrDefault(c =>
                c.Name.Equals(previousTokens[0], StringComparison.OrdinalIgnoreCase));

            pool = command?.ArgumentCompletion switch
            {
                ArgumentCompletionKind.ConnectableServer => snapshot.ConnectableServers,
                ArgumentCompletionKind.KnownServer => snapshot.KnownServers,
                _ => []
            };
        }
        else
        {
            return CompletionResult.None;
        }

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

            var spaceAlreadyFollows = cursorIndex < input.Length && char.IsWhiteSpace(input[cursorIndex]);
            if (appendSpaceWhenUnique && !spaceAlreadyFollows)
                replacement += " ";
        }
        else
        {
            replacement = LongestCommonPrefix(candidates);
        }

        return new CompletionResult(tokenStart, token.Length, replacement, candidates);
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

            while (common < limit &&
                   char.ToUpperInvariant(first[common]) == char.ToUpperInvariant(values[i][common]))
            {
                common++;
            }

            length = common;
        }

        return first[..length];
    }
}