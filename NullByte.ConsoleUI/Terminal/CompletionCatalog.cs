using NullByte.ConsoleUI.Commands;
using NullByte.Core.Domain;
using NullByte.Core.Domain.Activities;
using NullByte.Shell.Persistence;

namespace NullByte.ConsoleUI.Terminal;

/// <summary>
/// Bridges game state and the terminal thread for tab completion.
/// </summary>
/// <remarks>
/// <see cref="Refresh"/> reads game state and must be called on the game thread.
/// It publishes an immutable <see cref="CompletionSnapshot"/>, which
/// <see cref="Complete"/> then reads from any thread without locks.
/// </remarks>
public sealed class CompletionCatalog : ICompletionProvider
{
    private readonly CommandRegistry registry;
    private readonly GameState gameState;
    private readonly ActivityCatalog activityCatalog;
    private readonly SaveManager saveManager;

    private volatile CompletionSnapshot snapshot = CompletionSnapshot.Empty;

    public CompletionCatalog(CommandRegistry registry, GameState gameState, ActivityCatalog activityCatalog, SaveManager saveManager)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(gameState);
        ArgumentNullException.ThrowIfNull(activityCatalog);
        ArgumentNullException.ThrowIfNull(saveManager);

        this.registry = registry;
        this.gameState = gameState;
        this.activityCatalog = activityCatalog;
        this.saveManager = saveManager;
    }

    /// <summary>
    /// Rebuilds the snapshot from current game state. Game thread only.
    /// </summary>
    public void Refresh()
    {
        var player = gameState.Player;
        var network = gameState.Network;

        var commands =
            BuildCommands();

        var knownServers =
            player.DiscoveredHostnames
                .Select(hostname =>
                    network.TryGetServer(
                        hostname,
                        out var server)
                        ? server
                        : null)
                .OfType<Server>()
                .Select(server => server.Hostname)
                .ToArray();

        // Mirrors NetworkService.Connect: adjacent servers, plus any backdoored server, as long as it is discovered.
        var connectableServers =
            network
                .GetNeighbors(player.CurrentHostname)
                .Concat(network.Servers.Where(server => server.HasBackdoor))
                .Where(server =>
                    player.DiscoveredHostnames.Contains(
                        server.Hostname) &&
                    !string.Equals(
                        server.Hostname,
                        player.CurrentHostname,
                        StringComparison.OrdinalIgnoreCase))
                .Select(server => server.Hostname)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

        var files =
            gameState.FileSystem
                .ListFiles(player.CurrentHostname)
                .ToArray();

        var directories =
            gameState.FileSystem
                .ListDirectories(player.CurrentHostname)
                .ToArray();

        var saves =
            saveManager
            .ListAsync()
            .Result
            .Select(s => s.Name)
            .ToArray();

        var botnetNodes =
            Enumerable
                .Range(1, gameState.Botnet.NodeCount)
                .Select(node => node.ToString())
                .ToArray();

        var values = new Dictionary<CompletionKind, IReadOnlyList<string>>
            {
                [CompletionKind.ConnectableServer] = connectableServers,
                [CompletionKind.KnownServer] = knownServers,
                [CompletionKind.Filename] = files,
                [CompletionKind.Directory] = directories,
                [CompletionKind.SaveGame] = saves,
                [CompletionKind.BotnetNode] = botnetNodes,
                [CompletionKind.UniversityCourse] = activityCatalog.Courses.Where(c => c.Kind == FacilityKind.University).Select(c => c.Name).ToArray(),
                [CompletionKind.University] = activityCatalog.Facilities.Where(f => f.Kind == FacilityKind.University).Select(f => f.Name).ToArray(),
                [CompletionKind.GymCourse] = activityCatalog.Courses.Where(c => c.Kind == FacilityKind.Gym).Select(c => c.Name).ToArray(),
                [CompletionKind.Gym] = activityCatalog.Facilities.Where(f => f.Kind == FacilityKind.Gym).Select(f => f.Name).ToArray(),
                [CompletionKind.Company] = activityCatalog.Companies.Select(c => c.Name).ToArray(),
                [CompletionKind.Career] = activityCatalog.Companies.SelectMany(c => c.Careers).Select(c => c.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                [CompletionKind.Crime] = activityCatalog.Crimes.Select(c => c.Name).ToArray()
            };

        snapshot = new CompletionSnapshot(commands, values, player.CurrentDirectory);
    }

    private IReadOnlyList<CommandCompletionInfo> BuildCommands()
    {
        var commands = new List<CommandCompletionInfo>();

        foreach (var command in registry.Commands)
        {
            var hidden =
                command.Debug &&
                !command.Name.Equals("debug", StringComparison.OrdinalIgnoreCase);

            foreach (var alias in command.Aliases.Prepend(command.Name))
                commands.Add(new CommandCompletionInfo(alias, command.Completions, hidden));
        }

        return commands;
    }

    public CompletionResult Complete(string input, int cursorIndex)
        => CompletionEngine.Complete(snapshot, input, cursorIndex);
}