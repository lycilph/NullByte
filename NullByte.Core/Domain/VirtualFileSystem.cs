namespace NullByte.Core.Domain;

/// <summary>
/// Stores files belonging to the servers in a game network.
/// This filesystem is entirely in memory and never accesses the host filesystem.
/// </summary>
public sealed class VirtualFileSystem
{
    private readonly Network network;

    // The outer dictionary uses case-insensitive server IDs, matching the network's existing conventions.
    // File names are case-sensitive.
    private readonly Dictionary<string, Dictionary<string, string>> filesByServer =
        new(StringComparer.OrdinalIgnoreCase);

    public VirtualFileSystem(Network network)
    {
        this.network = network
            ?? throw new ArgumentNullException(nameof(network));
    }

    /// <summary>
    /// Gets the names of all files on a server, sorted alphabetically.
    /// </summary>
    public IReadOnlyList<string> ListFiles(string serverId)
    {
        ValidateServer(serverId);

        if (!filesByServer.TryGetValue(serverId, out var files))
            return Array.Empty<string>();

        return files.Keys
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Attempts to read a file from a server.
    /// </summary>
    public bool TryReadFile(string serverId, string fileName, out string? content)
    {
        ValidateServer(serverId);
        ValidateFileName(fileName);

        if (filesByServer.TryGetValue(serverId, out var files) && files.TryGetValue(fileName, out content))
            return true;

        content = null;
        return false;
    }

    /// <summary>
    /// Creates a new file or replaces the contents of an existing file.
    /// </summary>
    public void WriteFile(string serverId, string fileName, string content)
    {
        ValidateServer(serverId);
        ValidateFileName(fileName);
        ArgumentNullException.ThrowIfNull(content);

        if (!filesByServer.TryGetValue(serverId, out var files))
        {
            files = new Dictionary<string, string>(StringComparer.Ordinal);
            filesByServer.Add(serverId, files);
        }

        files[fileName] = content;
    }

    /// <summary>
    /// Deletes a file. Returns false if the file does not exist.
    /// </summary>
    public bool DeleteFile(string serverId, string fileName)
    {
        ValidateServer(serverId);
        ValidateFileName(fileName);

        return filesByServer.TryGetValue(serverId, out var files) && files.Remove(fileName);
    }

    private void ValidateServer(string serverId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);

        if (!network.TryGetServer(serverId, out _))
            throw new KeyNotFoundException($"Server '{serverId}' does not exist.");
    }

    private static void ValidateFileName(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if (fileName is "." or ".." ||
            fileName.Contains('/') ||
            fileName.Contains('\\') ||
            fileName.Any(char.IsControl))
        {
            throw new ArgumentException(
                "File names must be simple names without directory separators.",
                nameof(fileName));
        }
    }
}