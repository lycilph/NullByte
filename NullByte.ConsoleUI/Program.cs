using System.Text;
using NullByte.ConsoleUI.Terminal;
using NullByte.Core.Persistence;
using NullByte.Editor;
using NullByte.Scripting.Compilation;
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

        EditorServer? editor = null;
        try
        {
            await host.StartAsync(resumeAutosave: !args.Contains("--new"));

            if (args.Contains("--vscode"))
                editor = await StartEditorAsync(host, terminal);

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

    private static async Task<EditorServer?> StartEditorAsync(GameHost host, ThreadSafeConsoleTerminal terminal)
    {
        EditorServer? editor = null;
        try
        {
            var token = EditorToken.LoadOrCreate();
            editor = new EditorServer(
                () => host.Session is { } s ? new EditorBackend(s.State, s.GameThread) : null, token, new ScriptCompiler());

            await editor.StartAsync();
            host.SessionChanged += editor.OnGameChanged;
            editor.OnGameChanged();   // the first game is already running

            terminal.WriteLine($"VS Code integration listening on {editor.Url} (token: {token.Path})");
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
}