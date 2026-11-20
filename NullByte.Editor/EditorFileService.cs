using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NullByte.Core.Domain;
using NullByte.Scripting.Compilation;
using NullByte.Scripting.LanguageService;

using NodePath = (string? Host, string? File);

namespace NullByte.Editor;

internal sealed class EditorFileService(Func<EditorBackend?> backendProvider, IScriptCompiler compiler, ScriptLanguageService languageService)
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
            "delete" => Delete(backend.State, path),
            "rename" => Rename(backend.State, path, ParsePath(Required(request, "newPath")),
                               Flag(request, "overwrite")),
            _ => throw new EditorException("BadRequest", $"Unknown operation '{op}'.")
        }));
    }

    private DiagnoseResult Diagnose(string path, string content)
    {
        var fileName = ParsePath(path).File
            ?? throw new EditorException("BadRequest", "Expected the path of a file.");

        var result = compiler.Compile(content, fileName);
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
        if (p.Host is not null) RequireServer(state, p.Host);
        if (p.File is null)
            return new StatResult("directory", 0, 0, 0);

        var content = ReadContent(state, p);   // throws NotFound
        if (!state.FileSystem.TryGetFileTimes(p.Host!, p.File, out var times))
            throw new EditorException("NotFound", $"File '{p.File}' not found on '{p.Host}'.");

        return new StatResult("file", Encoding.UTF8.GetByteCount(content),
            times.Created.ToUnixTimeMilliseconds(), times.Modified.ToUnixTimeMilliseconds());
    }

    private static DirectoryResult ReadDirectory(GameState state, (string? Host, string? File) p)
    {
        if (p.File is not null)
            throw new EditorException("NotADirectory", "Not a directory.");

        if (p.Host is null)
            return new(state.Player.DiscoveredHostnames
                .Select(h => state.Network.TryGetServer(h, out var server) ? server.Hostname : null)
                .OfType<string>()
                .OrderBy(h => h, StringComparer.Ordinal)
                .Select(h => new DirectoryEntry(h, "directory"))
                .ToArray());

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
        if (!state.Network.TryGetServer(host, out _) || !state.Player.DiscoveredHostnames.Contains(host))
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