using System.Diagnostics;
using System.Text;
using NullByte.ConsoleUI.Configuration;
using NullByte.ConsoleUI.Terminal;
using NullByte.ConsoleUI.Startup;
using NullByte.Core.Configuration;
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
        var applicationOptions = LoadApplicationOptions(args);
        if (applicationOptions is null)
        {
            return;
        }
        if (applicationOptions.WriteDefaultSettings)
        {
            WriteDefaultSettings();
            return;
        }

        var settings = applicationOptions.Settings;

        Console.OutputEncoding = Encoding.UTF8;
        Console.TreatControlCAsInput = true; // Ctrl+C is handled as terminal input rather than terminating the process immediately.

        await using var terminal = new ThreadSafeConsoleTerminal(
            prompt: "root@home $ ",
            maximumHistoryLines: 2_000);

        var saves = new SaveManager(
            new FileSaveStore(settings.SaveDirectory),
            new SaveGameSerializer());

        var sessionOptions = new GameSessionOptions
        {
            Output = terminal,
            Balance = new BalanceConfig { GameSpeedMultiplier = settings.Development.GameSpeedMultiplier }
        };

        await using var host = new GameHost(
            terminal,
            saves,
            sessionOptions,
            settings);

        if (settings.Startup.ShowBootSequence)
        {
            Console.Clear();
            await new BootSequence(Console.Out).RunAsync();
        }

        var terminalTask = terminal.RunAsync();
        EditorServer? editor = null;

        try
        {
            if (settings.Editor.Enabled)
                editor = await StartEditorAsync(host, terminal, settings.Editor);

            await host.StartAsync(resumeAutosave: !applicationOptions.StartNewGame);
            await host.RunAsync();
        }
        finally
        {
            // Stop the editor first, so no events are raised while the game shuts down.
            if (editor is not null)
                await editor.DisposeAsync();

            // Save and stop the game while the terminal is still running, then stop the terminal.
            await host.DisposeAsync();

            terminal.RequestStop();
            await terminalTask;
        }

        Console.WriteLine("Everything stopped, bye");
        await Task.Delay(500);
    }

    private static ApplicationOptions? LoadApplicationOptions(string[] args)
    {
        try
        {
            return SettingsLoader.Load(args);
        }
        catch (SettingsValidationException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return null;
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine($"Invalid command line: {exception.Message}");
            return null;
        }
        catch (InvalidOperationException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return null;
        }
        catch (IOException exception)
        {
            Console.Error.WriteLine($"Could not read application settings: " + exception.Message);
            return null;
        }
        catch (UnauthorizedAccessException exception)
        {
            Console.Error.WriteLine($"Could not read application settings: " + exception.Message);
            return null;
        }
    }

    private static void WriteDefaultSettings()
    {
        try
        {
            var path = SettingsFileLoader.WriteDefault();

            Console.WriteLine($"Default settings written to '{path}'.");
        }
        catch (InvalidOperationException exception)
        {
            Console.Error.WriteLine(exception.Message);
        }
        catch (IOException exception)
        {
            Console.Error.WriteLine("Could not write application settings: " + exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            Console.Error.WriteLine("Could not write application settings: " + exception.Message);
        }
    }

    private static async Task<EditorServer?> StartEditorAsync(GameHost host, ThreadSafeConsoleTerminal terminal, EditorSettings settings)
    {
        EditorServer? editor = null;

        try
        {
            var requestedPort = settings.Port;

            var options = new EditorServerOptions(
                Port: requestedPort ?? EditorServerOptions.DefaultPort,
                // If no port was explicitly configured, try the
                // default first and then let the OS choose one.
                FallbackToFreePort: requestedPort is null,
                ConnectionFilePath: settings.ConnectionFilePath);

            editor = new EditorServer(
                () =>
                    host.Session is { } session
                        ? new EditorBackend(session.State, session.GameThread, session.Services.Scripts)
                        : null,
                new ScriptCompiler(),
                new ScriptLanguageService(),
                options);

            await editor.StartAsync();

            host.ExternalEditorStatus = editor;
            host.SessionChanged += editor.OnGameChanged;

            editor.OnGameChanged();

            var warmUpTimer = Stopwatch.StartNew();

            _ = editor.WarmUp.ContinueWith(
                task => terminal.WriteLine(task.IsCompletedSuccessfully
                    ? $"VS Code language features ready ({warmUpTimer.Elapsed.TotalSeconds:F1}s)."
                    : "VS Code language features failed to start: " + task.Exception?.GetBaseException().Message),
                TaskScheduler.Default);

            terminal.WriteLine(
                $"VS Code integration listening on {editor.Url} "
                + $"(connection file: "
                + $"{editor.ConnectionFilePath})");

            return editor;
        }
        catch (Exception exception)
        {
            terminal.WriteLine("Could not start the VS Code integration: " + exception.Message);

            if (editor is not null)
                await editor.DisposeAsync();

            return null;
        }
    }
}