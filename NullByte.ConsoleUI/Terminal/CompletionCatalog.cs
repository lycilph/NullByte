using NullByte.ConsoleUI.Commands;
using NullByte.Core.Domain;

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

    private volatile CompletionSnapshot snapshot = CompletionSnapshot.Empty;

    public CompletionCatalog(CommandRegistry registry, GameState gameState)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(gameState);

        this.registry = registry;
        this.gameState = gameState;
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

        var connectableServers =
            network
                .GetNeighbors(player.CurrentHostname)
                .Where(server =>
                    player.DiscoveredHostnames.Contains(
                        server.Hostname))
                .Select(server => server.Hostname)
                .ToArray();

        var files =
            gameState.FileSystem
                .ListFiles(player.CurrentHostname)
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
                [CompletionKind.BotnetNode] = botnetNodes
            };

        snapshot = new CompletionSnapshot(commands, values);
    }

    private IReadOnlyList<CommandCompletionInfo> BuildCommands()
    {
        var commands = new List<CommandCompletionInfo>();

        foreach (var command in registry.Commands)
        {
            var hidden =
                command.Debug &&
                !command.Name.Equals("debug",StringComparison.OrdinalIgnoreCase);

            foreach (var alias in command.Aliases.Prepend(command.Name))
                commands.Add(new CommandCompletionInfo(alias, command.Completions, hidden));
        }

        return commands;
    }

    public CompletionResult Complete(string input, int cursorIndex)
        => CompletionEngine.Complete(snapshot, input, cursorIndex);
}