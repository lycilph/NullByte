# NullByte for VS Code

Edit NullByte player scripts (`.cx`) in VS Code while the game is running.

## Getting started

1. Start the game with `--vscode`.
2. In VS Code, run **NullByte: Open Game Workspace**.
3. Each server you have discovered is a folder under `NullByte`. Open, edit and save scripts as usual, then `run` them in the game.

Changes you make in the game (`edit`, `scp`, `mkdir`, `mv`, `rm` and so on) show up in VS Code immediately, and newly discovered servers appear as you find them.

## Features

- Create, edit, rename and delete scripts **and folders** on the servers you have discovered
- Compile errors and warnings in the Problems panel
- Completion, hover and signature help for the NullByte API, with each method's RAM cost
- Suggestions follow the same whitelist as the game's compiler, so they only offer what can actually compile
- A warning when you save a script that was changed in the game after you opened it, with Compare and Overwrite

## Folders

Use **New Folder**, rename, drag and drop and delete in the explorer. Folders can be empty, and they are saved with the game. Paths are relative to the server root, at most 8 levels deep, and case-sensitive. Servers themselves are read-only: you cannot create, rename or delete them.

In the game's console:

| Command | Purpose |
|---|---|
| `ls [directory]` | List a directory (folders first, shown with a trailing `/`) |
| `mkdir <directory>` | Create a directory and any missing parents |
| `rmdir <directory>` | Delete an empty directory |
| `rm [-r] <path>` | Delete a file, or with `-r` a directory and everything in it |
| `mv <source> <destination>` | Move or rename a file or directory |
| `scp <file...> <server>` | Copy files to another server (files only) |

## Game arguments

| Argument | Purpose |
|---|---|
| `--vscode` | Starts the VS Code integration |
| `--vscode-port <n>` | Use this port. Without it the game tries 8765, then a free port |
| `--vscode-file <path>` | Write the connection file here instead of `~/.nullbyte/editor.json` |

## Settings

- `nullbyte.connectionFile`: where to find the connection file (the counterpart of `--vscode-file`)

## Troubleshooting

The status bar item shows the connection state. Hover it to see why it is disconnected, click it to open the log. **NullByte: Reconnect** retries immediately.