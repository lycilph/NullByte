using NullByte.ConsoleUI.Commands.Groups;
using NullByte.ConsoleUI.Terminal;
using NullByte.Core.Services;
using NullByte.Shell;
using NullByte.Shell.Persistence;

namespace NullByte.ConsoleUI.Commands;

public static class CommandSetup
{
    public static CommandRegistry Create(
        GameSession session,
        IGameTerminal terminal,
        ShutdownRequest? shutdownRequest = null,
        SaveManager? saves = null,
        LoadRequest? loadRequest = null)
    {
        var registry = new CommandRegistry();

        var workingDirectory = new WorkingDirectory(session.State);

        List<ICommandGroup> groups =
        [
            new DebugCommands(session.State.Player, session.Services.Network, session.State.FileSystem, session.Clock, registry, terminal),
            new SystemCommands(registry, terminal, shutdownRequest),
            new PlayerCommands(session.State.Player, session.Services.Network, session.Services.HackingFormulas, terminal),
            new NetworkCommands(session.Clock, session.Services.Network, session.Services.NetworkFormulas, terminal),
            new FileCommands(session.State.FileSystem, session.Services.Network, workingDirectory, terminal),
            new ScriptCommands(session.Services.Network, session.Services.Scripts, terminal),
            new ActionCommands(session.State.Player, session.Clock, session.Services.HackingFormulas, session.Services.Actions, session.Services.Network, terminal),
            new BotnetCommands(session.State.Botnet, session.Services.Botnet, session.Services.BotnetFormulas, terminal)
        ];
        
        // Hosts that cannot save (and tests) simply leave these out.
        if (saves is not null && loadRequest is not null)   // new
            groups.Add(new PersistenceCommands(session, saves, loadRequest, terminal));

        foreach (var group in groups)
            group.Register(registry);

        return registry;
    }
}
