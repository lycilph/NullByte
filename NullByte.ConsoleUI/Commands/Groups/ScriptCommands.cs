using NullByte.ConsoleUI.Terminal;
using NullByte.Core.Services;
using NullByte.Scripting.Compilation;
using NullByte.Scripting.Runtime;

namespace NullByte.ConsoleUI.Commands.Groups;

public sealed class ScriptCommands(NetworkService networkService, ScriptService scriptService, IGameOutput output) : ICommandGroup
{
    public void Register(CommandRegistry registry)
    {
        registry.Register(
            new CommandDefinition(
                "run",
                "run <filename> [args...]",
                "Run a script.",
                RunScriptAsync)
            {
                MinArgs = 1, MaxArgs = int.MaxValue,
                Completions = [new ArgumentCompletion {ArgumentIndex = 0, Kind = CompletionKind.Filename}]
            });

        registry.Register(
            new CommandDefinition(
                "ps",
                "ps",
                "Displays running scripts.",
                DisplayScriptsAsync));
    }

    private async Task DisplayScriptsAsync(IReadOnlyList<string> arguments)
    {
        var server = networkService.GetCurrentServer();
        var processes = scriptService.ProcessesRunning(server.Hostname);

        if (processes.Count == 0)
        {
            output.WriteLine($"No scripts are running on {server.Hostname}.");
            return;
        }

        output.WriteLine($"Scripts running on {server.Hostname}:");
        output.WriteLine(
            $"{"Filename",-24} {"PID",5} {"Threads",8}  Arguments");

        foreach (var process in processes)
        {
            var scriptArguments = FormatArguments(process.Args);

            output.WriteLine(
                $"{process.Filename,-24} " +
                $"{process.Pid,5} " +
                $"{process.Threads,8}  " +
                $"{scriptArguments}");
        }
    }

    private static string FormatArguments(IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
            return "-";

        return string.Join(" ", arguments.Select(QuoteArgumentIfNeeded));
    }

    private static string QuoteArgumentIfNeeded(string argument)
    {
        if (argument.Length == 0)
            return "\"\"";

        return argument.Any(char.IsWhiteSpace)
            ? $"\"{argument.Replace("\"", "\\\"")}\""
            : argument;
    }

    private async Task RunScriptAsync(IReadOnlyList<string> arguments)
    {
        var server = networkService.GetCurrentServer();
        var fileName = arguments[0];
        var scriptArguments = arguments.Skip(1).ToArray();

        output.WriteLine($"Executing {fileName}...");

        var result = await Task.Run(() => scriptService.Start(server.Hostname, fileName, scriptArguments));

        foreach (var diagnostic in result.Diagnostics)
            WriteScriptDiagnostic(output, fileName, diagnostic);

        if (!result.Succeeded && result.Diagnostics.Count == 0)
            output.WriteLine(result.FailureMessage);
    }

    private static void WriteScriptDiagnostic(IGameOutput output, string fileName, ScriptDiagnostic diagnostic)
    {
        var severity = diagnostic.Severity.ToString().ToLowerInvariant();

        output.WriteLine(
            $"{fileName}({diagnostic.Line},{diagnostic.Column}): " +
            $"{severity} {diagnostic.Id}: {diagnostic.Message}");
    }
}
