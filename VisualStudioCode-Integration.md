Access:
wscat -c ws://127.0.0.1:8765/ws -H "Authorization: Bearer <contents of the token file>"


Where the PoC stands
Known gaps, in roughly the order I’d tackle them if you want to expand:


Real modification times and conflict detection: mtime is still a constant 0, so VS Code can’t warn when a file changed underneath you.

full-range underlines for diagnostics,

Subfolders: this needs a change to VirtualFileSystem.

Run from VS Code: a command, or a run operation that executes the current file.



~~Packaging and configuration: a .vsix build, and a configurable port. If you ever use VS Code remotely, the extension would read the token from the wrong machine.~~
~~Compile errors in the Problems panel: a compile or diagnostics operation on top of the Roslyn scripting layer, with the extension publishing the results as VS Code diagnostics.~~
~~IntelliSense for the player API: a generated stub project or reference assembly so VS Code’s C# tooling knows the nb API.~~
~~Hiding undiscovered servers: so the explorer doesn’t spoil the network map.~~