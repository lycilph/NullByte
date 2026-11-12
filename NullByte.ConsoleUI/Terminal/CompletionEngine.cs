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

        IReadOnlyList<string> pool;

        if (previousTokens.Count == 0)
        {
            pool =
                snapshot.Commands
                    .Where(command => !command.Hidden)
                    .Select(command => command.Name)
                    .ToArray();
        }
        else
        {
            var command = FindCommand(snapshot, previousTokens[0]);

            if (command is null)
                return CompletionResult.None;

            var argumentIndex = previousTokens.Count - 1;

            var completion = FindCompletion(command, argumentIndex, previousTokens);

            if (completion is null)
                return CompletionResult.None;

            pool = GetCompletionValues(snapshot, completion);
        }

        var candidates =
            pool
                .Where(name =>
                    name.StartsWith(
                        token,
                        StringComparison.OrdinalIgnoreCase))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .OrderBy(
                    name => name,
                    StringComparer.OrdinalIgnoreCase)
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

    private static CommandCompletionInfo? FindCommand(CompletionSnapshot snapshot, string commandName)
    {
        return snapshot.Commands.FirstOrDefault(
            command =>
                command.Name.Equals(
                    commandName,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static ArgumentCompletion? FindCompletion(
        CommandCompletionInfo command,
        int argumentIndex,
        IReadOnlyList<string> tokens)
    {
        return command.Completions.FirstOrDefault(
            completion =>
                completion.ArgumentIndex == argumentIndex &&
                ConditionMatches(completion.When, tokens));
    }

    private static bool ConditionMatches(CompletionCondition? condition, IReadOnlyList<string> tokens)
    {
        if (condition is null)
            return true;

        /*
         * tokens[0] is the command name.
         * CompletionCondition.ArgumentIndex is relative to
         * command arguments, so add one.
         */
        var tokenIndex =
            condition.ArgumentIndex + 1;

        if (tokenIndex >= tokens.Count)
            return false;

        return tokens[tokenIndex].Equals(
            condition.Value,
            StringComparison.OrdinalIgnoreCase);
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
