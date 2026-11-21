using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NullByte.Core.Domain;
using NullByte.Scripting.Compilation;
using NullByte.Scripting.LanguageService;

namespace NullByte.Editor;

internal sealed class EditorFileService(Func<EditorBackend?> backendProvider, IScriptCompiler compiler, ScriptLanguageService languageService)
{
    /// <summary>A place in the game: the root (no host), a server (empty path) or something inside a server.</summary>
    private readonly record struct NodePath(string? Host, string Path);

    private static readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web);
    private static readonly object empty = new();

    public async Task<string> HandleAsync(string message)
    {
        int? id = null;
        try
        {
            var request = JsonNode.Parse(message)?.AsObject()
                ?? throw new EditorException("BadRequest", "Expected a JSON object.");
            id = request["id"]?.GetValue<int>();
            var op = request["op"]?.GetValue<string>() ?? throw new EditorException("BadRequest", "Missing 'op'.");

            var result = await ExecuteAsync(op, request);
            return JsonSerializer.Serialize(new { id, ok = true, result }, json);
        }
        catch (EditorException e) { return Error(id, e.Code, e.Message); }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException or ArgumentException)
        { return Error(id, "BadRequest", e.Message); }
        catch (Exception e) { return Error(id, "Internal", e.Message); }
    }

    private async Task<object> ExecuteAsync(string op, JsonObject request)
    {
        if (op == "diagnose")
            return await Task.Run(() => Diagnose(Required(request, "path"), Required(request, "content")));
        if (op == "complete")
            return await Task.Run(() => CompleteAsync(request));
        if (op == "hover")
            return await Task.Run(() => HoverAsync(request));
        if (op == "signatureHelp")
            return await Task.Run(() => SignatureHelpAsync(request));

        var backend = backendProvider();
        if (backend is not { GameThread.IsRunning: true })
            throw new EditorException("Unavailable", "No game is running.");

        var path = ParsePath(Required(request, "path"));

        return await backend.GameThread.InvokeAsync(() => Task.FromResult<object>(op switch
        {
            "stat" => Stat(backend.State, path),
            "readDirectory" => ReadDirectory(backend.State, path),
            "readFile" => new FileResult(ReadContent(backend.State, path)),
            "writeFile" => WriteFile(backend.State, path, Required(request, "content"),
                                     Flag(request, "create"), Flag(request, "overwrite")),
            "createDirectory" => CreateDirectory(backend.State, path),
            "delete" => Delete(backend.State, path, Flag(request, "recursive")),
            "rename" => Rename(backend.State, path, ParsePath(Required(request, "newPath")),
                               Flag(request, "overwrite")),
            _ => throw new EditorException("BadRequest", $"Unknown operation '{op}'.")
        }));
    }

    private DiagnoseResult Diagnose(string path, string content)
    {
        var node = ParsePath(path);
        if (node.Host is null || node.Path.Length == 0)
            throw new EditorException("BadRequest", "Expected the path of a file.");

        var result = compiler.Compile(content, node.Path);
        return new(result.Diagnostics
            .Select(d => new DiagnosticInfo(d.Id, d.Message, d.Severity.ToString().ToLowerInvariant(), d.Line, d.Column))
            .ToArray());
    }

    private static (string Content, int Offset) Source(JsonObject request)
    => (Required(request, "content"),
        request["offset"]?.GetValue<int>() ?? throw new EditorException("BadRequest", "Missing 'offset'."));

    private async Task<object> CompleteAsync(JsonObject request)
    {
        var (content, offset) = Source(request);
        return new CompleteResult(await languageService.GetCompletionsAsync(content, offset));
    }

    private async Task<object> HoverAsync(JsonObject request)
    {
        var (content, offset) = Source(request);
        return new HoverResult(await languageService.GetHoverAsync(content, offset));
    }

    private async Task<object> SignatureHelpAsync(JsonObject request)
    {
        var (content, offset) = Source(request);
        return new SignatureHelpResult(await languageService.GetSignatureHelpAsync(content, offset));
    }

    // Runs on the game thread from here down.

    private static StatResult Stat(GameState state, NodePath p)
    {
        if (p.Host is null)
            return Directory();

        RequireServer(state, p.Host);
        if (p.Path.Length == 0)
            return Directory();

        if (state.FileSystem.TryReadFile(p.Host, p.Path, out var content) && content is not null &&
            state.FileSystem.TryGetFileTimes(p.Host, p.Path, out var times))
        {
            return new StatResult("file", Encoding.UTF8.GetByteCount(content),
                times.Created.ToUnixTimeMilliseconds(), times.Modified.ToUnixTimeMilliseconds());
        }

        if (state.FileSystem.DirectoryExists(p.Host, p.Path))
            return Directory();

        throw NotFound(p);
    }

    private static StatResult Directory() => new("directory", 0, 0, 0);

    private static DirectoryResult ReadDirectory(GameState state, NodePath p)
    {
        if (p.Host is null)
            return new(state.Player.DiscoveredHostnames
                .Select(h => state.Network.TryGetServer(h, out var server) ? server.Hostname : null)
                .OfType<string>()
                .OrderBy(h => h, StringComparer.Ordinal)
                .Select(h => new DirectoryEntry(h, "directory"))
                .ToArray());

        RequireServer(state, p.Host);

        if (!state.FileSystem.TryListDirectory(p.Host, p.Path, out var entries))
        {
            // The server root always exists, so Path is not empty here.
            throw state.FileSystem.TryReadFile(p.Host, p.Path, out _)
                ? new EditorException("NotADirectory", $"'{p.Path}' is a file.")
                : NotFound(p);
        }

        return new(entries
            .Select(e => new DirectoryEntry(e.Name, e.Kind == FileSystemEntryKind.Directory ? "directory" : "file"))
            .ToArray());
    }

    private static string ReadContent(GameState state, NodePath p)
    {
        if (p.Host is null || p.Path.Length == 0)
            throw new EditorException("IsADirectory", "Is a directory.");

        RequireServer(state, p.Host);

        if (state.FileSystem.TryReadFile(p.Host, p.Path, out var content) && content is not null)
            return content;

        throw state.FileSystem.DirectoryExists(p.Host, p.Path)
            ? new EditorException("IsADirectory", $"'{p.Path}' is a directory.")
            : NotFound(p);
    }

    private static WriteResult WriteFile(GameState state, NodePath p, string content, bool create, bool overwrite)
    {
        var (host, path) = RequireInsideServer(state, p);
        var files = state.FileSystem;
        var exists = files.TryReadFile(host, path, out _);

        if (files.DirectoryExists(host, path))
            throw new EditorException("IsADirectory", $"'{path}' is a directory.");
        if (FileInTheWay(state, host, path) is { } blocker)
            throw new EditorException("NotADirectory", $"'{blocker}' is a file, not a directory.");

        if (!exists && !create)
            throw NotFound(p);
        if (exists && create && !overwrite)
            throw new EditorException("FileExists", $"File '{path}' already exists.");

        files.WriteFile(host, path, content);
        return new WriteResult(Created: !exists);
    }

    private static object CreateDirectory(GameState state, NodePath p)
    {
        var (host, path) = RequireInsideServer(state, p);
        var files = state.FileSystem;

        if (files.DirectoryExists(host, path) || files.TryReadFile(host, path, out _))
            throw new EditorException("FileExists", $"'{path}' already exists.");
        if (FileInTheWay(state, host, path) is { } blocker)
            throw new EditorException("NotADirectory", $"'{blocker}' is a file, not a directory.");

        files.CreateDirectory(host, path);
        return empty;
    }

    private static object Delete(GameState state, NodePath p, bool recursive)
    {
        var (host, path) = RequireInsideServer(state, p);
        var files = state.FileSystem;

        if (files.DeleteFile(host, path))
            return empty;

        if (!files.DirectoryExists(host, path))
            throw NotFound(p);

        if (!files.DeleteDirectory(host, path, recursive))
            throw new EditorException("NoPermissions", $"The directory '{path}' is not empty.");

        return empty;
    }

    private static object Rename(GameState state, NodePath from, NodePath to, bool overwrite)
    {
        var (host, source) = RequireInsideServer(state, from);
        var (newHost, destination) = RequireInsideServer(state, to);
        var files = state.FileSystem;

        if (!string.Equals(host, newHost, StringComparison.OrdinalIgnoreCase))
            throw new EditorException("NoPermissions", "Files and directories cannot be moved between servers.");

        var sourceIsFile = files.TryReadFile(host, source, out _);
        if (!sourceIsFile && !files.DirectoryExists(host, source))
            throw NotFound(from);

        if (source == destination)
            return empty;

        if (!sourceIsFile)
        {
            if (destination.StartsWith(source + "/", StringComparison.Ordinal))
                throw new EditorException("NoPermissions", "A directory cannot be moved into itself.");
            if (source.StartsWith(destination + "/", StringComparison.Ordinal))
                throw new EditorException("NoPermissions", "A directory cannot replace one of its parents.");
        }

        if (FileInTheWay(state, host, destination) is { } blocker)
            throw new EditorException("NotADirectory", $"'{blocker}' is a file, not a directory.");

        var destinationIsFile = files.TryReadFile(host, destination, out _);
        if (destinationIsFile || files.DirectoryExists(host, destination))
        {
            if (!overwrite)
                throw new EditorException("FileExists", $"'{destination}' already exists.");
            if (destinationIsFile != sourceIsFile)
                throw new EditorException("NoPermissions", "A file cannot replace a directory, or the other way around.");

            // Replace: the target goes first.
            if (destinationIsFile)
                files.DeleteFile(host, destination);
            else
                files.DeleteDirectory(host, destination, recursive: true);
        }

        var moved = sourceIsFile
            ? files.MoveFile(host, source, destination)
            : files.MoveDirectory(host, source, destination);

        if (!moved)
            throw new EditorException("NoPermissions", $"'{source}' could not be moved to '{destination}'.");

        return empty;
    }

    private static (string Host, string Path) RequireInsideServer(GameState state, NodePath p)
    {
        if (p.Host is null || p.Path.Length == 0)
            throw new EditorException("NoPermissions", "Only files and directories inside a server can be created, changed or removed.");

        RequireServer(state, p.Host);
        return (p.Host, p.Path);
    }

    private static void RequireServer(GameState state, string host)
    {
        if (!state.Network.TryGetServer(host, out _) || !state.Player.DiscoveredHostnames.Contains(host))
            throw new EditorException("NotFound", $"Server '{host}' does not exist.");
    }

    // The first directory above the path that is really a file.
    private static string? FileInTheWay(GameState state, string host, string path)
        => VirtualPath.Ancestors(path).FirstOrDefault(ancestor => state.FileSystem.TryReadFile(host, ancestor, out _));

    private static EditorException NotFound(NodePath p)
        => new("NotFound", $"'{p.Path}' not found on '{p.Host}'.");

    /// <summary>"/home/tools/a.cx" becomes server "home", path "tools/a.cx".</summary>
    private static NodePath ParsePath(string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return new NodePath(null, "");

        var inner = string.Join('/', parts.Skip(1));
        if (!VirtualPath.TryNormalizeDirectory(inner, out var normalized, out var error))
            throw new EditorException("NoPermissions", error);

        return new NodePath(parts[0], normalized);
    }

    private static string Error(int? id, string code, string message)
        => JsonSerializer.Serialize(new { id, ok = false, error = new { code, message } }, json);

    private static string Required(JsonObject request, string name)
    => request[name]?.GetValue<string>() ?? throw new EditorException("BadRequest", $"Missing '{name}'.");

    private static bool Flag(JsonObject request, string name)
        => request[name]?.GetValue<bool>() ?? false;
}