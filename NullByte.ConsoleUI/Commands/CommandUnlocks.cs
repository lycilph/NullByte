using NullByte.Core.Domain.Progression;

namespace NullByte.ConsoleUI.Commands;

/// <summary>
/// Which story feature unlocks each command. This table is the one place that decides it:
/// a command that is not listed here is always available.
/// </summary>
/// <remarks>
/// Keep it in step with STORY.md. A test checks that every command is either listed here or
/// explicitly named as always available, so a new command cannot slip through unnoticed.
/// </remarks>
public static class CommandUnlocks
{
    // Keyed by the first word of the command name: "botnet buy" is unlocked together with "botnet".
    private static readonly Dictionary<string, GameFeature> Gated = new(StringComparer.OrdinalIgnoreCase)
    {
        ["scan"] = GameFeature.Scan,

        ["connect"] = GameFeature.Connect,
        ["home"] = GameFeature.Connect,
        ["analyze"] = GameFeature.Connect,

        ["nuke"] = GameFeature.Nuke,
        ["hack"] = GameFeature.Hack,
        ["weaken"] = GameFeature.Weaken,
        ["grow"] = GameFeature.Grow,
        ["backdoor"] = GameFeature.Backdoor,

        ["ls"] = GameFeature.Files,
        ["cd"] = GameFeature.Files,
        ["pwd"] = GameFeature.Files,
        ["cat"] = GameFeature.Files,
        ["rm"] = GameFeature.Files,
        ["mkdir"] = GameFeature.Files,
        ["rmdir"] = GameFeature.Files,
        ["mv"] = GameFeature.Files,

        ["edit"] = GameFeature.Scripts,
        ["scp"] = GameFeature.Scripts,
        ["run"] = GameFeature.Scripts,
        ["mem"] = GameFeature.Scripts,
        ["free"] = GameFeature.Scripts,
        ["ps"] = GameFeature.Scripts,
        ["kill"] = GameFeature.Scripts,
        ["killall"] = GameFeature.Scripts,
        ["vscode"] = GameFeature.Scripts,

        ["botnet"] = GameFeature.Botnet,
        ["cloud"] = GameFeature.Cloud,

        // stop, focus and activity manage whatever the player is doing, which starts with learning.
        ["study"] = GameFeature.Learning,
        ["train"] = GameFeature.Learning,
        ["skills"] = GameFeature.Learning,
        ["stop"] = GameFeature.Learning,
        ["focus"] = GameFeature.Learning,
        ["activity"] = GameFeature.Learning,
        ["work"] = GameFeature.Work,
        ["crime"] = GameFeature.Crime
    };

    /// <summary>Commands that never need unlocking: the player needs them from the very first moment.</summary>
    public static IReadOnlySet<string> AlwaysAvailable { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "help", "exit", "clear", "status", "money", "journey",
        "save", "saves", "load", "reset"
    };

    public static bool TryGetFeature(CommandDefinition command, out GameFeature feature)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Debug commands are developer tools and ignore the story.
        if (command.Debug)
        {
            feature = default;
            return false;
        }

        var firstWord = command.Name.Split(' ', 2)[0];

        return Gated.TryGetValue(firstWord, out feature);
    }

    /// <summary>True if the command has been given an unlock decision, either a feature or "always available".</summary>
    public static bool IsDecided(CommandDefinition command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var firstWord = command.Name.Split(' ', 2)[0];

        return command.Debug || Gated.ContainsKey(firstWord) || AlwaysAvailable.Contains(firstWord);
    }
}
