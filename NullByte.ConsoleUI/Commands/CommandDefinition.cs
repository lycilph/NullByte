namespace NullByte.ConsoleUI.Commands;

public sealed class CommandDefinition
{
    public CommandDefinition(
        string name,
        string usage,
        string description,
        Func<CommandContext, IReadOnlyList<string>, Task> handler,
        bool debug = false,
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
        Debug = debug;
        Aliases = aliases;
    }

    public string Name { get; }
    public string Usage { get; }
    public string Description { get; }
    public Func<CommandContext, IReadOnlyList<string>, Task> Handler { get; }
    public bool Debug { get; }
    public IReadOnlyList<string> Aliases { get; }
}