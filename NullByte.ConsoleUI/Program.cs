using System.Text;
using NullByte.ConsoleUI.Commands;
using NullByte.ConsoleUI.Terminal;
using NullByte.Core.Services;
using NullByte.Core.Time;
using NullByte.Shell;

namespace NullByte.ConsoleUI;

public class Program
{
    static async Task Main()
    {
        Console.OutputEncoding = Encoding.UTF8;

        await using var terminal = new ThreadSafeConsoleTerminal(prompt: "root@home $ ", maximumHistoryLines: 2_000);

        var sessionOptions = new GameSessionOptions() { Output = terminal };
        await using var session = GameSession.Create(sessionOptions);

        var shutdownRequest = new ShutdownRequest();

        var commandRegistry = CommandSetup.Create(session, terminal, shutdownRequest);
        var commandDispatcher = new CommandDispatcher(commandRegistry, terminal);
        var completionCatalog = new CompletionCatalog(commandRegistry, session.State);
        terminal.SetCompletionProvider(completionCatalog);

        var terminalTask = terminal.RunAsync();

        try
        {
            await session.StartAsync();

            // Initialize the completion catalog
            await session.GameThread.InvokeAsync(() =>
            {
                completionCatalog.Refresh();
                return Task.CompletedTask;
            });

            WriteStartupMessage(terminal, session.Clock);

            await ProcessCommandsAsync(terminal, session, commandDispatcher, completionCatalog, shutdownRequest);
        }
        finally
        {
            terminal.RequestStop();
            await session.StopAsync();
            await terminalTask;
        }

        Console.WriteLine("Everything stopped, bye");
        await Task.Delay(500);
    }

    private static void WriteStartupMessage(IGameOutput output, IGameClock clock)
    {
        output.WriteLine("NullByte");
        output.WriteLine();
        output.WriteLine("Single-threaded simulation started.");
        output.WriteLine($"Game thread ready. Game time: {clock.Elapsed.TotalSeconds:F3}s");
        output.WriteLine();
        output.WriteLine("Type 'help' to see available commands ('debug' for other commands).");
        output.WriteLine("Tab completes commands and server names, Up/Down recalls earlier input.");
        output.WriteLine();
    }

    private static async Task ProcessCommandsAsync(
        IGameTerminal terminal,
        GameSession session,
        CommandDispatcher dispatcher,
        CompletionCatalog completionCatalog,
        ShutdownRequest shutdownRequest)
    {
        await foreach (var commandLine in terminal.ReadCommandsAsync())
        {
            if (string.IsNullOrWhiteSpace(commandLine))
                continue;

            try
            {
                await session.GameThread.InvokeAsync(async () =>
                {
                    try
                    {
                        terminal.SetInputEnabled(false);
                        await dispatcher.DispatchAsync(commandLine);

                        // Update terminal prompt
                        terminal.SetPrompt($"root@{session.State.Player.CurrentHostname} $ ");
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
                when (!session.GameThread.IsRunning)
            {
                return;
            }
            catch (Exception exception)
            {
                terminal.WriteLine($"Command failed: {exception.Message}");
            }

            if (shutdownRequest.IsRequested)
                break;
        }
    }
}