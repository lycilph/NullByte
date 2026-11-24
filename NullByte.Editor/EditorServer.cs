using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NullByte.Core.Domain;
using NullByte.Scripting.Compilation;
using NullByte.Scripting.LanguageService;

namespace NullByte.Editor;

public sealed record EditorServerOptions(
    int Port = EditorServerOptions.DefaultPort,
    bool FallbackToFreePort = false,
    string? ConnectionFilePath = null)
{
    public const int DefaultPort = 8765;
}

public sealed class EditorServer(
    Func<EditorBackend?> backendProvider,
    IScriptCompiler compiler,
    ScriptLanguageService languageService,
    EditorServerOptions options) : IAsyncDisposable, IExternalEditorStatus
{
    private readonly ConcurrentDictionary<EditorConnection, byte> connections = new();
    private readonly EditorFileService files = new(backendProvider, compiler, languageService);
    private readonly EditorToken token = EditorToken.Create();
    private WebApplication? app;
    private GameState? watched;

    public Task WarmUp { get; private set; } = Task.CompletedTask;
    public int Port { get; private set; }
    public string Url => $"ws://127.0.0.1:{Port}/ws";
    public string ConnectionFilePath { get; } = options.ConnectionFilePath ?? EditorConnectionFile.DefaultPath;

    public async Task StartAsync()
    {
        try
        {
            await StartListeningAsync(options.Port);
        }
        catch (IOException) when (options.FallbackToFreePort)
        {
            await StartListeningAsync(0);   // the default port is taken: let the OS pick one
        }

        EditorConnectionFile.Write(ConnectionFilePath, Url, token.Value);

        WarmUp = Task.Run(async () =>
        {
            compiler.Compile("{}", "warmup");
            await languageService.WarmUpAsync();
        });
    }

    private async Task StartListeningAsync(int port)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();   // Kestrel logging would corrupt the terminal
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");

        var application = builder.Build();
        application.UseWebSockets();
        application.Map("/ws", async context =>
        {
            // Browsers always send Origin on websocket handshakes. The extension never does.
            if (context.Request.Headers.ContainsKey("Origin"))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            var header = context.Request.Headers.Authorization.ToString();
            if (!header.StartsWith("Bearer ", StringComparison.Ordinal) ||
                !token.Matches(header["Bearer ".Length..]))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            await HandleConnectionAsync(socket, context.RequestAborted);
        });

        try
        {
            await application.StartAsync();
        }
        catch
        {
            await application.DisposeAsync();   // a failed start leaves nothing behind
            throw;
        }

        app = application;
        Port = new Uri(application.Urls.First()).Port;   // the real port, also when 0 was requested
    }

    private async Task HandleConnectionAsync(WebSocket socket, CancellationToken ct)
    {
        var connection = new EditorConnection(socket);
        connections.TryAdd(connection, 0);
        var sendLoop = connection.RunSendLoopAsync(ct);

        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var message = await ReceiveTextAsync(socket, ct);
                if (message is null) break;
                _ = RespondAsync(connection, message);   // concurrent: a slow compile must not block file operations
            }
        }
        catch (Exception e) when (e is OperationCanceledException or WebSocketException) { }
        finally
        {
            connections.TryRemove(connection, out _);
            connection.Complete();
        }

        try
        {
            await sendLoop;   // flushes what is still queued
            if (socket.State == WebSocketState.CloseReceived)
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
        }
        catch (Exception e) when (e is OperationCanceledException or WebSocketException) { }
    }
    
    private async Task RespondAsync(EditorConnection connection, string message)
        => connection.Send(await files.HandleAsync(message));   // HandleAsync catches everything itself

    private static async Task<string?> ReceiveTextAsync(WebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[4096];
        using var stream = new MemoryStream();
        ValueWebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer.AsMemory(), ct);
            if (result.MessageType == WebSocketMessageType.Close) return null;
            stream.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
    }

    /// Call at startup and whenever the running game is replaced.
    public void OnGameChanged()
    {
        var state = backendProvider()?.State;
        if (ReferenceEquals(state, watched)) return;

        Unwatch();
        watched = state;
        if (state is not null)
        {
            state.FileSystem.Changed += OnFileChanged;
            state.Player.ServerDiscovered += OnServerDiscovered;
        }

        Broadcast(new { @event = "reset" });
    }

    private void Unwatch()
    {
        if (watched is null) return;
        watched.FileSystem.Changed -= OnFileChanged;
        watched.Player.ServerDiscovered -= OnServerDiscovered;
    }

    // Both handlers run on the game thread, so reading game state here is safe.
    private void OnFileChanged(FileChange change)
    {
        if (watched is not { } state || !state.Player.DiscoveredHostnames.Contains(change.Hostname))
            return;   // changes on hidden servers stay hidden

        Broadcast(new
        {
            @event = "fileChanged",
            type = change.Kind.ToString().ToLowerInvariant(),
            path = $"/{change.Hostname}/{change.Filename}",
            isDirectory = change.IsDirectory
        });
    }

    private void OnServerDiscovered(string hostname)
    {
        if (watched is not { } state) return;

        var name = state.Network.TryGetServer(hostname, out var server) ? server.Hostname : hostname;
        Broadcast(new { @event = "fileChanged", type = "created", path = $"/{name}" });
    }

    private void Broadcast(object message)
    {
        var json = JsonSerializer.Serialize(message);
        foreach (var connection in connections.Keys)
            connection.Send(json);
    }

    public ExternalEditorStatus GetStatus()
    {
        var connectionInfos = connections.Keys
            .Select(connection => new ExternalEditorConnectionInfo(connection.ConnectedAt))
            .OrderBy(info => info.ConnectedAt)
            .ToArray();

        return new ExternalEditorStatus(connectionInfos);
    }

    public async ValueTask DisposeAsync()
    {
        EditorConnectionFile.RemoveIfOwned(ConnectionFilePath, token.Value);

        Unwatch();

        languageService.Dispose();

        if (app is not null)
            await app.DisposeAsync();
    }
}