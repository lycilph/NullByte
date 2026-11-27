using NullByte.Core.Domain.Activities;

namespace NullByte.Core.Domain;

/// <summary>
/// Represents the complete mutable state of a running game.
/// </summary>
public sealed class GameState
{
    public GameState(Player player, Network network)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(network);

        if (!network.TryGetServer(player.HomeServerId, out _))
            throw new ArgumentException($"The player's home server '{player.HomeServerId}' does not exist in the network.", nameof(player));

        if (!network.TryGetServer(player.CurrentServerId, out _))
            throw new ArgumentException($"The player's current server '{player.CurrentServerId}' does not exist in the network.", nameof(player));

        if (!player.DiscoveredServerIds.All(id => network.TryGetServer(id, out _)))
            throw new ArgumentException("The player references a discovered server that does not exist in the network.", nameof(player));

        Player = player;
        Network = network;
        Botnet = new Botnet();
        Activity = new ActivityState();
        Employment = new Employment();
        FileSystem = new VirtualFileSystem(network);
    }

    public Player Player { get; }
    public Network Network { get; }
    public Botnet Botnet { get; }
    public ActivityState Activity { get; }
    public Employment Employment { get; }
    public VirtualFileSystem FileSystem { get; }
}