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
    ///  Copies files from source to destination servers
    /// </summary>
    /// <returns>True only if all files were copied</returns>
    public bool CopyFiles(string source, string destination, IReadOnlyList<string> files)
    {
        ValidateServer(source);
        ValidateServer(destination);

        var result = true;
        foreach (var file in files)
        {
            if (TryReadFile(source, file, out string? content) && content != null)
                WriteFile(destination, file, content);
            else
                result = false;
        }

        return result;
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

    /// <summary>
    /// Moves a file on a server.
    ///
    /// Returns false when the source file does not exist or the target filename
    /// is already in use. In either case, the filesystem is left unchanged.
    /// </summary>
    public bool MoveFile(string hostname, string sourceFilename, string destinationFilename)
    {
        ValidateServer(hostname);
        ValidateFileName(sourceFilename);
        ValidateFileName(destinationFilename);

        if (!filesByServer.TryGetValue(hostname, out var files))
            return false;

        if (!files.TryGetValue(sourceFilename, out var content))
            return false;

        // File names are case-sensitive, so an identical name is the same file.
        if (string.Equals(sourceFilename, destinationFilename, StringComparison.Ordinal))
            return true;

        // Do not silently overwrite another file.
        if (files.ContainsKey(destinationFilename))
            return false;

        files.Add(destinationFilename, content);
        files.Remove(sourceFilename);

        return true;
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