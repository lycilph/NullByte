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
                "run <filename> [-t <threads>] [args...]",
                "Run a script, optionally with several threads.",
                RunScriptAsync)
            {
                MinArgs = 1, MaxArgs = int.MaxValue,
                Completions = [
                    new ArgumentCompletion {ArgumentIndex = 0, Kind = CompletionKind.Filename},
                    new ArgumentCompletion { ArgumentIndex = 1, Values = [RunArguments.ThreadsFlag] }]
            });

        registry.Register(
            new CommandDefinition(
                "mem",
                "mem <filename> [-t <threads>]",
                "Displays the amount of RAM required to run the script.",
                ScriptMemoryAsync)
            {
                MinArgs = 1,
                MaxArgs = 3,
                Completions = [
                    new ArgumentCompletion {ArgumentIndex = 0, Kind = CompletionKind.Filename},
                    new ArgumentCompletion { ArgumentIndex = 1, Values = [RunArguments.ThreadsFlag] }]
            });

        registry.Register(
            new CommandDefinition(
                "free",
                "free",
                "Check the machine's memory (RAM) usage.",
                CheckServerMemoryAsync));

        registry.Register(
            new CommandDefinition(
                "ps",
                "ps",
                "Displays running scripts.",
                DisplayScriptsAsync));

        registry.Register(
            new CommandDefinition(
                "kill",
                "kill <pid>",
                "Stops the specified script on the current server .",
                KillScriptAsync)
            { MinArgs = 1, MaxArgs = 1 });

        registry.Register(
            new CommandDefinition(
                "killall",
                "killall",
                "Stops all running scripts on the current server .",
                KillAllScriptAsync));
    }

    private async Task CheckServerMemoryAsync(IReadOnlyList<string> arguments)
    {
        var server = networkService.GetCurrentServer();
        var free = server.UsedRam / server.MaximumRam * 100;

        output.WriteLine($"Total: {server.MaximumRam,11:N2}GB");
        output.WriteLine($"Used: {server.UsedRam,12:N2}GB ({free:N2}%)");
        output.WriteLine($"Available: {server.AvailableRam,7:N2}GB");
    }

    private async Task ScriptMemoryAsync(IReadOnlyList<string> arguments)
    {
        if (!RunArguments.TryParse(arguments, out var request, out var error))
        {
            output.WriteLine(error);
            output.WriteLine("Usage: mem <filename> [-t <threads>]");
            return;
        }

        var server = networkService.GetCurrentServer();
        var cost = scriptService.GetScriptRam(request.Filename, server.Hostname) * request.Threads;

        output.WriteLine($"This script requires {cost:N2}GB of RAM to run for {request.Threads} thread(s)");
    }

    private async Task KillAllScriptAsync(IReadOnlyList<string> arguments)
    {
        var result = scriptService.KillAll();
        output.WriteLine($"Killing all processes... [{result} processes killed]");
    }

    private async Task KillScriptAsync(IReadOnlyList<string> arguments)
    {
        if (!int.TryParse(arguments[0], out var pid))
        {
            output.WriteLine($"Not a valid pid: [{arguments[0]}]");
            return;
        }

        var result = scriptService.Kill(pid);
        output.WriteLine($"Killing process {pid}... {result}");
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
            $"{"Filename",-24} {"PID",5} {"Threads",8} {"State",12} Arguments");

        foreach (var process in processes)
        {
            var scriptArguments = FormatArguments(process.Args);

            output.WriteLine(
                $"{process.Filename,-24} " +
                $"{process.Pid,5} " +
                $"{process.Threads,8} " +
                $"{process.State,12} " +
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
        if (!RunArguments.TryParse(arguments, out var request, out var error))
        {
            output.WriteLine(error);
            output.WriteLine("Usage: run <filename> [-t <threads>] [args...]");
            return;
        }

        var server = networkService.GetCurrentServer();

        output.WriteLine(request.Threads > 1
            ? $"Executing {request.Filename} ({request.Threads} threads)..."
            : $"Executing {request.Filename}...");

        var result = await scriptService.Start(server.Hostname, request.Filename, request.ScriptArguments, request.Threads);

        foreach (var diagnostic in result.Diagnostics)
            WriteScriptDiagnostic(output, request.Filename, diagnostic);

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
