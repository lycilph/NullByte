Yes, this is feasible, and it fits NullByte especially well because you already have a virtual filesystem and C# script compilation.

My recommendation is:

Build a small VS Code extension that exposes the game’s virtual files through a custom FileSystemProvider, backed by a reliable localhost WebSocket connection to NullByte.

That would let the player open a workspace such as:

nullbyte://localhost/home/


and edit in-game files in normal VS Code tabs. VS Code’s FileSystemProvider API is specifically intended for custom storage backends and virtual workspaces, including files that live on a server rather than the local disk.

Recommended architecture
┌─────────────────────────────────────┐
│ VS Code                             │
│                                     │
│ NullByte extension                  │
│  ├─ FileSystemProvider              │
│  ├─ WebSocket client                │
│  ├─ Connection status               │
│  └─ Connect/disconnect commands     │
└──────────────────┬──────────────────┘
                   │
                   │ WebSocket over localhost
                   │ JSON request/response messages
                   │
┌──────────────────▼──────────────────┐
│ NullByte host                       │
│                                     │
│ Editor bridge                       │
│  ├─ WebSocket server                │
│  ├─ Authentication token            │
│  ├─ Path validation                 │
│  └─ Request dispatcher              │
│             │                       │
│             ▼                       │
│ GameThread.InvokeAsync              │
│             │                       │
│             ▼                       │
│ VirtualFileSystem                   │
└─────────────────────────────────────┘


The extension should not hold a second authoritative copy of the files. NullByte remains the source of truth.

Why WebSockets rather than UDP

Do not use UDP for file synchronization.

UDP has no built-in guarantees for:

Delivery
Ordering
Duplicate suppression
Message size
Connection state
Request/response correlation

Losing one file-write packet could silently leave VS Code and the game with different script contents.

A localhost WebSocket gives you:

Reliable ordered transport over TCP
Bidirectional messages
Easy JSON messages
Connection/disconnection detection
Browser and Node.js ecosystem support
One long-lived connection
Server-pushed file-change notifications

The alternatives are:

Transport	AssessmentWebSocket	Best fit for an extension and bidirectional events
Named pipes	Excellent locally, but more platform-specific and awkward from TypeScript
TCP socket	Viable, but you must implement message framing yourself
HTTP plus polling	Easy prototype, but weaker change notification behavior
HTTP plus Server-Sent Events	Good, but split request and notification mechanisms
UDP	Wrong fit for durable file operations
Shared host directory	Simplest MVP, but the game loses direct ownership of the filesystem

For your Windows-first hobby project, named pipes would technically work. WebSockets are still preferable because the extension side is simpler and the protocol remains portable.

Two possible integration models
Model A: Mirror files to a real host directory

The game writes its scripts to a directory such as:

%LOCALAPPDATA%\NullByte\Workspace\home\


VS Code opens that normal directory. A watcher sends edits back into the game.

Advantages
Very fast to prototype.
All ordinary VS Code extensions work.
C# tooling can see normal files.
No custom VS Code filesystem implementation required initially.
Users can use search, source control, formatting, and standard filesystem tools.
Disadvantages
You now have two sources of truth.
External file changes must be reconciled.
Saving/loading the game must synchronize the mirror.
Players could edit files while the game is not running.
Host files can drift from saved game files.
It weakens the game fiction of an isolated in-game network.
Model B: VS Code virtual workspace

The extension implements vscode.FileSystemProvider and maps operations directly to the game. Microsoft’s sample demonstrates a provider implementing file/directory metadata and CRUD operations, with a custom URI scheme registered through workspace.registerFileSystemProvider.

Advantages
The game remains the authoritative filesystem.
No host-file synchronization.
Paths naturally map to in-game servers.
Saving/loading continues to work normally.
The extension can receive push notifications.
Stronger game identity.
Disadvantages
More extension work.
Some third-party extensions do not support virtual workspaces because they assume resources are ordinary disk files. VS Code can deactivate or limit such extensions in a virtual workspace.
External compilers that require a physical path cannot read the virtual files.
Roslyn diagnostics need either local extension logic or an explicit diagnostics endpoint.

For NullByte, I would choose Model B, but begin with a narrowly scoped proof of concept.

Protocol design

Use JSON request/response envelopes with unique IDs.

Request
{
  "protocolVersion": 1,
  "id": "78c1a87d-b95c-477e-8f19-b343ad867db8",
  "method": "file/read",
  "params": {
    "hostname": "home",
    "path": "/scripts/money.cs"
  }
}

Successful response
{
  "protocolVersion": 1,
  "id": "78c1a87d-b95c-477e-8f19-b343ad867db8",
  "ok": true,
  "result": {
    "content": "while (true) { await NB.Hack(\"n00dles\"); }",
    "revision": 17,
    "modifiedAtUtc": "2026-11-18T06:45:00Z"
  }
}

Error response
{
  "protocolVersion": 1,
  "id": "78c1a87d-b95c-477e-8f19-b343ad867db8",
  "ok": false,
  "error": {
    "code": "file-not-found",
    "message": "File '/scripts/money.cs' does not exist on 'home'."
  }
}

Server-pushed event
{
  "protocolVersion": 1,
  "event": "file/changed",
  "params": {
    "hostname": "home",
    "path": "/scripts/money.cs",
    "revision": 18,
    "change": "updated"
  }
}


Start with these methods:

session/hello
server/list
directory/read
file/stat
file/read
file/write
file/delete
file/rename
directory/create
watch/subscribe
watch/unsubscribe


Eventually add:

script/compile
script/run
script/kill
process/list
diagnostics/publish

Revision-based write protection

Every file should have a revision number.

When VS Code reads a file:

{
  "content": "...",
  "revision": 17
}


When it saves:

{
  "method": "file/write",
  "params": {
    "hostname": "home",
    "path": "/scripts/money.cs",
    "content": "...",
    "expectedRevision": 17
  }
}


If the file changed inside the game after VS Code read it, reject the write:

{
  "ok": false,
  "error": {
    "code": "revision-conflict",
    "message": "Expected revision 17, but the current revision is 18."
  }
}


Do not silently choose “last writer wins.” That risks overwriting edits made through the in-game edit command or a loaded save.

This means VirtualFileSystem needs richer metadata than its current dictionary of path to content.

A suitable internal model would be:

public sealed record VirtualFile(
    string Content,
    long Revision,
    DateTimeOffset ModifiedAtUtc);


Or keep metadata separately if changing the current representation would create too much M7 churn.

Game-side bridge

Add a separate project:

NullByte.EditorBridge/
  EditorBridgeServer.cs
  EditorBridgeOptions.cs
  EditorBridgeProtocol.cs
  EditorBridgeRequestHandler.cs
  EditorBridgeSession.cs


Project references:

NullByte.ConsoleUI
    ├─ NullByte.Shell
    └─ NullByte.EditorBridge

NullByte.EditorBridge
    ├─ NullByte.Shell
    └─ NullByte.Core


Prefer accessing the game through a narrow facade rather than giving the bridge unrestricted access to GameSession.

public interface IEditorBridgeGame
{
    Task<IReadOnlyList<string>> ListServersAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<VirtualDirectoryEntry>> ReadDirectoryAsync(
        string hostname,
        string path,
        CancellationToken cancellationToken);

    Task<VirtualFileSnapshot> ReadFileAsync(
        string hostname,
        string path,
        CancellationToken cancellationToken);

    Task<WriteVirtualFileResult> WriteFileAsync(
        string hostname,
        string path,
        string content,
        long? expectedRevision,
        CancellationToken cancellationToken);

    Task DeleteFileAsync(
        string hostname,
        string path,
        CancellationToken cancellationToken);
}


Implementation:

public sealed class EditorBridgeGame : IEditorBridgeGame
{
    private readonly Func<GameSession> getSession;

    public EditorBridgeGame(Func<GameSession> getSession)
    {
        ArgumentNullException.ThrowIfNull(getSession);
        this.getSession = getSession;
    }

    public Task<VirtualFileSnapshot> ReadFileAsync(
        string hostname,
        string path,
        CancellationToken cancellationToken)
    {
        var session = getSession();

        return session.GameThread.InvokeAsync(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var content = session.State.FileSystem.ReadFile(
                    hostname,
                    path);

                var result = new VirtualFileSnapshot(
                    hostname,
                    path,
                    content,
                    Revision: 0);

                return Task.FromResult(result);
            });
    }

    public Task<WriteVirtualFileResult> WriteFileAsync(
        string hostname,
        string path,
        string content,
        long? expectedRevision,
        CancellationToken cancellationToken)
    {
        var session = getSession();

        return session.GameThread.InvokeAsync(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                session.State.FileSystem.WriteFile(
                    hostname,
                    path,
                    content);

                return Task.FromResult(
                    new WriteVirtualFileResult(
                        Revision: 0));
            });
    }

    // Remaining operations...
}


Using Func<GameSession> is significant. The editor bridge must always resolve sessionHost.Session; it cannot retain the original session because M7 loading replaces that object.

Wire it as:

var editorBridgeGame = new EditorBridgeGame(
    () => sessionHost.Session);

Use ASP.NET Core for the socket server

Although NullByte is a console application, the most straightforward C# implementation is an ASP.NET Core WebSocket endpoint bound only to loopback.

Add a framework reference:

<ItemGroup>
  <FrameworkReference Include="Microsoft.AspNetCore.App" />
</ItemGroup>


Minimal host setup:

var builder = WebApplication.CreateSlimBuilder();

builder.WebHost.UseUrls("http://127.0.0.1:43821");

var app = builder.Build();

app.UseWebSockets();

app.Map(
    "/editor",
    async context =>
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode =
                StatusCodes.Status400BadRequest;

            return;
        }

        using var socket =
            await context.WebSockets.AcceptWebSocketAsync();

        await editorBridgeServer.HandleConnectionAsync(
            socket,
            context.RequestAborted);
    });

await app.StartAsync(cancellationToken);


Use an ephemeral or configurable port later. For the proof of concept, a fixed port such as 43821 is fine.

Security boundary

Even on localhost, treat the bridge as an authenticated service.

At game startup:

Generate 32 random bytes.
Encode them with Base64Url or hexadecimal.
Print the connection token.
Require the extension to send it in session/hello.
Reject all other operations before authentication.
var token = Convert.ToHexString(
    RandomNumberGenerator.GetBytes(32));


Bind only to:

127.0.0.1


and optionally IPv6 loopback:

[::1]


Do not bind to 0.0.0.0.

Also validate every virtual path within the game. The client must not be able to use paths such as:

../../AppData/...


In principle, the virtual filesystem cannot reach host files anyway, but canonical path validation should still be part of the protocol boundary.

VS Code extension structure

Create an extension using Microsoft’s extension generator:

npm install --global yo generator-code
yo code


Choose:

New Extension (TypeScript)


Suggested structure:

nullbyte-vscode/
  package.json
  src/
    extension.ts
    nullByteClient.ts
    nullByteFileSystemProvider.ts
    protocol.ts
    connectionManager.ts

package.json
{
  "name": "nullbyte",
  "displayName": "NullByte",
  "description": "Edit NullByte virtual files from VS Code.",
  "version": "0.1.0",
  "engines": {
    "vscode": "^1.95.0"
  },
  "activationEvents": [
    "onFileSystem:nullbyte",
    "onCommand:nullbyte.connect"
  ],
  "main": "./out/extension.js",
  "contributes": {
    "commands": [
      {
        "command": "nullbyte.connect",
        "title": "NullByte: Connect"
      },
      {
        "command": "nullbyte.disconnect",
        "title": "NullByte: Disconnect"
      },
      {
        "command": "nullbyte.openWorkspace",
        "title": "NullByte: Open Game Workspace"
      }
    ],
    "configuration": {
      "title": "NullByte",
      "properties": {
        "nullbyte.port": {
          "type": "number",
          "default": 43821,
          "minimum": 1,
          "maximum": 65535,
          "description": "Local NullByte editor bridge port."
        }
      }
    }
  },
  "capabilities": {
    "virtualWorkspaces": true
  }
}

Register the provider
import * as vscode from "vscode";
import { NullByteClient } from "./nullByteClient";
import { NullByteFileSystemProvider } from
    "./nullByteFileSystemProvider";

export function activate(
    context: vscode.ExtensionContext
): void {
    const client = new NullByteClient();
    const provider =
        new NullByteFileSystemProvider(client);

    context.subscriptions.push(
        vscode.workspace.registerFileSystemProvider(
            "nullbyte",
            provider,
            {
                isCaseSensitive: true,
                isReadonly: false
            }
        )
    );

    context.subscriptions.push(
        vscode.commands.registerCommand(
            "nullbyte.connect",
            async () => {
                const configuration =
                    vscode.workspace.getConfiguration(
                        "nullbyte"
                    );

                const port =
                    configuration.get<number>(
                        "port",
                        43821
                    );

                const token =
                    await vscode.window.showInputBox({
                        prompt:
                            "Enter the token shown by NullByte",
                        password: true,
                        ignoreFocusOut: true
                    });

                if (!token)
                    return;

                await client.connect(port, token);

                vscode.window.showInformationMessage(
                    "Connected to NullByte."
                );
            }
        )
    );

    context.subscriptions.push(
        vscode.commands.registerCommand(
            "nullbyte.openWorkspace",
            async () => {
                if (!client.isConnected)
                    await vscode.commands.executeCommand(
                        "nullbyte.connect"
                    );

                if (!client.isConnected)
                    return;

                const workspaceUri = vscode.Uri.parse(
                    "nullbyte://game/"
                );

                await vscode.commands.executeCommand(
                    "vscode.openFolder",
                    workspaceUri,
                    {
                        forceNewWindow: false
                    }
                );
            }
        )
    );
}

export function deactivate(): void {
}


The VS Code API supports registering commands and invoking them through the command palette or UI contributions.

FileSystemProvider mapping

Your provider needs these operations:

export class NullByteFileSystemProvider
    implements vscode.FileSystemProvider {

    private readonly changedEmitter =
        new vscode.EventEmitter<
            vscode.FileChangeEvent[]
        >();

    readonly onDidChangeFile =
        this.changedEmitter.event;

    constructor(
        private readonly client: NullByteClient
    ) {
        client.onFileChanged(change => {
            this.changedEmitter.fire([
                {
                    type: mapChangeType(change.change),
                    uri: toUri(
                        change.hostname,
                        change.path
                    )
                }
            ]);
        });
    }

    watch(
        uri: vscode.Uri,
        options: {
            readonly recursive: boolean;
            readonly excludes: readonly string[];
        }
    ): vscode.Disposable {
        return this.client.watch(
            fromUri(uri),
            options.recursive
        );
    }

    async stat(
        uri: vscode.Uri
    ): Promise<vscode.FileStat> {
        const result =
            await this.client.stat(fromUri(uri));

        return {
            type:
                result.kind === "directory"
                    ? vscode.FileType.Directory
                    : vscode.FileType.File,
            ctime: result.createdAt,
            mtime: result.modifiedAt,
            size: result.size
        };
    }

    async readDirectory(
        uri: vscode.Uri
    ): Promise<
        [string, vscode.FileType][]
    > {
        const entries =
            await this.client.readDirectory(
                fromUri(uri)
            );

        return entries.map(entry => [
            entry.name,
            entry.kind === "directory"
                ? vscode.FileType.Directory
                : vscode.FileType.File
        ]);
    }

    async readFile(
        uri: vscode.Uri
    ): Promise<Uint8Array> {
        const result =
            await this.client.readFile(
                fromUri(uri)
            );

        return new TextEncoder().encode(
            result.content
        );
    }

    async writeFile(
        uri: vscode.Uri,
        content: Uint8Array,
        options: {
            readonly create: boolean;
            readonly overwrite: boolean;
        }
    ): Promise<void> {
        await this.client.writeFile({
            ...fromUri(uri),
            content:
                new TextDecoder().decode(content),
            create: options.create,
            overwrite: options.overwrite
        });
    }

    async delete(
        uri: vscode.Uri,
        options: {
            readonly recursive: boolean;
        }
    ): Promise<void> {
        await this.client.delete(
            fromUri(uri),
            options.recursive
        );
    }

    async rename(
        oldUri: vscode.Uri,
        newUri: vscode.Uri,
        options: {
            readonly overwrite: boolean;
        }
    ): Promise<void> {
        await this.client.rename(
            fromUri(oldUri),
            fromUri(newUri),
            options.overwrite
        );
    }

    async createDirectory(
        uri: vscode.Uri
    ): Promise<void> {
        await this.client.createDirectory(
            fromUri(uri)
        );
    }
}


This mirrors the responsibilities of Microsoft’s FileSystemProvider sample: metadata, directory enumeration, reads, writes, renames, deletes, directory creation, watches, and file-change events.

Workspace URI design

Use the URI authority for the game connection and the first path segment for the in-game hostname:

nullbyte://game/home/scripts/money.cs
nullbyte://game/n00dles/scripts/worker.cs
nullbyte://game/foodnstuff/notes.txt


Mapping:

URI scheme     nullbyte
URI authority  game
Path segment 1 server hostname
Remaining path virtual file path


For example:

function fromUri(
    uri: vscode.Uri
): {
    hostname: string;
    path: string;
} {
    const segments = uri.path
        .split("/")
        .filter(Boolean);

    if (segments.length === 0) {
        return {
            hostname: "",
            path: "/"
        };
    }

    const [hostname, ...pathSegments] =
        segments;

    return {
        hostname,
        path:
            "/" + pathSegments.join("/")
    };
}


At the workspace root, readDirectory returns the currently accessible in-game servers. Under each server, it lists that server’s files.

You must decide whether “accessible” means:

All discovered servers
Servers with root access
Only the player’s current server
Home plus rooted servers

I would expose home plus servers with root access. That aligns external editing with actual access rights.

C# language support

There is a significant limitation: a VS Code virtual workspace does not automatically make the NullByte API understood by the standard C# extension.

Your scripts appear to use top-level C# and a custom NB API. The standard C# language server will not necessarily understand:

The game-provided script prelude
Implicit imports
NB
The virtual project structure
Your custom compilation references

There are three levels of improvement.

Level 1: Basic text editing

Associate in-game script files with C#:

await vscode.languages.setTextDocumentLanguage(
    document,
    "csharp"
);


You get syntax highlighting and basic editing, but diagnostics may be incomplete or incorrect.

Level 2: Ask the game to compile on save

After file/write, call:

script/compile


The game returns Roslyn diagnostics:

{
  "diagnostics": [
    {
      "path": "/scripts/money.cs",
      "severity": "error",
      "code": "CS0103",
      "message": "The name 'TimeSpan' does not exist in the current context.",
      "startLine": 3,
      "startColumn": 15,
      "endLine": 3,
      "endColumn": 23
    }
  ]
}


The extension turns those into a DiagnosticCollection:

const diagnostics =
    vscode.languages.createDiagnosticCollection(
        "nullbyte"
    );

diagnostics.set(
    document.uri,
    returnedDiagnostics.map(
        diagnostic =>
            new vscode.Diagnostic(
                new vscode.Range(
                    diagnostic.startLine,
                    diagnostic.startColumn,
                    diagnostic.endLine,
                    diagnostic.endColumn
                ),
                diagnostic.message,
                mapSeverity(
                    diagnostic.severity
                )
            )
    )
);


This is the best next feature after basic editing because it reuses your exact Roslyn compiler configuration.

Level 3: Custom language server

Build a Language Server Protocol server using Roslyn and teach it about:

NullByte’s references
Global usings
NB API surface
Completion for server names and script paths
Hover documentation
Signature help
Diagnostics

This would be powerful, but it is much larger than the editing bridge. Do not start here.

Integration with save/load

M7 creates two events the editor bridge must understand:

session/replacing
session/replaced


On load:

Broadcast session/replacing.
Reject or pause writes.
Replace the session through SessionHost.
Restart saved processes.
Broadcast session/replaced.
Send filesystem change events for the complete workspace.
The VS Code provider refreshes its tree and open editors.

The bridge must resolve the current session dynamically:

new EditorBridgeGame(
    () => sessionHost.Session)


Do not inject sessionHost.Session directly during startup, or the bridge will continue reading the old session after load.

Before replacing a session, VS Code may contain unsaved editor buffers. The extension should not silently overwrite those. Initially, warn the user:

NullByte loaded a save. Some open in-game files changed.
Review or revert unsaved editors before saving.


Revision checks then prevent accidental overwrites.

Suggested implementation milestones
E1: Read-only proof of concept

Time estimate: one or two focused evenings.

Game:

Start a loopback WebSocket server.
Authenticate one client.
Implement server/list.
Implement directory/read.
Implement file/read.

Extension:

Connect command.
FileSystemProvider.
stat.
readDirectory.
readFile.
Open nullbyte://game/.

Acceptance:

Browse servers and open a script in VS Code.
No writes yet.
E2: Editing

Game:

Implement file/write.
Implement revision numbers.
Validate access and paths.
Fire file-change events.

Extension:

Implement writeFile.
Handle server notifications.
Display conflicts.

Acceptance:

Saving in VS Code immediately changes the in-game script.
Editing in the game updates or invalidates the VS Code document.
E3: Full filesystem operations

Implement:

Create
Delete
Rename
Directories
Recursive watches

Acceptance:

Explorer operations work as expected.
E4: Roslyn diagnostics

Game:

Compile an unscheduled script.
Return structured diagnostics.

Extension:

Run validation after save.
Publish VS Code diagnostics.

Acceptance:

Compiler errors appear with correct lines and columns.
E5: Script control

Add VS Code commands:

NullByte: Run Current Script
NullByte: Kill Current Script
NullByte: Show Processes
NullByte: Connect to Server


Acceptance:

A script can be edited, saved, compiled, and started without returning to the game console.
Where this fits in the game roadmap

I would not combine this with M7’s core persistence work.

Finish M7 first through:

Save/load DTOs
Session replacement
Script restart
Autosave
Golden-file tests

Then add the editor bridge as either:

M7.5 External editor bridge


or a separate infrastructure milestone.

It touches M7 because save/load replaces the session, but it should remain its own subsystem.

Recommended first implementation

Start with this narrow vertical slice:

VS Code extension
  readFile("nullbyte://game/home/test.cs")
       ↓
WebSocket request
       ↓
EditorBridgeGame.ReadFileAsync
       ↓
sessionHost.Session.GameThread.InvokeAsync
       ↓
VirtualFileSystem.ReadFile
       ↓
WebSocket response
       ↓
VS Code editor tab


Once that works, add readDirectory, then writeFile.

That sequence tests the hardest architectural assumption early: whether a VS Code virtual filesystem backed by your game feels natural enough to justify the deeper integration.