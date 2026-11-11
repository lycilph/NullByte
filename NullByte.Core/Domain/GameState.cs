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

        if (!network.TryGetServer(player.HomeHostname, out _))
        {
            throw new ArgumentException(
                $"The player's home server " +
                $"'{player.HomeHostname}' does not exist " +
                $"in the network.",
                nameof(player));
        }

        if (!network.TryGetServer(player.CurrentHostname, out _))
        {
            throw new ArgumentException(
                $"The player's current server " +
                $"'{player.CurrentHostname}' does not exist " +
                $"in the network.",
                nameof(player));
        }

        Player = player;
        Network = network;
        Botnet = new Botnet();
        FileSystem = new VirtualFileSystem(network);
    }

    /// <summary>Gets the player's state.</summary>
    public Player Player { get; }

    /// <summary><Gets the game network.</summary>
    public Network Network { get; }

    /// <summary>Gets the player's botnet.</summary>
    public Botnet Botnet { get; }

    /// <summary>Gets the virtual filesystem containing files stored on game servers.</summary>
    public VirtualFileSystem FileSystem { get; }
}