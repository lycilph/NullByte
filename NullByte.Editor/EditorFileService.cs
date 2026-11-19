using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NullByte.Core.Domain;

using NodePath = (string? Host, string? File);

namespace NullByte.Editor;

internal sealed class EditorFileService(Func<EditorBackend?> backendProvider)
{
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
        var backend = backendProvider() ?? throw new EditorException("Unavailable", "No game is running.");
        var path = ParsePath(Required(request, "path"));

        return await backend.GameThread.InvokeAsync(() => Task.FromResult<object>(op switch
        {
            "stat" => Stat(backend.State, path),
            "readDirectory" => ReadDirectory(backend.State, path),
            "readFile" => new FileResult(ReadContent(backend.State, path)),
            "writeFile" => WriteFile(backend.State, path, Required(request, "content"),
                                     Flag(request, "create"), Flag(request, "overwrite")),
            "delete" => Delete(backend.State, path),
            "rename" => Rename(backend.State, path, ParsePath(Required(request, "newPath")),
                               Flag(request, "overwrite")),
            _ => throw new EditorException("BadRequest", $"Unknown operation '{op}'.")
        }));
    }

    // Runs on the game thread from here down.

    private static StatResult Stat(GameState state, (string? Host, string? File) p)
    {
        if (p.Host is not null) RequireServer(state, p.Host);
        return p.File is null
            ? new StatResult("directory", 0)
            : new StatResult("file", Encoding.UTF8.GetByteCount(ReadContent(state, p)));
    }

    private static DirectoryResult ReadDirectory(GameState state, (string? Host, string? File) p)
    {
        if (p.File is not null)
            throw new EditorException("NotADirectory", "Not a directory.");

        if (p.Host is null)
            return new(state.Network.Servers
                .Select(s => new DirectoryEntry(s.Hostname, "directory"))
                .OrderBy(e => e.Name, StringComparer.Ordinal).ToArray());

        RequireServer(state, p.Host);
        return new(state.FileSystem.ListFiles(p.Host).Select(f => new DirectoryEntry(f, "file")).ToArray());
    }

    private static string ReadContent(GameState state, (string? Host, string? File) p)
    {
        if (p.Host is null || p.File is null)
            throw new EditorException("IsADirectory", "Is a directory.");

        RequireServer(state, p.Host);
        return state.FileSystem.TryReadFile(p.Host, p.File, out var content) && content is not null
            ? content
            : throw new EditorException("NotFound", $"File '{p.File}' not found on '{p.Host}'.");
    }

    private static WriteResult WriteFile(GameState state, NodePath p, string content, bool create, bool overwrite)
    {
        var (host, file) = RequireFile(state, p);
        var exists = state.FileSystem.TryReadFile(host, file, out _);

        if (!exists && !create)
            throw new EditorException("NotFound", $"File '{file}' not found on '{host}'.");
        if (exists && create && !overwrite)
            throw new EditorException("FileExists", $"File '{file}' already exists.");

        state.FileSystem.WriteFile(host, file, content);
        return new WriteResult(Created: !exists);
    }

    private static object Delete(GameState state, NodePath p)
    {
        var (host, file) = RequireFile(state, p);
        if (!state.FileSystem.DeleteFile(host, file))
            throw new EditorException("NotFound", $"File '{file}' not found on '{host}'.");
        return empty;
    }

    private static object Rename(GameState state, NodePath from, NodePath to, bool overwrite)
    {
        var (host, file) = RequireFile(state, from);
        var (newHost, newFile) = RequireFile(state, to);

        if (!string.Equals(host, newHost, StringComparison.OrdinalIgnoreCase))
            throw new EditorException("NoPermissions", "Files cannot be moved between servers.");
        if (!state.FileSystem.TryReadFile(host, file, out _))
            throw new EditorException("NotFound", $"File '{file}' not found on '{host}'.");

        if (file != newFile && state.FileSystem.TryReadFile(host, newFile, out _))
        {
            if (!overwrite)
                throw new EditorException("FileExists", $"File '{newFile}' already exists.");
            state.FileSystem.DeleteFile(host, newFile);
        }

        state.FileSystem.MoveFile(host, file, newFile);
        return empty;
    }

    private static (string Host, string File) RequireFile(GameState state, NodePath p)
    {
        if (p.Host is null || p.File is null)
            throw new EditorException("NoPermissions", "Only files can be created, changed or removed.");
        RequireServer(state, p.Host);
        return (p.Host, p.File);
    }

    private static void RequireServer(GameState state, string host)
    {
        if (!state.Network.TryGetServer(host, out _))
            throw new EditorException("NotFound", $"Server '{host}' does not exist.");
    }

    private static (string? Host, string? File) ParsePath(string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => (null, null),
            1 => (parts[0], null),
            2 => (parts[0], parts[1]),
            _ => throw new EditorException("NotFound", "Subfolders are not supported.")
        };
    }

    private static string Error(int? id, string code, string message)
        => JsonSerializer.Serialize(new { id, ok = false, error = new { code, message } }, json);

    private static string Required(JsonObject request, string name)
    => request[name]?.GetValue<string>() ?? throw new EditorException("BadRequest", $"Missing '{name}'.");

    private static bool Flag(JsonObject request, string name)
        => request[name]?.GetValue<bool>() ?? false;
}