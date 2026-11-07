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
    private static readonly string[] ExitCommands = ["exit", "quit"];

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

        var commands = new List<CommandCompletionInfo>();

        foreach (var command in registry.Commands)
        {
            // Debug commands stay hidden (as in 'help'), except 'debug' itself.
            var hidden = command.Debug && !command.Name.Equals("debug", StringComparison.OrdinalIgnoreCase);

            commands.Add(new CommandCompletionInfo(command.Name, command.ArgumentCompletion, hidden));

            foreach (var alias in command.Aliases)
                commands.Add(new CommandCompletionInfo(alias, command.ArgumentCompletion, hidden));
        }

        // 'exit' and 'quit' are handled by Program, not by the registry.
        foreach (var exit in ExitCommands)
            commands.Add(new CommandCompletionInfo(exit, ArgumentCompletionKind.None));

        var known = player.DiscoveredServerIds
            .Select(id => network.TryGetServer(id, out var server) ? server : null)
            .OfType<Server>()
            .ToArray();

        var connectable = network.GetNeighbors(player.CurrentServerId)
            .Where(server => player.DiscoveredServerIds.Contains(server.Id))
            .Select(server => server.Hostname)
            .ToArray();

        snapshot = new CompletionSnapshot(
            commands,
            connectable,
            known.Select(server => server.Hostname).ToArray());
    }

    public CompletionResult Complete(string input, int cursorIndex)
        => CompletionEngine.Complete(snapshot, input, cursorIndex);
}