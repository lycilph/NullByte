using NullByte.ConsoleUI.Terminal;

namespace NullByte.ConsoleUI.Commands;

public sealed class CommandDefinition
{
    public CommandDefinition(
        string name,
        string usage,
        string description,
        Func<CommandContext, IReadOnlyList<string>, Task> handler,
        params string[] aliases)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(usage);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(aliases);

        Name = name;
        Usage = usage;
        Description = description;
        Handler = handler;
        Aliases = aliases;
    }

    public string Name { get; }
    public string Usage { get; }
    public string Description { get; }
    public Func<CommandContext, IReadOnlyList<string>, Task> Handler { get; }
    public IReadOnlyList<string> Aliases { get; }

    /// <summary>Is this a debugging command.</summary>
    public bool Debug { get; init; } = false;

    /// <summary>What the command's first argument can be tab-completed to.</summary>
    public ArgumentCompletionKind ArgumentCompletion { get; init; } = ArgumentCompletionKind.None;
}