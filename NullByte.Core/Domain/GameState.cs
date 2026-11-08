namespace NullByte.Core.Domain;

/// <summary>
/// Represents the complete mutable state of a running game.
/// </summary>
public sealed class GameState
{
    public GameState(Player player, Network network, Botnet botnet)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(network);

        if (!network.TryGetServer(player.HomeServerId, out _))
        {
            throw new ArgumentException(
                $"The player's home server " +
                $"'{player.HomeServerId}' does not exist " +
                $"in the network.",
                nameof(player));
        }

        if (!network.TryGetServer(player.CurrentServerId, out _))
        {
            throw new ArgumentException(
                $"The player's current server " +
                $"'{player.CurrentServerId}' does not exist " +
                $"in the network.",
                nameof(player));
        }

        Player = player;
        Network = network;
        Botnet = botnet;
    }

    /// <summary>
    /// Gets the player's state.
    /// </summary>
    public Player Player { get; }

    /// <summary>
    /// Gets the game network.
    /// </summary>
    public Network Network { get; }

    /// <summary>
    /// Gets the player's botnet.
    /// </summary>
    public Botnet Botnet { get; }
}