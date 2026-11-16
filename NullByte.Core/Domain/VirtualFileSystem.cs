namespace NullByte.Core.Domain;

/// <summary>
/// Stores files belonging to the servers in a game network.
/// This filesystem is entirely in memory and never accesses the host filesystem.
/// </summary>
public sealed class VirtualFileSystem
{
    private readonly Network network;

    // The outer dictionary uses case-insensitive server hostnamess, matching the network's existing conventions.
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
    public IReadOnlyList<string> ListFiles(string hostname)
    {
        ValidateServer(hostname);

        if (!filesByServer.TryGetValue(hostname, out var files))
            return Array.Empty<string>();

        return files.Keys
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Attempts to read a file from a server.
    /// </summary>
    public bool TryReadFile(string hostname, string filename, out string? content)
    {
        ValidateServer(hostname);
        ValidateFileName(filename);

        if (filesByServer.TryGetValue(hostname, out var files) && files.TryGetValue(filename, out content))
            return true;

        content = null;
        return false;
    }

    /// <summary>
    /// Creates a new file or replaces the contents of an existing file.
    /// </summary>
    public void WriteFile(string hostname, string filename, string content)
    {
        ValidateServer(hostname);
        ValidateFileName(filename);
        ArgumentNullException.ThrowIfNull(content);

        if (!filesByServer.TryGetValue(hostname, out var files))
        {
            files = new Dictionary<string, string>(StringComparer.Ordinal);
            filesByServer.Add(hostname, files);
        }

        files[filename] = content;
    }

    /// <summary>
    /// Deletes a file. Returns false if the file does not exist.
    /// </summary>
    public bool DeleteFile(string hostname, string filename)
    {
        ValidateServer(hostname);
        ValidateFileName(filename);

        return filesByServer.TryGetValue(hostname, out var files) && files.Remove(filename);
    }

    private void ValidateServer(string hostname)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostname);

        if (!network.TryGetServer(hostname, out _))
            throw new KeyNotFoundException($"Server '{hostname}' does not exist.");
    }

    private static void ValidateFileName(string filename)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filename);

        if (filename is "." or ".." ||
            filename.Contains('/') ||
            filename.Contains('\\') ||
            filename.Any(char.IsControl))
        {
            throw new ArgumentException(
                "File names must be simple names without directory separators.",
                nameof(filename));
        }
    }
}