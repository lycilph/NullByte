using System.Diagnostics.CodeAnalysis;
using NullByte.Core.Domain.Progression;

namespace NullByte.ConsoleUI.Commands;

public sealed class CommandRegistry
{
    private readonly Dictionary<string, CommandDefinition> commands = new(StringComparer.OrdinalIgnoreCase);
    private int maxWords = 1;
    private readonly ProgressionState? progression;

    /// <param name="progression">What the player has unlocked. Without it every command is available.</param>
    public CommandRegistry(ProgressionState? progression = null) => this.progression = progression;

    /// <summary>The commands the player can use now. Commands the story has not unlocked yet are left out.</summary>
    public IReadOnlyCollection<CommandDefinition> Commands =>
        commands.Values
            .Distinct()
            .Where(IsAvailable)
            .OrderBy(command => command.Name)
            .ToArray();

    public void Register(CommandDefinition command)
    {
        ArgumentNullException.ThrowIfNull(command);

        RegisterName(command.Name, command);
        foreach (var alias in command.Aliases)
            RegisterName(alias, command);
    }

    public bool TryGet(string name, out CommandDefinition? command) => commands.TryGetValue(name, out command);

    /// <summary>
    /// Finds the longest registered name that is a prefix of the typed tokens,
    /// so "botnet buy" wins over "botnet".
    /// </summary>
    public bool TryResolve(
        IReadOnlyList<string> tokens,
        [NotNullWhen(true)] out CommandDefinition? command,
        out int wordsConsumed)
    {
        for (var n = Math.Min(maxWords, tokens.Count); n >= 1; n--)
        {
            if (commands.TryGetValue(string.Join(' ', tokens.Take(n)), out command) && IsAvailable(command))
            {
                wordsConsumed = n;
                return true;
            }
        }

        command = null;
        wordsConsumed = 0;
        return false;
    }

    /// <summary>Whether the story has unlocked the command. A command without a feature is always available.</summary>
    public bool IsAvailable(CommandDefinition command)
        => progression is null
           || !CommandUnlocks.TryGetFeature(command, out var feature)
           || progression.IsUnlocked(feature);

    /// <summary>
    /// Finds a command the typed tokens name but the story has not unlocked yet, so the player
    /// can be told it is coming rather than that it does not exist.
    /// </summary>
    public bool TryGetLocked(IReadOnlyList<string> tokens, [NotNullWhen(true)] out CommandDefinition? command)
    {
        for (var n = Math.Min(maxWords, tokens.Count); n >= 1; n--)
        {
            if (commands.TryGetValue(string.Join(' ', tokens.Take(n)), out command) && !IsAvailable(command))
                return true;
        }

        command = null;
        return false;
    }

    /// <summary>Commands whose name extends this one: "botnet" -> "botnet buy", "botnet upgrade".</summary>
    public IEnumerable<CommandDefinition> Subcommands(CommandDefinition command)
        => Commands.Where(c => c.Name.StartsWith(command.Name + " ", StringComparison.OrdinalIgnoreCase));

    private void RegisterName(string name, CommandDefinition command)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0 || string.Join(' ', words) != name)
            throw new ArgumentException($"Invalid command name '{name}'.", nameof(name));

        if (!commands.TryAdd(name, command))
            throw new InvalidOperationException($"A command named '{name}' is already registered.");

        maxWords = Math.Max(maxWords, words.Length);
    }
}