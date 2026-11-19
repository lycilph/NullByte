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

namespace NullByte.Editor;

public sealed class EditorServer(Func<EditorBackend?> backendProvider, EditorToken token, IScriptCompiler compiler, int port = 8765) : IAsyncDisposable
{
    private readonly ConcurrentDictionary<EditorConnection, byte> connections = new();
    private readonly EditorFileService files = new(backendProvider, compiler);
    private WebApplication? app;
    private GameState? watched;

    public string Url => $"ws://127.0.0.1:{port}/ws";

    public async Task StartAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();   // Kestrel logging would corrupt the terminal
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");

        app = builder.Build();
        app.UseWebSockets();
        app.Map("/ws", async context =>
        {
            // Browsers always send Origin on websocket handshakes. The extension never does.
            if (context.Request.Headers.ContainsKey("Origin"))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            var header = context.Request.Headers.Authorization.ToString();
            if (!header.StartsWith("Bearer ", StringComparison.Ordinal) || !token.Matches(header["Bearer ".Length..]))
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

        await app.StartAsync();

        OnGameChanged();
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
            path = $"/{change.Hostname}/{change.Filename}"
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

    public async ValueTask DisposeAsync()
    {
        Unwatch();

        if (app is not null)
            await app.DisposeAsync();
    }
}