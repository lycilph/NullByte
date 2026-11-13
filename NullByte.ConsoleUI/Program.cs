using System.Text;
using NullByte.ConsoleUI.Commands;
using NullByte.ConsoleUI.Terminal;
using NullByte.Core.Configuration;
using NullByte.Core.Formulas;
using NullByte.Core.Randomness;
using NullByte.Core.Scripting;
using NullByte.Core.Services;
using NullByte.Core.Simulation;
using NullByte.Core.Systems;
using NullByte.Core.Threading;
using NullByte.Core.Time;
using NullByte.Core.World;
using NullByte.Scripting.Compilation;
using NullByte.Scripting.Loading;
using NullByte.Scripting.Runtime;
using static Microsoft.CodeAnalysis.CSharp.SyntaxTokenParser;

namespace NullByte.ConsoleUI;

public class Program
{
    static async Task Main()
    {
        Console.OutputEncoding = Encoding.UTF8;

        var balanceConfig = new BalanceConfig() { RandomSeed = DateTime.Now.Millisecond };
        var clock = new RealGameClock() { SpeedMultiplier = balanceConfig.GameSpeedMultiplier };
        
        var synchronizationContext = new GameSynchronizationContext();
        var scheduler = new GameScheduler(clock);
        await using var gameLoop = new GameLoop(clock, synchronizationContext, scheduler);

        var gameState = InitialGameStateFactory.Create();
        var networkService = new NetworkService(gameState);
        var hackingFormulas = new HackingFormulas(balanceConfig);
        var botnetFormulas = new BotnetFormulas(balanceConfig);
        var randomSource = new SeededRandomSource(balanceConfig.RandomSeed);
        var serverActions = new ServerActions(gameState, scheduler, hackingFormulas, randomSource);
        var botnetService = new BotnetService(gameState, botnetFormulas);

        // Systems are updated by the game loop on the game thread; they must be added before it starts.
        gameLoop.AddSystem(new BotnetSystem(gameState, botnetFormulas));

        var commandRegistry = CommandRegistryFactory.Create();
        var commandDispatcher = new CommandDispatcher(commandRegistry);

        // Tab completion reads an immutable snapshot that is refreshed on the game thread.
        var completionCatalog = new CompletionCatalog(commandRegistry, gameState);

        await using var terminal = new ThreadSafeConsoleTerminal(
            prompt: "root@home $ ", 
            maximumHistoryLines: 2_000,
            completionProvider: completionCatalog);
        
        var terminalTask = terminal.RunAsync();
        var scriptCompiler = new ScriptCompiler();
        var scriptLoader = new ScriptAssemblyLoader();
        var scriptRunner = new ScriptRunner(scriptLoader);

        // Warm up the script compiler (should i await this somewhere???)
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        Task.Run(() => scriptCompiler.Compile("{}", "warmup"));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

        var api = new NullByteApi(gameState, scheduler, serverActions, terminal);
        var scriptService = new ScriptService(
            gameState,
            scriptCompiler,
            scriptRunner,
            api);
        
        scriptService.ScriptFailed += exception => terminal.WriteLine($"Script failed: {exception.Message}");

        var context = new CommandContext(
            gameState,
            gameLoop,
            clock,
            networkService,
            serverActions,
            botnetService,
            scriptService,
            hackingFormulas,
            botnetFormulas,
            commandRegistry,
            terminal);

        // Work that throws on the game thread is reported, not fatal.
        gameLoop.UnhandledException += exception =>
            terminal.WriteLine($"Unhandled error on the game thread: {exception.Message}");

        gameLoop.Start();

        try
        {
            await gameLoop.Started;
            
            // Initialize the completion catalog
            await gameLoop.InvokeAsync(() =>
            {
                completionCatalog.Refresh();
                return Task.CompletedTask;
            });

            WriteStartupMessage(terminal, clock);

            await ProcessCommandsAsync(context, commandDispatcher, completionCatalog);
        }
        finally
        {
            terminal.RequestStop();
            gameLoop.RequestStop();

            try
            {
                await gameLoop.Completion;
            }
            catch (Exception exception)
            {
                terminal.WriteLine($"Game loop stopped with an error: {exception.Message}");
            }
            await terminalTask;
        }

        Console.WriteLine("Everything stopped, bye");
        await Task.Delay(500);
    }

    private static void WriteStartupMessage(IGameTerminal terminal, IGameClock clock)
    {
        terminal.WriteLine("NullByte");
        terminal.WriteLine();
        terminal.WriteLine("Single-threaded simulation started.");
        terminal.WriteLine($"Game thread ready. Game time: {clock.Elapsed.TotalSeconds:F3}s");
        terminal.WriteLine();
        terminal.WriteLine("Type 'help' to see available commands ('debug' for other commands).");
        terminal.WriteLine("Tab completes commands and server names, Up/Down recalls earlier input.");
        terminal.WriteLine();
    }

    private static async Task ProcessCommandsAsync(
        CommandContext context, 
        CommandDispatcher dispatcher,
        CompletionCatalog completionCatalog)
    {
        var terminal = context.Terminal;
        var gameThread = context.GameThread;

        await foreach (var commandLine in terminal.ReadCommandsAsync())
        {
            if (string.IsNullOrWhiteSpace(commandLine))
                continue;

            if (IsExitCommand(commandLine))
            {
                terminal.WriteLine("Shutting down...");
                return;
            }

            try
            {
                await gameThread.InvokeAsync(async () =>
                {
                    try
                    {
                        terminal.SetInputEnabled(false);
                        await dispatcher.DispatchAsync(commandLine, context);

                        // Update terminal prompt
                        terminal.SetPrompt($"root@{context.GameState.Player.CurrentHostname} $ ");
                    }
                    finally
                    {
                        terminal.SetInputEnabled(true);
                        // Commands such as scan and connect change what can be completed.
                        completionCatalog.Refresh();
                    }
                });
            }
            catch (OperationCanceledException)
                when (terminal.StoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (InvalidOperationException)
                when (!gameThread.IsRunning)
            {
                return;
            }
            catch (Exception exception)
            {
                terminal.WriteLine($"Command failed: {exception.Message}");
            }
        }
    }

    private static bool IsExitCommand(string commandLine)
    {
        var arguments = CommandLineParser.Parse(commandLine);

        if (arguments.Count != 1)
            return false;

        return arguments[0].Equals("exit", StringComparison.OrdinalIgnoreCase) ||
               arguments[0].Equals("quit", StringComparison.OrdinalIgnoreCase);
    }
}