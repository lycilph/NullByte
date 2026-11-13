using NullByte.ConsoleUI.Terminal;

namespace NullByte.ConsoleUI.Commands;

public sealed class CommandDefinition
{
    public CommandDefinition(
        string name,
        string usage,
        string description,
        Func<IReadOnlyList<string>, Task> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(usage);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(handler);

        Name = name;
        Usage = usage;
        Description = description;
        Handler = handler;
    }

    public string Name { get; }
    public string Usage { get; }
    public string Description { get; }
    public Func<IReadOnlyList<string>, Task> Handler { get; }

    /// <summary>Alternative versions of the command</summary>
    public IReadOnlyList<string> Aliases { get; init;  } = [];
    /// <summary>Checks if the number of arguments are correct</summary>
    public int MinArgs { get; init; } = 0;
    public int MaxArgs { get; init; } = 0; // int.MaxValue for variadic
    /// <summary>Is this a debugging command.</summary>
    public bool Debug { get; init; } = false;
    /// <summary>Describes tab completion for the command's positional arguments.</summary>
    public IReadOnlyList<ArgumentCompletion> Completions { get; init; } = [];
}