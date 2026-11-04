using System.Text;

namespace NullByte.ConsoleUI;

public static class CommandLineParser
{
    public static IReadOnlyList<string> Parse(string commandLine)
    {
        var arguments = new List<string>();
        var current = new StringBuilder();

        var insideQuotes = false;
        var escaping = false;

        foreach (var character in commandLine)
        {
            if (escaping)
            {
                current.Append(character);
                escaping = false;
                continue;
            }

            if (character == '\\')
            {
                escaping = true;
                continue;
            }

            if (character == '"')
            {
                insideQuotes = !insideQuotes;
                continue;
            }

            if (char.IsWhiteSpace(character) && !insideQuotes)
            {
                AddCurrentArgument(arguments, current);
                continue;
            }

            current.Append(character);
        }

        if (escaping)
            current.Append('\\');

        AddCurrentArgument(arguments, current);

        return arguments;
    }

    private static void AddCurrentArgument(
        ICollection<string> arguments,
        StringBuilder current)
    {
        if (current.Length == 0)
            return;

        arguments.Add(current.ToString());
        current.Clear();
    }
}