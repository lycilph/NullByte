namespace NullByte.ConsoleUI.Commands;

public sealed class CommandRegistry
{
    private readonly Dictionary<string, CommandDefinition> commands = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<CommandDefinition> Commands =>
        commands.Values
            .Distinct()
            .OrderBy(command => command.Name)
            .ToArray();

    public void Register(CommandDefinition command)
    {
        ArgumentNullException.ThrowIfNull(command);

        RegisterName(command.Name, command);

        foreach (var alias in command.Aliases)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(alias);
            RegisterName(alias, command);
        }
    }

    public bool TryGet(string name, out CommandDefinition? command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return commands.TryGetValue(name, out command);
    }

    private void RegisterName(string name, CommandDefinition command)
    {
        if (!commands.TryAdd(name, command))
            throw new InvalidOperationException($"A command named '{name}' is already registered.");
    }
}