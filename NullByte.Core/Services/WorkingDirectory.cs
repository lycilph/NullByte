using NullByte.Core.Domain;

namespace NullByte.Core.Services;

/// <summary>The player's current directory on the current server, and resolving typed paths against it.</summary>
public sealed class WorkingDirectory(GameState gameState)
{
    /// <summary>The current directory ("" is the root).</summary>
    public string Current => gameState.Player.CurrentDirectory;

    /// <summary>Resolves a typed path that may be the root.</summary>
    public bool TryResolve(string typed, out string resolved, out string error)
        => PathResolver.TryResolve(Current, typed, out resolved, out error);

    /// <summary>Resolves a typed path that must name something below the root.</summary>
    public bool TryResolveFile(string typed, out string resolved, out string error)
        => PathResolver.TryResolveFile(Current, typed, out resolved, out error);

    /// <summary>
    /// Moves the player back to the root if the current directory no longer exists
    /// (a script or the editor may have deleted it). Returns true if that happened.
    /// </summary>
    public bool ResetIfMissing()
    {
        var player = gameState.Player;
        if (player.CurrentDirectory.Length == 0 ||
            gameState.FileSystem.DirectoryExists(gameState.Network.GetServer(player.CurrentServerId).Hostname, player.CurrentDirectory))
            return false;

        player.CurrentDirectory = "";
        return true;
    }

    /// <summary>Changes to a typed directory. The error echoes what was typed.</summary>
    public bool TryChange(string typed, out string error)
    {
        if (!TryResolve(typed, out var directory, out error))
            return false;

        var hostname = gameState.Player.CurrentHostname;
        if (!gameState.FileSystem.DirectoryExists(hostname, directory))
        {
            error = gameState.FileSystem.TryReadFile(hostname, directory, out _)
                ? $"{typed} is a file, not a directory."
                : $"Directory not found: {typed}";
            return false;
        }

        gameState.Player.CurrentDirectory = directory;
        error = "";
        return true;
    }
}