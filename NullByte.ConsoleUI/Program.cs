using System.Text;
using NullByte.ConsoleUI.Terminal;
using NullByte.Core.Persistence;
using NullByte.Shell;
using NullByte.Shell.Persistence;

namespace NullByte.ConsoleUI;

public class Program
{
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.TreatControlCAsInput = true; // Ctrl+c no longer exists the application

        await using var terminal = new ThreadSafeConsoleTerminal(prompt: "root@home $ ", maximumHistoryLines: 2_000);

        var saves = new SaveManager(new FileSaveStore(GetSaveDirectory()), new SaveGameSerializer());
        var shutdownRequest = new ShutdownRequest();

        var sessionOptions = new GameSessionOptions()
        {
            Output = terminal,
            // TODO: remove this (only for debugging)
            Balance = new Core.Configuration.BalanceConfig() { GameSpeedMultiplier = 5.0, AnalyzeDuration = 5.0 }
        };
        await using var host = new GameHost(terminal, saves, shutdownRequest, sessionOptions);

        var terminalTask = terminal.RunAsync();

        try
        {
            await host.StartAsync(resumeAutosave: !args.Contains("--new"));
            await host.RunAsync();
        }
        finally
        {
            // Save and stop the game while the terminal is still running, then stop the terminal.
            await host.DisposeAsync();
            terminal.RequestStop();
            await terminalTask;
        }

        Console.WriteLine("Everything stopped, bye");
        await Task.Delay(500);
    }

    // TODO: remove this (only for debugging)
    private static string GetSaveDirectory() => "Saves";
        //=> Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NullByte", "saves");
}