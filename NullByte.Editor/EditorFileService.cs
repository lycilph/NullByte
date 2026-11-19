using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NullByte.Core.Domain;

namespace NullByte.Editor;

internal sealed class EditorFileService(Func<EditorBackend?> backendProvider)
{
    private static readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web);

    public async Task<string> HandleAsync(string message)
    {
        int? id = null;
        try
        {
            var request = JsonNode.Parse(message)?.AsObject()
                ?? throw new EditorException("BadRequest", "Expected a JSON object.");
            id = request["id"]?.GetValue<int>();
            var op = request["op"]?.GetValue<string>() ?? throw new EditorException("BadRequest", "Missing 'op'.");
            var path = request["path"]?.GetValue<string>() ?? throw new EditorException("BadRequest", "Missing 'path'.");

            var result = await ExecuteAsync(op, ParsePath(path));
            return JsonSerializer.Serialize(new { id, ok = true, result }, json);
        }
        catch (EditorException e) { return Error(id, e.Code, e.Message); }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException or ArgumentException)
        { return Error(id, "BadRequest", e.Message); }
        catch (Exception e) { return Error(id, "Internal", e.Message); }
    }

    private async Task<object> ExecuteAsync(string op, (string? Host, string? File) path)
    {
        var backend = backendProvider() ?? throw new EditorException("Unavailable", "No game is running.");

        return await backend.GameThread.InvokeAsync(() => Task.FromResult<object>(op switch
        {
            "stat" => Stat(backend.State, path),
            "readDirectory" => ReadDirectory(backend.State, path),
            "readFile" => new FileResult(ReadContent(backend.State, path)),
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
}