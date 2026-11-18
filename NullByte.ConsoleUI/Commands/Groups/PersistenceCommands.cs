using NullByte.ConsoleUI.Terminal;
using NullByte.Shell;
using NullByte.Shell.Persistence;

namespace NullByte.ConsoleUI.Commands.Groups;

public sealed class PersistenceCommands(
    GameSession session, SaveManager saves, LoadRequest loadRequest, IGameTerminal terminal) : ICommandGroup
{
    private const string DefaultSlot = "quicksave";

    public void Register(CommandRegistry registry)
    {
        registry.Register(new CommandDefinition(
            "save", 
            "save [name]",
            $"Save the game (default name '{DefaultSlot}').", 
            SaveAsync)
        { MinArgs = 1, MaxArgs = 1 });

        registry.Register(new CommandDefinition(
            "load",
            "load [name]",
            $"Load a saved game (default name '{DefaultSlot}'). Unsaved progress is lost.", 
            LoadAsync)
        { MinArgs = 1, MaxArgs = 1 });

        registry.Register(new CommandDefinition(
            "saves",
            "saves",
            "List saved games, newest first.", 
            ListAsync));
    }

    private async Task SaveAsync(IReadOnlyList<string> arguments)
    {
        var slot = SlotFrom(arguments);
        if (slot is null)
            return;

        try
        {
            await saves.SaveAsync(session, slot);
            terminal.WriteLine($"Game saved to '{slot}'.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            terminal.WriteLine($"Could not save: {exception.Message}");
        }
    }

    private Task LoadAsync(IReadOnlyList<string> arguments)
    {
        var slot = SlotFrom(arguments);
        if (slot is null)
            return Task.CompletedTask;

        // This command runs on the game thread of the session it would replace,
        // so it only asks. The host swaps the session when the command returns.
        terminal.WriteLine($"Loading '{slot}'...");
        loadRequest.Request(slot);

        return Task.CompletedTask;
    }

    private async Task ListAsync(IReadOnlyList<string> arguments)
    {
        var slots = await saves.ListAsync();

        if (slots.Count == 0)
        {
            terminal.WriteLine("No saved games.");
            return;
        }

        foreach (var slot in slots)
            terminal.WriteLine($"  {slot.Name,-34} {slot.SavedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
    }

    private string? SlotFrom(IReadOnlyList<string> arguments)
    {
        var slot = arguments.Count > 0 ? arguments[0] : DefaultSlot;

        if (SaveSlot.IsValidName(slot))
            return slot;

        terminal.WriteLine($"Invalid name. Use letters, digits, '-' and '_' (at most {SaveSlot.MaxLength} characters).");
        return null;
    }
}