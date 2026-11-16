using System.Globalization;

namespace NullByte.ConsoleUI.Commands.Groups;

internal readonly record struct RunRequest(string Filename, int Threads, IReadOnlyList<string> ScriptArguments);

internal static class RunArguments
{
    public const string ThreadsFlag = "-t";

    public static bool TryParse(IReadOnlyList<string> arguments, out RunRequest request, out string error)
    {
        request = default;
        error = string.Empty;

        var threads = 1;
        var next = 1; // arguments[0] is the filename

        if (next < arguments.Count && arguments[next] == ThreadsFlag)
        {
            if (next + 1 >= arguments.Count)
            {
                error = $"Missing thread count after {ThreadsFlag}.";
                return false;
            }

            var text = arguments[next + 1];

            // NumberStyles.None rejects signs and whitespace, so "-3" and "+3" are errors too.
            if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out threads) || threads < 1)
            {
                error = $"Invalid thread count: [{text}]. It must be a whole number of at least 1.";
                return false;
            }

            next += 2;
        }

        request = new RunRequest(arguments[0], threads, arguments.Skip(next).ToArray());
        return true;
    }
}