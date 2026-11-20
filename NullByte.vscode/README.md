# NullByte for VS Code

Edit NullByte player scripts (`.cx`) in VS Code while the game is running.

## Getting started

1. Start the game with `--vscode`.
2. In VS Code, run **NullByte: Open Game Workspace**.
3. Each server you have discovered shows up as a folder. Open, edit and save scripts as usual, then `run` them in the game.

New servers appear in the explorer as you discover them, and files you change in the game's console show up in VS Code immediately.

## Features

- Create, edit, rename and delete scripts on the servers you have discovered
- Compile errors and warnings in the Problems panel
- Completion, hover and signature help for the NullByte API, with each method's RAM cost
- Suggestions follow the same whitelist as the game's compiler, so they only offer what can actually compile

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