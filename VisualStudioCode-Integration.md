I've read the design doc and the repo, and I'm checking whether there's an existing VS Code notes file before I write up the proposed architecture and my questions.

I've read the design doc and the repo. Here is the architecture I'd aim for, followed by the questions I need answered before writing code.

## Proposed target architecture

```
VS Code                                   NullByte (.NET process)
┌──────────────────────────┐              ┌─────────────────────────────────────────┐
│ Extension (TypeScript)   │              │ NullByte.Editor (new project)           │
│  FileSystemProvider      │   WebSocket  │  WebSocket server (Kestrel, localhost)  │
│  scheme: nullbyte://     │◄────────────►│  JSON-RPC-style protocol                │
│  nullbyte://home/foo.cs  │  JSON text   │  EditorFileService                      │
└──────────────────────────┘              │        │ GameThread.InvokeAsync(...)    │
                                          │        ▼                                │
                                          │  VirtualFileSystem (Core, unchanged)    │
                                          └─────────────────────────────────────────┘
```

- **Extension side:** a small TypeScript extension registers a `FileSystemProvider` for a `nullbyte:` scheme. Each server becomes a top-level folder, so `nullbyte:/home/hack.cs` is `hack.cs` on `home`. VS Code then gives you the file tree, editing, and save with no syncing.
- **Protocol:** request/response messages with an `id`, plus server-pushed events. The operations are `stat`, `readDirectory`, `readFile`, `writeFile`, `delete`, `rename`, `createDirectory`. Pushed events are `fileChanged`, so `edit` or `scp` in the console shows up in VS Code.
- **Game side:** a new `NullByte.Editor` project owns the socket server and translates messages into `VirtualFileSystem` calls. **Every call is marshalled through `GameThread.InvokeAsync`.** That keeps the single-threaded rule from section 4.2 of your doc: socket threads never touch game state.
- **Host wiring:** `GameHost` starts and stops the server alongside the session. Core and Scripting stay unchanged apart from one small addition, a change notification on `VirtualFileSystem`.
- **Out of scope for the PoC:** compile diagnostics in the Problems panel, running scripts from VS Code, IntelliSense for the `nb` API, and auth.

## Questions

1. **Directories:** `VirtualFileSystem` is flat, and file names can't contain `/`. For VS Code, should the root show one folder per server (my proposal), or only the current server's files? Subfolders inside a server would be a later change to the filesystem.
2. **Server stack:** Kestrel (`Microsoft.AspNetCore.App`) is the sturdiest choice, but it adds a framework reference. `HttpListener` plus `System.Net.WebSockets` is lighter and fine for localhost. Do you have a preference? Mine is Kestrel, since it's the standard and expands better.
3. **Extension tooling:** are you OK with a TypeScript extension (Node and npm required), run from VS Code with F5 in the Extension Development Host? There's no .NET alternative for writing a VS Code extension.
4. **Change events:** should the PoC include game→VS Code push events (edit in the console, see it in VS Code)? Or is the minimum "VS Code reads and writes files, then you `run` them in the console"? I'd include a simple version, since it's what makes `edit` and VS Code coexist safely.
5. **Port and security:** is a fixed default port (say 8765), bound to `127.0.0.1` only and without auth, acceptable for now? Any website you visit could try to open a localhost websocket, so I'd at least check the `Origin` header.
6. **Layout and exceptions:** should the extension live in the repo, for example `/vscode-extension/`? And does the "script crashes are caught" deliverable in M8 count as already done? I see `ProcessFailed` and `UnhandledException` handlers in `GameSession`, so I'd only verify it rather than rebuild it.

If you'd rather skip some of these, I'll go with my defaults: one folder per server, Kestrel, a TypeScript extension in the repo, push events included, `127.0.0.1:8765` with an Origin check, and a quick verification of the crash handling.

## Steps once you confirm

1. Add `NullByte.Editor` with a bare WebSocket echo server, started from `GameHost`.
2. Define the protocol and the file operations, marshalled onto the game thread.
3. Build the minimal extension with `stat`, `readDirectory`, `readFile` and `writeFile`.
4. End-to-end check: edit a script in VS Code and `run` it in the game.
5. Add change events, then rename and delete.

Good, that settles the architecture. Auth moves to the last step. Here is **Step 1: a bare websocket echo server, started from the console host.**

The goal is only to prove that Kestrel runs inside your console process and that a client can connect. There is no game logic yet.

### 1. New project `NullByte.Editor`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
</Project>
```

Add it to `NullByte.slnx` under `/src/`, and add a `ProjectReference` to it from `NullByte.ConsoleUI`.

### 2. `EditorServer.cs`

```csharp
using System.Net.WebSockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NullByte.Editor;

public sealed class EditorServer(int port = 8765) : IAsyncDisposable
{
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
            await EchoAsync(socket, context.RequestAborted);
        });

        await app.StartAsync();
    }

    private static async Task EchoAsync(WebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[4096];
        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                await socket.SendAsync(buffer.AsMemory(0, result.Count),
                    result.MessageType, result.EndOfMessage, ct);
            }

            if (socket.State == WebSocketState.CloseReceived)
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
        }
        catch (Exception e) when (e is OperationCanceledException or WebSocketException)
        {
            // Client vanished or the server is shutting down: nothing to do.
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (app is not null)
            await app.DisposeAsync();
    }
}
```

### 3. Start it from `Program.cs`

Inside the `try`, right after `host.StartAsync(...)`:

```csharp
await using var editor = new EditorServer();
await editor.StartAsync();
terminal.WriteLine($"Editor server listening on {editor.Url}");
```

I put it in `Program` rather than `GameHost` on purpose. `GameHost` replaces its `Game` on `load`, so in Step 2 the server will receive a *function* that returns the current session's filesystem and game thread, not a fixed reference.

### 4. Check it works

Start the game, then in another terminal:

```
npx wscat -c ws://127.0.0.1:8765/ws
> hello
< hello
```

Also check that closing the game with the client still connected exits cleanly, and that the terminal prompt isn't disturbed.

Tell me when the echo works, or paste any errors. **Step 2** is the protocol: JSON request/response messages, plus the `list` and `read` operations marshalled onto the game thread.