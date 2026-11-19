using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace NullByte.Editor;

public sealed class EditorServer(Func<EditorBackend?> backendProvider, int port = 8765) : IAsyncDisposable
{
    private readonly EditorFileService files = new(backendProvider);
    private WebApplication? app;

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
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            await HandleConnectionAsync(socket, context.RequestAborted);
        });

        await app.StartAsync();
    }

    private async Task HandleConnectionAsync(WebSocket socket, CancellationToken ct)
    {
        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var message = await ReceiveTextAsync(socket, ct);
                if (message is null) break;

                var response = await files.HandleAsync(message);
                await socket.SendAsync(Encoding.UTF8.GetBytes(response), WebSocketMessageType.Text, endOfMessage: true, ct);
            }

            if (socket.State == WebSocketState.CloseReceived)
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
        }
        catch (Exception e) when (e is OperationCanceledException or WebSocketException) { }
    }

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

    public async ValueTask DisposeAsync()
    {
        if (app is not null)
            await app.DisposeAsync();
    }
}