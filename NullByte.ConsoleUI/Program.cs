using System.Text;
using NullByte.ConsoleUI.Scripting;

namespace NullByte.ConsoleUI;

public class Program
{
    static async Task Main()
    {
        Console.OutputEncoding = Encoding.UTF8;

        await using var terminal = new ThreadSafeConsoleTerminal(
            prompt: "root@home $ ",
            maximumHistoryLines: 2_000);

        var terminalTask = terminal.RunAsync();

        terminal.WriteLine("Incremental scripting game started.");
        terminal.WriteLine("Type 'help' to see available commands.");
        terminal.WriteLine();

        // Simulate asynchronous game or script output.
        var backgroundSimulationTask = SimulateBackgroundActivityAsync(terminal);

        try
        {
            await foreach (var command in terminal.ReadCommandsAsync())
            {
                if (string.IsNullOrWhiteSpace(command))
                    continue;

                var shouldContinue = await ExecuteCommandAsync(command, terminal);

                if (!shouldContinue)
                    break;
            }
        }
        finally
        {
            terminal.RequestStop();

            await terminalTask;
            await backgroundSimulationTask;
        }
    }

    static async Task<bool> ExecuteCommandAsync(
        string commandLine,
        IGameTerminal terminal)
    {
        var arguments = CommandLineParser.Parse(commandLine);

        if (arguments.Count == 0)
            return true;

        var command = arguments[0].ToLowerInvariant();

        switch (command)
        {
            case "help":
                terminal.WriteLine("Available commands:");
                terminal.WriteLine("  help             Show this help");
                terminal.WriteLine("  scan             Scan the network");
                terminal.WriteLine("  hack <server>    Start hacking a server");
                terminal.WriteLine("  long             Print a long line");
                terminal.WriteLine("  script           Run a test script");
                terminal.WriteLine("  clear            Clear terminal history");
                terminal.WriteLine("  exit             Exit the application");
                return true;

            case "scan":
                terminal.WriteLine("Scanning network...");

                _ = Task.Run(async () =>
                {
                    await Task.Delay(500);
                    terminal.WriteLine("Discovered server: n00dles");

                    await Task.Delay(350);
                    terminal.WriteLine("Discovered server: foodnstuff");

                    await Task.Delay(300);
                    terminal.WriteLine("Scan completed.");
                });

                return true;

            case "hack":
                if (arguments.Count < 2)
                {
                    terminal.WriteLine("Usage: hack <server>");
                    return true;
                }

                var server = arguments[1];

                terminal.WriteLine($"Starting hack against {server}...");

                _ = Task.Run(async () =>
                {
                    await Task.Delay(1_500);
                    terminal.WriteLine($"Hack against {server} succeeded.");
                });

                return true;

            case "long":
                terminal.WriteLine(
                    "This is a deliberately long output line. " +
                    "The terminal renderer wraps output based on the current " +
                    "console window width and recalculates wrapping when the " +
                    "window is resized.");

                return true;

            case "script":
                await RunTestScript(terminal);
                return true;

            case "clear":
                terminal.Clear();
                return true;

            case "exit":
            case "quit":
                terminal.WriteLine("Shutting down...");
                return false;

            default:
                terminal.WriteLine($"Unknown command: {arguments[0]}");
                return true;
        }
    }

    static async Task RunTestScript(IGameTerminal terminal)
    {
        var game = new GameState();
        var api = new ScriptApi(game, terminal);
        var runner = new ScriptRunner(terminal);
        var script = @"var server = Api.GetServer(""foodmart"");

Api.Log(
    $""Target: {server.Hostname}, "" +
    $""Money: {server.Money:C}, "" +
    $""Security: {server.Security:F1}"");

var result = await Api.HackAsync(
    server.Hostname,
    CancellationToken);

Api.Log(
    $""Hack returned {result.MoneyStolen:C}"");

server = Api.GetServer(""foodmart"");

Api.Log(
    $""Server now has {server.Money:C}"");";

        using var cts = new CancellationTokenSource();

        terminal.WriteLine($"Player money before: {game.PlayerMoney:C}");

        await runner.RunAsync(
            script,
            api,
            cts.Token);

        terminal.WriteLine($"Player money after: {game.PlayerMoney:C}");
    }

    static async Task SimulateBackgroundActivityAsync(IGameTerminal terminal)
    {
        var counter = 1;

        while (!terminal.StoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(5),
                    terminal.StoppingToken);

                terminal.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss}] Simulation tick {counter++}");
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}