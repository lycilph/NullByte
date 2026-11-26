namespace NullByte.Core.Domain;

public enum FileChangeKind { Created, Changed, Deleted }

/// <summary>
/// A change to a file or directory. <paramref name="Filename"/> is the path relative to the server root.
/// Deleting or moving a directory is reported once, for the directory itself.
/// </summary>
public readonly record struct FileChange(string Hostname, string Filename, FileChangeKind Kind, bool IsDirectory = false);

/// <summary>When a file was created and last modified. Wall-clock time, not game time.</summary>
public readonly record struct FileTimes(DateTimeOffset Created, DateTimeOffset Modified);

public enum FileSystemEntryKind { Directory, File }   // directories sort first

public readonly record struct FileSystemEntry(string Name, FileSystemEntryKind Kind);

/// <summary>
/// Stores files and directories belonging to the servers in a game network.
/// This filesystem is entirely in memory and never accesses the host filesystem.
/// Paths are described by <see cref="VirtualPath"/>.
/// </summary>
public sealed class VirtualFileSystem
{
    private sealed record FileEntry(string Content, DateTimeOffset Created, DateTimeOffset Modified);

    private sealed class ServerFiles
    {
        public Dictionary<string, FileEntry> Files { get; } = new(StringComparer.Ordinal);

        // Every existing directory, including those that only exist because something lives below them.
        // Never contains the root.
        public HashSet<string> Directories { get; } = new(StringComparer.Ordinal);
    }

    private readonly Network network;
    private readonly TimeProvider timeProvider;

    // Files are keyed by immutable server identity; hostnames are resolved at the API boundary.
    private readonly Dictionary<ServerId, ServerFiles> serversById = [];

    /// <summary>Raised on the game thread after a file or directory changed. Handlers must not throw.</summary>
    public event Action<FileChange>? Changed;

    public VirtualFileSystem(Network network, TimeProvider? timeProvider = null)
    {
        this.network = network ?? throw new ArgumentNullException(nameof(network));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    // ---- Listing ----

    /// <summary>Gets the paths of all files on a server, in any directory, sorted alphabetically.</summary>
    public IReadOnlyList<string> ListFiles(string hostname)
    {
        var serverId = ResolveServerId(hostname);

        return Find(serverId)?.Files.Keys.OrderBy(path => path, StringComparer.Ordinal).ToArray() ?? [];
    }

    /// <summary>Gets the paths of all directories on a server, sorted alphabetically.</summary>
    public IReadOnlyList<string> ListDirectories(string hostname)
    {
        var serverId = ResolveServerId(hostname);

        return Find(serverId)?.Directories.OrderBy(path => path, StringComparer.Ordinal).ToArray() ?? [];
    }

    /// <summary>
    /// Lists the direct children of a directory (names, not paths), directories first.
    /// Returns false if the directory does not exist.
    /// </summary>
    public bool TryListDirectory(string hostname, string directory, out IReadOnlyList<FileSystemEntry> entries)
    {
        var serverId = ResolveServerId(hostname);
        directory = VirtualPath.NormalizeDirectory(directory);

        var server = Find(serverId);
        if (directory.Length > 0 && (server is null || !server.Directories.Contains(directory)))
        {
            entries = [];
            return false;
        }

        var children = new List<FileSystemEntry>();
        if (server is not null)
        {
            children.AddRange(server.Directories
                .Where(path => VirtualPath.Parent(path) == directory)
                .Select(path => new FileSystemEntry(VirtualPath.Name(path), FileSystemEntryKind.Directory)));

            children.AddRange(server.Files.Keys
                .Where(path => VirtualPath.Parent(path) == directory)
                .Select(path => new FileSystemEntry(VirtualPath.Name(path), FileSystemEntryKind.File)));
        }

        entries = children
            .OrderBy(entry => entry.Kind)
            .ThenBy(entry => entry.Name, StringComparer.Ordinal)
            .ToArray();
        return true;
    }

    public bool DirectoryExists(string hostname, string path)
    {
        var serverId = ResolveServerId(hostname);
        path = VirtualPath.NormalizeDirectory(path);

        return path.Length == 0 || (Find(serverId)?.Directories.Contains(path) ?? false);
    }

    // ---- Files ----

    /// <summary>Attempts to read a file from a server.</summary>
    public bool TryReadFile(string hostname, string filename, out string? content)
    {
        var serverId = ResolveServerId(hostname);
        var path = VirtualPath.NormalizeFile(filename);

        if (Find(serverId) is { } server && server.Files.TryGetValue(path, out var entry))
        {
            content = entry.Content;
            return true;
        }

        content = null;
        return false;
    }

    /// <summary>Attempts to get the creation and modification time of a file.</summary>
    public bool TryGetFileTimes(string hostname, string filename, out FileTimes times)
    {
        var serverId = ResolveServerId(hostname);
        var path = VirtualPath.NormalizeFile(filename);

        if (Find(serverId) is { } server && server.Files.TryGetValue(path, out var entry))
        {
            times = new FileTimes(entry.Created, entry.Modified);
            return true;
        }

        times = default;
        return false;
    }

    /// <summary>
    /// Creates a new file or replaces the contents of an existing file. Missing parent directories are created.
    /// Throws if the path is a directory, or if a file is in the way of the parent directories.
    /// </summary>
    public void WriteFile(string hostname, string filename, string content)
    {
        var serverId = ResolveServerId(hostname);
        var path = VirtualPath.NormalizeFile(filename);
        ArgumentNullException.ThrowIfNull(content);

        var server = GetOrCreate(serverId);
        if (server.Directories.Contains(path))
            throw new InvalidOperationException($"'{path}' is a directory.");

        EnsureParents(serverId, server, path);

        var now = Now();
        if (server.Files.TryGetValue(path, out var existing))
        {
            // Strictly increasing, so two writes in the same millisecond still look different to an editor.
            var modified = now > existing.Modified ? now : existing.Modified.AddMilliseconds(1);
            server.Files[path] = existing with { Content = content, Modified = modified };
            Raise(serverId, path, FileChangeKind.Changed);
        }
        else
        {
            server.Files[path] = new FileEntry(content, now, now);
            Raise(serverId, path, FileChangeKind.Created);
        }
    }

    /// <summary>Deletes a file. Returns false if the file does not exist.</summary>
    public bool DeleteFile(string hostname, string filename)
    {
        var serverId = ResolveServerId(hostname);
        var path = VirtualPath.NormalizeFile(filename);

        if (Find(serverId) is not { } server || !server.Files.Remove(path))
            return false;

        Raise(serverId, path, FileChangeKind.Deleted);
        return true;
    }

    /// <summary>
    /// Moves a file on a server, creating missing parent directories of the destination.
    ///
    /// Returns false when the source file does not exist, or the destination is in use
    /// (by a file or a directory) or lies below a file. In either case, nothing changes.
    /// </summary>
    public bool MoveFile(string hostname, string sourceFilename, string destinationFilename)
    {
        var serverId = ResolveServerId(hostname);
        var source = VirtualPath.NormalizeFile(sourceFilename);
        var destination = VirtualPath.NormalizeFile(destinationFilename);

        if (Find(serverId) is not { } server || !server.Files.TryGetValue(source, out var entry))
            return false;

        // Paths are case-sensitive, so an identical path is the same file.
        if (source == destination)
            return true;

        if (server.Files.ContainsKey(destination) || server.Directories.Contains(destination) ||
            HasFileAncestor(server, destination))
            return false;

        EnsureParents(serverId, server, destination);

        server.Files.Add(destination, entry);   // keeps its times: a rename is not a modification
        server.Files.Remove(source);

        Raise(serverId, source, FileChangeKind.Deleted);
        Raise(serverId, destination, FileChangeKind.Created);
        return true;
    }

    /// <summary>
    /// Copies files between servers, creating missing directories on the destination.
    /// </summary>
    /// <returns>True only if all files were copied</returns>
    public bool CopyFiles(string source, string destination, IReadOnlyList<string> files)
    {
        ValidateServer(source);
        ValidateServer(destination);

        var result = true;
        foreach (var file in files)
        {
            if (!TryReadFile(source, file, out var content) || content is null)
            {
                result = false;
                continue;
            }

            try
            {
                WriteFile(destination, file, content);
            }
            catch (InvalidOperationException)
            {
                result = false;   // for example a directory with that name already exists on the destination
            }
        }

        return result;
    }

    // ---- Directories ----

    /// <summary>
    /// Creates a directory and any missing parents. Returns false if it already exists.
    /// Throws if a file is in the way.
    /// </summary>
    public bool CreateDirectory(string hostname, string path)
    {
        var serverId = ResolveServerId(hostname);
        path = VirtualPath.NormalizeDirectory(path);

        if (path.Length == 0)
            return false;   // the root always exists

        var server = GetOrCreate(serverId);
        if (server.Files.ContainsKey(path))
            throw new InvalidOperationException($"'{path}' is a file.");

        if (server.Directories.Contains(path))
            return false;

        EnsureParents(serverId, server, path);
        AddDirectory(serverId, server, path);
        return true;
    }

    /// <summary>
    /// Deletes a directory. A directory with content is only deleted when <paramref name="recursive"/> is set.
    /// Returns false if the directory does not exist, or has content and <paramref name="recursive"/> is not set.
    /// </summary>
    public bool DeleteDirectory(string hostname, string path, bool recursive = false)
    {
        var serverId = ResolveServerId(hostname);
        path = VirtualPath.NormalizeDirectory(path);

        if (path.Length == 0)
            throw new ArgumentException("The root directory cannot be deleted.", nameof(path));

        if (Find(serverId) is not { } server || !server.Directories.Contains(path))
            return false;

        var prefix = path + "/";
        var files = server.Files.Keys.Where(f => f.StartsWith(prefix, StringComparison.Ordinal)).ToList();
        var directories = server.Directories.Where(d => d.StartsWith(prefix, StringComparison.Ordinal)).ToList();

        if (!recursive && (files.Count > 0 || directories.Count > 0))
            return false;

        foreach (var file in files)
            server.Files.Remove(file);
        foreach (var directory in directories)
            server.Directories.Remove(directory);
        server.Directories.Remove(path);

        Raise(serverId, path, FileChangeKind.Deleted, isDirectory: true);   // one event for the whole subtree
        return true;
    }

    /// <summary>
    /// Moves a directory with everything below it, creating missing parents of the destination.
    ///
    /// Returns false when the source does not exist, or the destination is in use, lies below a file,
    /// or is inside the source. Throws if a moved path would become too long or too deep.
    /// In every case, nothing changes unless true is returned.
    /// </summary>
    public bool MoveDirectory(string hostname, string sourcePath, string destinationPath)
    {
        var serverId = ResolveServerId(hostname);
        var source = VirtualPath.NormalizeDirectory(sourcePath);
        var destination = VirtualPath.NormalizeDirectory(destinationPath);

        if (source.Length == 0 || destination.Length == 0)
            throw new ArgumentException("The root directory cannot be moved or replaced.");

        if (Find(serverId) is not { } server || !server.Directories.Contains(source))
            return false;

        if (source == destination)
            return true;

        var prefix = source + "/";
        if (destination.StartsWith(prefix, StringComparison.Ordinal) ||
            server.Files.ContainsKey(destination) || server.Directories.Contains(destination) ||
            HasFileAncestor(server, destination))
            return false;

        string Moved(string path) => destination + path[source.Length..];

        var files = server.Files.Where(f => f.Key.StartsWith(prefix, StringComparison.Ordinal)).ToList();
        var directories = server.Directories.Where(d => d.StartsWith(prefix, StringComparison.Ordinal)).ToList();

        // Check the new paths before changing anything.
        foreach (var path in files.Select(f => f.Key).Concat(directories))
            VirtualPath.NormalizeDirectory(Moved(path));

        EnsureParents(serverId, server, destination);

        server.Directories.Remove(source);
        foreach (var directory in directories)
            server.Directories.Remove(directory);
        foreach (var file in files)
            server.Files.Remove(file.Key);

        server.Directories.Add(destination);
        foreach (var directory in directories)
            server.Directories.Add(Moved(directory));
        foreach (var file in files)
            server.Files.Add(Moved(file.Key), file.Value);   // keeps their times

        Raise(serverId, source, FileChangeKind.Deleted, isDirectory: true);
        Raise(serverId, destination, FileChangeKind.Created, isDirectory: true);
        return true;
    }

    // ---- Restoring from a save ----

    /// <summary>
    /// Restores a file from persistent state, including its original timestamps.
    ///
    /// Unlike <see cref="WriteFile"/>, this does not generate new timestamps or
    /// raise a file-change event. Missing parent directories are created, also silently.
    /// It is intended for save-game restoration.
    /// </summary>
    public void RestoreFile(
        string hostname,
        string filename,
        string content,
        DateTimeOffset created,
        DateTimeOffset modified)
    {
        var serverId = ResolveServerId(hostname);
        var path = VirtualPath.NormalizeFile(filename);
        ArgumentNullException.ThrowIfNull(content);

        if (modified < created)
            throw new ArgumentException("The modification time cannot be earlier than the creation time.", nameof(modified));

        var server = GetOrCreate(serverId);
        if (server.Directories.Contains(path))
            throw new InvalidOperationException($"'{path}' is a directory.");

        EnsureParents(serverId, server, path, raiseEvents: false);
        server.Files[path] = new FileEntry(content, created, modified);
    }

    /// <summary>
    /// Restores a directory (and any missing parents) from persistent state, without raising events.
    /// Throws if a file is in the way. It is intended for save-game restoration.
    /// </summary>
    public void RestoreDirectory(string hostname, string path)
    {
        var serverId = ResolveServerId(hostname);
        path = VirtualPath.NormalizeDirectory(path);

        if (path.Length == 0)
            return;

        var server = GetOrCreate(serverId);
        if (server.Files.ContainsKey(path))
            throw new InvalidOperationException($"'{path}' is a file.");

        EnsureParents(serverId, server, path, raiseEvents: false);
        server.Directories.Add(path);
    }

    // ---- Helpers ----

    private ServerFiles? Find(ServerId serverId) => serversById.GetValueOrDefault(serverId);

    private ServerFiles GetOrCreate(ServerId serverId)
    {
        if (!serversById.TryGetValue(serverId, out var server))
        {
            server = new ServerFiles();
            serversById.Add(serverId, server);
        }

        return server;
    }

    private static bool HasFileAncestor(ServerFiles server, string path)
        => VirtualPath.Ancestors(path).Any(server.Files.ContainsKey);

    /// <summary>Creates the missing directories above a path, outermost first. Throws if a file is in the way.</summary>
    private void EnsureParents(ServerId serverId, ServerFiles server, string path, bool raiseEvents = true)
    {
        var ancestors = VirtualPath.Ancestors(path).ToList();

        foreach (var ancestor in ancestors)
        {
            if (server.Files.ContainsKey(ancestor))
                throw new InvalidOperationException($"'{ancestor}' is a file.");
        }

        foreach (var ancestor in ancestors)
        {
            if (server.Directories.Add(ancestor) && raiseEvents)
                Raise(serverId, ancestor, FileChangeKind.Created, isDirectory: true);
        }
    }

    private void AddDirectory(ServerId serverId, ServerFiles server, string path)
    {
        if (server.Directories.Add(path))
            Raise(serverId, path, FileChangeKind.Created, isDirectory: true);
    }

    // Whole milliseconds, because that is what editors compare.
    private DateTimeOffset Now()
        => DateTimeOffset.FromUnixTimeMilliseconds(timeProvider.GetUtcNow().ToUnixTimeMilliseconds());

    private void Raise(ServerId serverId, string path, FileChangeKind kind, bool isDirectory = false)
        => Changed?.Invoke(new FileChange(network.GetServer(serverId).Hostname, path, kind, isDirectory));

    private ServerId ResolveServerId(string hostname)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostname);

        return network.TryGetServer(hostname, out var server)
            ? server.Id
            : throw new KeyNotFoundException($"Server '{hostname}' does not exist.");
    }
}