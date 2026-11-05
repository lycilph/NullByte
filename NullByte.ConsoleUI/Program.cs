using System.Text;
using NullByte.ConsoleUI.Terminal;
using NullByte.Core.Configuration;
using NullByte.Core.Simulation;
using NullByte.Core.Threading;
using NullByte.Core.Time;

namespace NullByte.ConsoleUI;

public class Program
{
    static async Task Main()
    {
        Console.OutputEncoding = Encoding.UTF8;

        var balanceConfig = new BalanceConfig();
        var clock = new RealGameClock() { SpeedMultiplier = balanceConfig.GameSpeedMultiplier };
        var synchronizationContext = new GameSynchronizationContext();

        await using var gameLoop = new GameLoop(clock, synchronizationContext);

        await using var terminal = new ThreadSafeConsoleTerminal(
            prompt: "root@home $ ",
            maximumHistoryLines: 2_000);
        var terminalTask = terminal.RunAsync();
        
        gameLoop.Start();

        try
        {
            await gameLoop.Started;

            WriteStartupMessage(terminal, gameLoop);

            await ProcessCommandsAsync(gameLoop, terminal);
        }
        finally
        {
            gameLoop.RequestStop();
            try
            {
                await gameLoop.Completion;
            }
            catch (Exception exception)
            {
                terminal.WriteLine($"Game loop stopped with an error: {exception.Message}");
            }

            terminal.RequestStop();
            await terminalTask;
        }

        Console.WriteLine("Everything stopped, bye");
        await Task.Delay(500);
    }

    private static void WriteStartupMessage(IGameTerminal terminal, GameLoop gameLoop)
    {
        terminal.WriteLine("NullByte");
        terminal.WriteLine();
        terminal.WriteLine("Single-threaded simulation started.");
        terminal.WriteLine($"Game thread ready. Game time: {gameLoop.Clock.Elapsed.TotalSeconds:F3}s");
        terminal.WriteLine();
        terminal.WriteLine("Type 'help' to see available commands.");
        terminal.WriteLine();
    }

    private static async Task RunMilestoneDemonstrationAsync(GameLoop gameLoop, IGameTerminal terminal)
    {
        try
        {
            terminal.WriteLine("[M1] Starting scheduler demonstration.");
            terminal.WriteLine($"[M1] Game thread ID: {Environment.CurrentManagedThreadId}");
            terminal.WriteLine();

            var slowOperation =
                DemonstrateOperationAsync(
                    name: "operation-a",
                    duration: TimeSpan.FromSeconds(3),
                    gameLoop,
                    terminal);

            var fastOperation =
                DemonstrateOperationAsync(
                    name: "operation-b",
                    duration: TimeSpan.FromSeconds(1.5),
                    gameLoop,
                    terminal);

            await Task.WhenAll(slowOperation, fastOperation);

            terminal.WriteLine();
            terminal.WriteLine("[M1] Both scheduled operations completed.");
        }
        catch (Exception exception)
        {
            terminal.WriteLine($"[M1] Demonstration failed: {exception.Message}");
        }
    }

    private static async Task DemonstrateOperationAsync(
        string name,
        TimeSpan duration,
        GameLoop gameLoop,
        IGameTerminal terminal)
    {
        var threadBefore = Environment.CurrentManagedThreadId;

        terminal.WriteLine(
            $"[M1] {name} started at " +
            $"{gameLoop.Clock.Elapsed.TotalSeconds:F3}s " +
            $"on thread {threadBefore}. " +
            $"Waiting {duration.TotalSeconds:F1} game seconds.");

        await gameLoop.Scheduler.DelayAsync(duration);

        var threadAfter = Environment.CurrentManagedThreadId;

        terminal.WriteLine(
            $"[M1] {name} completed at " +
            $"{gameLoop.Clock.Elapsed.TotalSeconds:F3}s " +
            $"on thread {threadAfter}.");

        if (threadBefore != threadAfter || !gameLoop.IsGameThread)
            throw new InvalidOperationException($"{name} resumed on the wrong thread.");
    }

    private static async Task ProcessCommandsAsync(
        GameLoop gameLoop,
        ThreadSafeConsoleTerminal terminal)
    {
        await foreach (var commandLine in terminal.ReadCommandsAsync())
        {
            if (string.IsNullOrWhiteSpace(commandLine))
                continue;

            var arguments = CommandLineParser.Parse(commandLine);

            if (arguments.Count == 0)
                continue;

            var command = arguments[0].ToLowerInvariant();

            if (command is "exit" or "quit")
            {
                terminal.WriteLine("Shutting down...");
                return;
            }
           
            try
            {
                gameLoop.Post(
                    _ => ExecuteGameCommand(
                        arguments,
                        gameLoop,
                        terminal));
            }
            catch (InvalidOperationException)
                when (!gameLoop.IsRunning)
            {
                return;
            }
        }
    }

    private static void ExecuteGameCommand(
        IReadOnlyList<string> arguments,
        GameLoop gameLoop,
        IGameTerminal terminal)
    {
        gameLoop.SynchronizationContext.VerifyAccess();

        var command = arguments[0].ToLowerInvariant();

        switch (command)
        {
            case "help":
                WriteHelp(terminal);
                break;

            case "time":
                WriteGameTime(gameLoop, terminal);
                break;

            case "thread":
                WriteThreadInformation(gameLoop, terminal);
                break;

            case "demo":
                _ = RunMilestoneDemonstrationAsync(gameLoop, terminal);
                break;

            case "clear":
                terminal.Clear();
                break;

            default:
                terminal.WriteLine($"Unknown command: {arguments[0]}");
                terminal.WriteLine("Type 'help' to see available commands.");
                break;
        }
    }
    private static void WriteHelp(IGameTerminal terminal)
    {
        terminal.WriteLine("Available commands:");
        terminal.WriteLine("  help       Show this help");
        terminal.WriteLine("  time       Show current game time");
        terminal.WriteLine("  thread     Show the current game thread");
        terminal.WriteLine("  demo       Run the M1 scheduler demo");
        terminal.WriteLine("  clear      Clear terminal history");
        terminal.WriteLine("  exit       Exit NullByte");
    }

    private static void WriteGameTime(GameLoop gameLoop, IGameTerminal terminal)
        => terminal.WriteLine($"Game time: {gameLoop.Clock.Elapsed.TotalSeconds:F3}s");

    private static void WriteThreadInformation(GameLoop gameLoop, IGameTerminal terminal)
    {
        terminal.WriteLine($"Thread ID: {Environment.CurrentManagedThreadId}");
        terminal.WriteLine($"Is game thread: {gameLoop.IsGameThread}");
    }
}