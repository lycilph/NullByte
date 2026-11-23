using NullByte.ConsoleUI.Commands;
using NullByte.ConsoleUI.Terminal;
using NullByte.Core.Domain;
using NullByte.Core.Persistence;
using NullByte.Core.Services;
using NullByte.Shell;
using NullByte.Shell.Persistence;

namespace NullByte.ConsoleUI;

public sealed class GameHost(
    ThreadSafeConsoleTerminal terminal,
    SaveManager saveManager,
    GameSessionOptions baseOptions) : IAsyncDisposable
{
    private const string workingDirectoryResetMessage = "The current directory no longer exists. Back at /.";

    private readonly TimeSpan autosaveInterval = TimeSpan.FromMinutes(5);
    private readonly HostRequestMailbox hostRequests = new();
    public event Action? SessionChanged;

    private bool shutdownRequested;
    public GameSession? Session => game?.Session;
    private Game? currentGame;
    private Game? game
    {
        get => currentGame;
        set
        {
            currentGame = value;
            if (value is not null) SessionChanged?.Invoke();
        }
    }

    // Everything that belongs to one session and goes away with it.
    private sealed record Game(
        GameSession Session,
        CommandDispatcher Dispatcher,
        CompletionCatalog Completion,
        AutosaveService Autosave);

    public async Task StartAsync(bool resumeAutosave)
    {
        if (resumeAutosave)
        {
            var restored = await TryLoadAsync(AutosaveService.Slot, reportMissing: false);
            if (restored is not null)
            {
                terminal.WriteLine("Continuing from your autosave (start with --new for a new game).");
                game = await StartGameAsync(restored);
                terminal.WriteLine($"Game thread ready. Game time: {game.Session.Clock.Elapsed.TotalSeconds:F3}s");
                return;
            }
        }

        game = await StartGameAsync(restored: null);
        WriteStartupMessage();
    }

    public async Task RunAsync()
    {
        await foreach (var commandLine in terminal.ReadCommandsAsync())
        {
            if (string.IsNullOrWhiteSpace(commandLine))
                continue;

            var current = game ?? throw new InvalidOperationException("The game has not been started.");

            try
            {
                await current.Session.GameThread.InvokeAsync(async () =>
                {
                    try
                    {
                        terminal.SetInputEnabled(false);

                        // A directory deleted since the last command (by a script or the editor) takes the player back to the root.
                        var workingDirectory = new WorkingDirectory(current.Session.State);
                        if (workingDirectory.ResetIfMissing())
                            terminal.WriteLine(workingDirectoryResetMessage);

                        await current.Dispatcher.DispatchAsync(commandLine);

                        // The command itself may have deleted it.
                        if (workingDirectory.ResetIfMissing())
                            terminal.WriteLine(workingDirectoryResetMessage);

                        // Update terminal prompt
                        terminal.SetPrompt(BuildPrompt(current.Session.State));
                    }
                    finally
                    {
                        terminal.SetInputEnabled(true);
                        // Commands such as scan and connect change what can be completed.
                        current.Completion.Refresh();
                    }
                });
            }
            catch (OperationCanceledException) when (terminal.StoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (InvalidOperationException) when (!current.Session.GameThread.IsRunning)
            {
                return;
            }
            catch (Exception exception)
            {
                terminal.WriteLine($"Command failed: {exception.Message}");
            }

            if (hostRequests.Take() is not { } request)
                continue;

            switch (request)
            {
                case HostRequest.Shutdown:
                    shutdownRequested = true;
                    return;

                case HostRequest.Reset:
                    await ResetAsync();
                    break;

                case HostRequest.Load load:
                    await LoadAsync(load.Slot);
                    break;

                default:
                    throw new InvalidOperationException($"Unknown host request: {request.GetType().Name}");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        var current = game;
        game = null;

        if (current is null)
            return;

        await current.Autosave.DisposeAsync();

        if (shutdownRequested && current.Session.GameThread.IsRunning)
        {
            try
            {
                await saveManager.SaveAsync(current.Session, AutosaveService.Slot);
            }
            catch (Exception exception)
            {
                terminal.WriteLine($"Could not save on exit: {exception.Message}");
            }
        }

        await current.Session.DisposeAsync();
    }

    private async Task ResetAsync()
    {
        // Typed input would otherwise queue up and run in the new game.
        terminal.SetInputEnabled(false);

        var old = game ?? throw new InvalidOperationException("The game has not been started.");

        try
        {
            // Stop the old autosave service first. Otherwise it could recreate
            // the autosave immediately after it has been deleted.
            await old.Autosave.DisposeAsync();

            try
            {
                await saveManager.DeleteAsync(AutosaveService.Slot);
            }
            catch (Exception exception)
            {
                terminal.WriteLine($"Could not delete the autosave: {exception.Message}");
                terminal.WriteLine("The current game is unchanged.");

                // The session is still valid, so restore autosaving before
                // returning control to the player.
                var autosave = new AutosaveService(
                    saveManager,
                    old.Session,
                    terminal,
                    autosaveInterval);

                currentGame = old with { Autosave = autosave };
                return;
            }

            await old.Session.DisposeAsync();

            game = await StartGameAsync(restored: null);

            terminal.WriteLine("A new game has started.");
            terminal.WriteLine($"Game time: {game.Session.Clock.Elapsed.TotalSeconds:F3}s");
        }
        finally
        {
            terminal.SetInputEnabled(true);
        }
    }
    private async Task LoadAsync(string slot)
    {
        // Typed input would otherwise queue up and run in the new game.
        terminal.SetInputEnabled(false);

        try
        {
            // Read and validate first. Only a save that loads cleanly replaces the running game.
            var restored = await TryLoadAsync(slot, reportMissing: true);
            if (restored is null)
            {
                terminal.WriteLine("The current game is unchanged.");
                return;
            }

            var old = game!;
            await old.Autosave.DisposeAsync();
            await old.Session.DisposeAsync();

            game = await StartGameAsync(restored);

            terminal.WriteLine($"Loaded '{slot}'. Game time: {restored.GameTime.TotalSeconds:F3}s");
        }
        finally
        {
            terminal.SetInputEnabled(true);
        }
    }

    private async Task<RestoredGame?> TryLoadAsync(string slot, bool reportMissing)
    {
        try
        {
            var restored = await saveManager.LoadAsync(slot);

            if (restored is null && reportMissing)
                terminal.WriteLine($"No save named '{slot}'.");

            return restored;
        }
        catch (Exception exception) when (exception is SaveFormatException or IOException or UnauthorizedAccessException)
        {
            terminal.WriteLine($"Could not load '{slot}': {exception.Message}");
            return null;
        }
    }

    private async Task<Game> StartGameAsync(RestoredGame? restored)
    {
        var options = restored is null ? baseOptions : baseOptions.WithSave(restored);
        var session = GameSession.Create(options);

        var registry = CommandSetup.Create(session, terminal, hostRequests, saveManager);
        var dispatcher = new CommandDispatcher(registry, terminal);
        var completion = new CompletionCatalog(registry, session.State, saveManager);
        terminal.SetCompletionProvider(completion);

        await session.StartAsync();

        // Initialize the completion catalog
        await session.GameThread.InvokeAsync(() =>
        {
            completion.Refresh();
            return Task.CompletedTask;
        });

        terminal.SetPrompt(BuildPrompt(session.State));

        // An activity that ends by itself (the player ran out of money) changes the prompt too.
        session.Services.Activities.ActivityEnded += _ => terminal.SetPrompt(BuildPrompt(session.State));

        var autosave = new AutosaveService(saveManager, session, terminal, autosaveInterval);

        return new Game(session, dispatcher, completion, autosave);
    }

    private void WriteStartupMessage()
    {
        var clock = game!.Session.Clock;

        terminal.WriteLine("NullByte");
        terminal.WriteLine();
        terminal.WriteLine("Single-threaded simulation started.");
        terminal.WriteLine($"Game thread ready. Game time: {clock.Elapsed.TotalSeconds:F3}s");
        terminal.WriteLine();
        terminal.WriteLine("Type 'help' to see available commands ('debug' for other commands).");
        terminal.WriteLine("Tab completes commands and server names, Up/Down recalls earlier input.");
        terminal.WriteLine("The game autosaves every few minutes; 'save' and 'load' manage your own saves.");
        terminal.WriteLine();
    }

    private static string BuildPrompt(GameState state)
    {
        var player = state.Player;
        var directory = player.CurrentDirectory.Length > 0 ? ":/" + player.CurrentDirectory : "";

        var activity = state.Activity.Current is { } current ? $"[{ActivityFormatter.PromptTag(current)}] " : "";

        return $"{activity}root@{player.CurrentHostname}{directory} $ ";
    }
}