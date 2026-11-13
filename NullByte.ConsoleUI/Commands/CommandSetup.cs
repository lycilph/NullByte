using NullByte.ConsoleUI.Commands.Groups;
using NullByte.ConsoleUI.Terminal;
using NullByte.Shell;

namespace NullByte.ConsoleUI.Commands;

public static class CommandSetup
{
    public static CommandRegistry Create(GameSession session, IGameTerminal terminal)
    {
        var registry = new CommandRegistry();

        ICommandGroup[] groups =
        [
            new DebugCommands(session.Services.Network, session.State.FileSystem, session.Clock, registry, terminal),
            new SystemCommands(registry, terminal),
            new PlayerCommands(session.State.Player, session.Services.Network, session.Services.HackingFormulas, terminal),
            new NetworkCommands(session.Services.Network, terminal),
            new FileCommands(session.State.FileSystem, session.Services.Network, terminal),
            new ScriptCommands(session.Services.Network, session.Services.Scripts, terminal),
            new ActionCommands(session.State.Player, session.Clock, session.Services.HackingFormulas, session.Services.Actions, session.Services.Network, terminal),
            new BotnetCommands(session.State.Botnet, session.Services.Botnet, session.Services.BotnetFormulas, terminal)
        ];

        foreach (var group in groups)
            group.Register(registry);

        return registry;
    }
}
