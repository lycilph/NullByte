using NullByte.ConsoleUI.Commands.Groups;
using NullByte.ConsoleUI.Configuration;
using NullByte.ConsoleUI.Terminal;
using NullByte.Core.Services;
using NullByte.Editor;
using NullByte.Shell;
using NullByte.Shell.Persistence;

namespace NullByte.ConsoleUI.Commands;

public static class CommandSetup
{
    public static CommandRegistry Create(
        GameSession session,
        IGameTerminal terminal,
        HostRequestMailbox hostRequests,
        SaveManager? saves = null,
        IExternalEditorStatus? externalEditorStatus = null,
        AppSettings? settings = null)
    {
        var registry = new CommandRegistry(session.State.Narrative);
        var workingDirectory = new WorkingDirectory(session.State);
        var formulas = session.Services.Formulas;

        List<ICommandGroup> groups =
        [
            new DebugCommands(session.State.Player, session.Clock, registry, terminal, externalEditorStatus),
            new SystemCommands(registry, terminal, hostRequests, settings, session.Narrative),
            new PlayerCommands(session.State.Player, session.Services.Network, formulas.Skills, terminal),
            new NetworkCommands(session.Clock, session.Services.Network, formulas.Network, terminal),
            new FileCommands(session.State.FileSystem, session.Services.Network, workingDirectory, terminal),
            new ScriptCommands(session.Services.Network, session.Services.Scripts, workingDirectory, terminal),
            new ActionCommands(session.State.Player, session.Clock, formulas.ServerOperations, session.Services.Actions, session.Services.Network, terminal),
            new BotnetCommands(session.State.Botnet, session.Services.Botnet, formulas.Botnet, terminal),
            new CloudCommands(session.Services.CloudServers, terminal),
            new ActivityCommands(session.Services.Activities, session.State.Player, formulas.Skills, terminal)
        ];

        // Hosts that cannot save (and tests) simply leave these out.
        if (saves is not null)
            groups.Add(new PersistenceCommands(session, saves, hostRequests, terminal));

        foreach (var group in groups)
            group.Register(registry);

        return registry;
    }
}
