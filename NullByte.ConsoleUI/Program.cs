using System.Diagnostics;
using System.Text;
using NullByte.ConsoleUI.Configuration;
using NullByte.ConsoleUI.Startup;
using NullByte.ConsoleUI.Terminal;
using NullByte.Core.Configuration;
using NullByte.Core.Persistence;
using NullByte.Core.Services;
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
            Console.Write("Press any key to continue...");
            Console.ReadKey();
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

        var scriptCompiler = new ScriptCompiler();

        var sessionOptions = new GameSessionOptions
        {
            Output = terminal,
            Balance = new BalanceConfig { GameSpeedMultiplier = settings.Development.GameSpeedMultiplier },
            ScriptCompiler = scriptCompiler
        };

        await using var host = new GameHost(
            terminal,
            saves,
            sessionOptions,
            settings);

        EditorServer? editor = settings.Editor.Enabled
            ? CreateEditorServer(host, settings.Editor, scriptCompiler)
            : null;

        var editorWarmUp = editor?.StartWarmUp();
        var warmUpEntries = new List<(string Name, TimeSpan Duration, Exception? Error)>();
        var totalWarmUpDuration = TimeSpan.Zero;

        async Task WarmUpAsync(CancellationToken cancellationToken)
        {
            var totalTimer = Stopwatch.StartNew();

            Task<(string Name, TimeSpan Duration, Exception? Error)> compilerTask = 
                ObserveWarmUpAsync(scriptCompiler.WarmUpAsync(), "Script Compiler", cancellationToken);
            Task<(string Name, TimeSpan Duration, Exception? Error)>? editorTask = editorWarmUp is null
                ? null : ObserveWarmUpAsync(editorWarmUp, "Editor language service", cancellationToken);
            
            var completed = editorTask is null
                ? [await compilerTask]
                : await Task.WhenAll(compilerTask, editorTask);

            totalTimer.Stop();
            warmUpEntries = completed.ToList();
            totalWarmUpDuration = totalTimer.Elapsed;

            var failures = warmUpEntries
                .Where(entry => entry.Error is not null)
                .Select(entry => entry.Error!)
                .ToArray();

            if (failures.Length > 0)
                throw new AggregateException("One or more startup warm-up tasks failed.", failures);
        }

        try
        {
            if (settings.Startup.ShowBootSequence)
            {
                Console.Clear();
                await new BootSequence(Console.Out).RunAsync(warmUp: WarmUpAsync);
            }
            else
            {
                Console.WriteLine("Warming up");
                await WarmUpAsync(CancellationToken.None);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Console.Error.WriteLine(FormatWarmUpSummary(warmUpEntries, totalWarmUpDuration));
            Console.Error.WriteLine();
            Console.Error.WriteLine("Startup aborted because a required warm-up task failed.");
            Console.Error.WriteLine(exception.GetBaseException().Message);

            if (editor is not null)
            {
                try
                {
                    await editor.DisposeAsync();
                }
                catch (Exception cleanupException)
                {
                    Console.Error.WriteLine("Could not cleanly stop the editor service: " + cleanupException.Message);
                }
            }

            return;
        }

        var terminalTask = terminal.RunAsync();
        terminal.WriteLine(FormatWarmUpSummary(warmUpEntries, totalWarmUpDuration));

        try
        {
            if (editor is not null)
                await StartEditorAsync(host, terminal, editor);

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

    private static async Task<(string Name, TimeSpan Duration, Exception? Error)> ObserveWarmUpAsync(
        Task warmUpTask,
        string message,
        CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        try
        {
            await warmUpTask.WaitAsync(cancellationToken);
            return (message, timer.Elapsed, null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return (message, timer.Elapsed, exception);
        }
    }

    private static string FormatWarmUpSummary(
        IReadOnlyList<(string Name, TimeSpan Duration, Exception? Error)> entries,
        TimeSpan totalDuration)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("STARTUP DIAGNOSTICS");
        foreach (var entry in entries)
        {
            var status = entry.Error is null
                ? $"{entry.Duration.TotalSeconds:F1}s"
                : $"FAILED ({entry.Error.Message})";
            sb.AppendLine($"{entry.Name,-26} {status}");
        }

        sb.AppendLine($"{"Total warm-up time",-26} {totalDuration.TotalSeconds:F1}s");
        return sb.ToString();
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

    private static EditorServer CreateEditorServer(GameHost host, EditorSettings settings, IScriptCompiler scriptCompiler)
    {
        var requestedPort = settings.Port;
        var options = new EditorServerOptions(
            Port: requestedPort ?? EditorServerOptions.DefaultPort,
            FallbackToFreePort: requestedPort is null,
            ConnectionFilePath: settings.ConnectionFilePath);

        return new EditorServer(
            () => host.Session is { } session
                ? new EditorBackend(session.State, session.GameThread, session.Services.Scripts)
                : null,
            scriptCompiler,
            new ScriptLanguageService(),
            options);
    }

    private static async Task StartEditorAsync(GameHost host, ThreadSafeConsoleTerminal terminal, EditorServer editor)
    {
        try
        {
            await editor.StartAsync();

            host.ExternalEditorStatus = editor;
            host.SessionChanged += editor.OnGameChanged;
            editor.OnGameChanged();

            terminal.WriteLine(
                $"VS Code integration listening on {editor.Url} "
                + $"(connection file: {editor.ConnectionFilePath})");
        }
        catch (Exception exception)
        {
            terminal.WriteLine("Could not start the VS Code integration: " + exception.Message);
        }
    }
}
