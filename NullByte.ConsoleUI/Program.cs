using System.Diagnostics;
using System.Text;
using NullByte.ConsoleUI.Terminal;
using NullByte.Core.Persistence;
using NullByte.Editor;
using NullByte.Scripting.Compilation;
using NullByte.Scripting.LanguageService;
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

        var sessionOptions = new GameSessionOptions()
        {
            Output = terminal,
            // TODO: remove this (only for debugging)
            Balance = new Core.Configuration.BalanceConfig() { GameSpeedMultiplier = 5.0, AnalyzeDuration = 5.0 }
        };
        await using var host = new GameHost(terminal, saves, sessionOptions);

        var terminalTask = terminal.RunAsync();

        EditorServer? editor = null;
        try
        {
            await host.StartAsync(resumeAutosave: !args.Contains("--new"));

            if (args.Contains("--vscode"))
                editor = await StartEditorAsync(host, terminal, args);

            await host.RunAsync();
        }
        finally
        {
            // Stop the editor first, so no events are raised while the game shuts down.
            if (editor is not null) await editor.DisposeAsync();
            
            // Save and stop the game while the terminal is still running, then stop the terminal.
            await host.DisposeAsync();
            terminal.RequestStop();
            await terminalTask;
        }

        Console.WriteLine("Everything stopped, bye");
        await Task.Delay(500);
    }

    private static async Task<EditorServer?> StartEditorAsync(GameHost host, ThreadSafeConsoleTerminal terminal, string[] args)
    {
        EditorServer? editor = null;
        try
        {
            int? requestedPort = null;
            if (GetOption(args, "--vscode-port") is { } portText)
            {
                if (!int.TryParse(portText, out var parsed) || parsed is < 1 or > 65535)
                    throw new ArgumentException($"'{portText}' is not a valid port.");
                requestedPort = parsed;
            }

            var options = new EditorServerOptions(
                Port: requestedPort ?? EditorServerOptions.DefaultPort,
                FallbackToFreePort: requestedPort is null,   // an explicit port is used exactly as given
                ConnectionFilePath: GetOption(args, "--vscode-file"));

            editor = new EditorServer(
                () => host.Session is { } s ? new EditorBackend(s.State, s.GameThread) : null,
                new ScriptCompiler(), new ScriptLanguageService(), options);

            await editor.StartAsync();
            host.SessionChanged += editor.OnGameChanged;
            editor.OnGameChanged();   // the first game is already running


            var warmUpTimer = Stopwatch.StartNew();
            _ = editor.WarmUp.ContinueWith(task => terminal.WriteLine(task.IsCompletedSuccessfully
                ? $"VS Code language features ready ({warmUpTimer.Elapsed.TotalSeconds:F1}s)."
                : $"VS Code language features failed to start: {task.Exception?.GetBaseException().Message}"),
                TaskScheduler.Default);

            terminal.WriteLine($"VS Code integration listening on {editor.Url} (connection file: {editor.ConnectionFilePath})");
            return editor;
        }
        catch (Exception exception)
        {
            terminal.WriteLine($"Could not start the VS Code integration: {exception.Message}");
            if (editor is not null) await editor.DisposeAsync();
            return null;
        }
    }

    // TODO: remove this (only for debugging)
    private static string GetSaveDirectory() => "Saves";
    //=> Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NullByte", "saves");

    private static string? GetOption(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}