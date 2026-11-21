using NullByte.Core.Domain;

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
            .Concat(ArgumentValues(snapshot, previousTokens, token))
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

            // A directory ends in a slash and keeps going, so it gets no space.
            if (!spaceAlreadyFollows && !replacement.EndsWith('/'))
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
    private static IEnumerable<string> ArgumentValues(
        CompletionSnapshot snapshot, IReadOnlyList<string> typed, string token)
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
        var completion = best.Completions.FirstOrDefault(c => c.ArgumentIndex == -1 || c.ArgumentIndex == argumentIndex);

        return completion is null ? [] : GetCompletionValues(snapshot, completion, token);
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

    private static IReadOnlyList<string> GetCompletionValues(
        CompletionSnapshot snapshot, ArgumentCompletion completion, string token)
    {
        if (completion.Values.Count > 0)
            return completion.Values;

        switch (completion.Kind)
        {
            case CompletionKind.None:
                return [];

            case CompletionKind.Filename:
            case CompletionKind.FilenameOrDirectory:
                return PathCandidates(snapshot, token, includeFiles: true);

            case CompletionKind.Directory:
                return PathCandidates(snapshot, token, includeFiles: false);

            case CompletionKind.FilenameOrKnownServer:
                // Values given directly for this kind are plain candidates (hosts that do not split them).
                return PathCandidates(snapshot, token, includeFiles: true)
                    .Concat(snapshot.GetValues(CompletionKind.KnownServer))
                    .Concat(snapshot.GetValues(CompletionKind.FilenameOrKnownServer))
                    .ToArray();

            default:
                return snapshot.GetValues(completion.Kind);
        }
    }

    // Completes one path segment at a time: the typed part up to the last slash names a directory
    // (relative to the current directory, or absolute), and its children are the candidates.
    // Directories end in a slash. Every candidate starts with the typed directory part, as typed.
    private static IReadOnlyList<string> PathCandidates(CompletionSnapshot snapshot, string token, bool includeFiles)
    {
        var slash = token.LastIndexOf('/');
        var typedDirectory = slash >= 0 ? token[..(slash + 1)] : "";
        var name = token[(slash + 1)..];

        if (!PathResolver.TryResolve(snapshot.CurrentDirectory, typedDirectory, out var directory, out _))
            return [];

        var candidates = new List<string>();

        foreach (var path in snapshot.GetValues(CompletionKind.Directory))
        {
            if (VirtualPath.Parent(path) == directory)
                candidates.Add(typedDirectory + VirtualPath.Name(path) + "/");
        }

        if (includeFiles)
        {
            foreach (var path in snapshot.GetValues(CompletionKind.Filename))
            {
                if (VirtualPath.Parent(path) == directory)
                    candidates.Add(typedDirectory + VirtualPath.Name(path));
            }
        }

        // "." and ".." are not in the file system, but they are how players move around.
        if (name == ".")
        {
            candidates.Add(typedDirectory + "./");
            candidates.Add(typedDirectory + "../");
        }
        else if (name == "..")
        {
            candidates.Add(typedDirectory + "../");
        }

        return candidates;
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