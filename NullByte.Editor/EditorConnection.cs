using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;

namespace NullByte.Editor;

/// <summary>
/// One connected external editor.
/// Everything sent to it goes through the outbox,
/// so only one SendAsync runs at a time.
/// </summary>
internal sealed class EditorConnection(WebSocket socket)
{
    private readonly Channel<string> outbox = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });

    public DateTimeOffset ConnectedAt { get; } = DateTimeOffset.UtcNow;

    public void Send(string json) => outbox.Writer.TryWrite(json);   // never throws, any thread
    public void Complete() => outbox.Writer.TryComplete();

    public async Task RunSendLoopAsync(CancellationToken ct)
    {
        await foreach (var json in outbox.Reader.ReadAllAsync(ct))
            await socket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, ct);
    }
}