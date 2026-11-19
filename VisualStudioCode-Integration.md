Access:
wscat -c ws://127.0.0.1:8765/ws -H "Authorization: Bearer <contents of the token file>"


Where the PoC stands

You now have the full loop. VS Code browses and edits the game’s servers and files over an authenticated websocket, using one FileSystemProvider. Console changes appear in VS Code immediately, and run executes whatever you saved. Everything that touches game state runs on the game thread, and there is a debug log to see what the extension is doing.

Known gaps, in roughly the order I’d tackle them if you want to expand:

IntelliSense for the player API: a generated stub project or reference assembly so VS Code’s C# tooling knows the nb API.

Real modification times and conflict detection: mtime is still a constant 0, so VS Code can’t warn when a file changed underneath you.
Subfolders: this needs a change to VirtualFileSystem.
Compile errors in the Problems panel: a compile or diagnostics operation on top of the Roslyn scripting layer, with the extension publishing the results as VS Code diagnostics.
Run from VS Code: a command, or a run operation that executes the current file.
Packaging and configuration: a .vsix build, and a configurable port. If you ever use VS Code remotely, the extension would read the token from the wrong machine.

~~Hiding undiscovered servers: so the explorer doesn’t spoil the network map.~~