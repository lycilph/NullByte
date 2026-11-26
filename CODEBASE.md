# NullByte Codebase Map

## Purpose

This file is a compact map of the repository. It answers **where to look first** when changing a feature; it is not intended to document every class.

## Top-level structure

```text
/
├── NullByte.slnx            solution (groups projects into virtual src/ and test/ folders)
├── NullByte.Core/           game domain and rules
├── NullByte.Scripting/      Roslyn pipeline and script runtime
├── NullByte.Shell/          session and persistence lifecycle
├── NullByte.ConsoleUI/      console executable
├── NullByte.Editor/         external-editor server
├── NullByte.vscode/         VS Code extension (TypeScript)
├── NullByte/                minimal WPF shell
├── NullByte.*.Tests/        one test project per layer above (except vscode/WPF)
└── *.md                     README, ARCHITECTURE, CODEBASE, DECISIONS, DEVELOPMENT,
                             ROADMAP, todo, AIPrompts, Game Design Document
```

All projects are siblings at the repository root. The `src/` and `test/` folders exist only inside `NullByte.slnx`, not on disk.

The important architectural split is:

| Area | Responsibility |
|---|---|
| `NullByte.Core` | Game domain, gameplay rules, simulation, VFS, persistence models, scripting API |
| `NullByte.Scripting` | Roslyn compilation, includes, RAM analysis, script runtime and language services |
| `NullByte.Shell` | Game session and persistence lifecycle |
| `NullByte.ConsoleUI` | Console executable, host lifecycle, commands and terminal UI |
| `NullByte.Editor` | External-editor server, protocol and adapters |
| `NullByte.vscode` | VS Code extension |
| `NullByte` | Minimal WPF shell; currently not part of the playable runtime composition |
| `NullByte.*.Tests` | Automated tests for the corresponding layers |

## Core

### Domain

Look in `NullByte.Core/Domain` for persistent game concepts:

- `GameState.cs` — root mutable game state.
- `Player.cs`, `PlayerSkills.cs` — player state and skills.
- `Server.cs`, `Network.cs` — network/server world model.
- `VirtualFileSystem.cs`, `VirtualPath.cs`, `PathResolver.cs` — player files, folders and path handling.
- `Botnet.cs`, `BotnetNode.cs` — botnet state.
- `Domain/Activities/` — activity state and definitions.

### Gameplay services

Look in `NullByte.Core/Services` for operations over the domain:

- `ServerActions.cs` — server actions such as hacking-related operations.
- `NetworkService.cs` — network operations.
- `ActivityService.cs` — activity lifecycle and attempts.
- `CrimeAttempt.cs` — crime resolution.
- `BotnetService.cs` — botnet operations.
- `WorkingDirectory.cs` — current-directory/path behavior.

New gameplay rules go here (or in formulas/systems), not in adapters. See `ARCHITECTURE.md`.

### Formulas

The formula areas contain deterministic calculations for hacking, networking, skills, botnets, activities and crimes.

When changing balance, first locate the existing formula rather than embedding a new calculation in a caller.

### Simulation, threading and time

- `Simulation/GameLoop.cs` — main simulation loop.
- `Simulation/IGameThread.cs` — game-thread abstraction.
- `Threading/GameSynchronizationContext.cs` — game-thread dispatch.
- `Time/IGameClock.cs` — game-time abstraction.
- `Time/RealGameClock.cs`, `FakeGameClock.cs` — real/test clocks.
- `Time/GameScheduler.cs` — scheduled game-time work.
- `Systems/ActivitySystem.cs`, `BotnetSystem.cs` — recurring simulation systems.

Game-thread ownership rules are in `ARCHITECTURE.md`.

### Core scripting API

Look in `NullByte.Core/Scripting` for the public API exposed to player scripts:

- `INullByteApi.cs`, `NullByteApi.cs`
- file and botnet APIs
- activity APIs
- `IScript.cs`, `IScriptOperations.cs`
- process information/state
- `RamCostAttribute.cs`

### Persistence and world data

- `Persistence/SaveGame.cs` — versioned save representation.
- `Persistence/SaveGameMapper.cs` — live state ↔ save model.
- `Persistence/SaveGameSerializer.cs` — serialization.
- `Persistence/SaveMigration.cs` — migration abstraction.
- `Persistence/Migrations/` — version-to-version migrations (`VnToVm`); the highest number is the current save version.
- `Data/initial-network.json` — initial network data.
- `Data/activities.json` — activity definitions.
- `InitialGameStateFactory.cs` — initial world construction.

## Scripting

### Compilation and analysis

Look in `NullByte.Scripting/Compilation`:

- `ScriptCompiler.cs` — Roslyn compilation.
- `ScriptSourceGenerator.cs` — generated script wrapper/source.
- `ScriptRamAnalyzer.cs` — RAM analysis.
- `ScriptDiagnostic.cs` — script diagnostics.
- `IncludeScanner.cs`, `IncludeResolver.cs` — include handling.
- `SourceSnapshot.cs` — immutable source input for background work.

### Runtime

Look in `NullByte.Scripting/Runtime`:

- `IScriptService.cs`, `ScriptService.cs` — compile/start/restore and RAM coordination.
- `ScriptRunner.cs` — execution.
- `ScriptProcess.cs` — process state.
- `ScriptProcessManager.cs` — running-process tracking.
- `ServerSourceProvider.cs` — source lookup.

### Loading and language services

- `Loading/ScriptAssemblyLoader.cs`, `ScriptLoadContext.cs` — assembly loading/isolation.
- `LanguageService/ScriptLanguageService.cs` — editor-facing language features.

## Shell and console

### Shell

`NullByte.Shell` owns the game session and persistence lifecycle:

- `GameSession.cs`
- `Persistence/SaveManager.cs`
- `Persistence/AutosaveService.cs`
- `Persistence/ISaveStore.cs`
- `Persistence/FileSaveStore.cs`

### Console host

`NullByte.ConsoleUI` contains:

- `Program.cs` — executable entry point.
- `GameHost.cs` — runtime composition and session ownership.
- `HostRequest.cs`, `HostRequestMailbox.cs` — host-level lifecycle requests.
- `NanoEditor.cs` — editor integration entry point.
- command groups for action, activity, botnet, debug, file, network, persistence, player, script and system operations.
- terminal parsing, completion, history, hotkeys and live panels.
- configuration under `Configuration/`.
- the presentation-only startup sequence under `Startup/`; it runs before the interactive terminal takes ownership of the console.

Load/reset/shutdown go through `HostRequestMailbox` (see `ARCHITECTURE.md`, Host lifecycle).

## External editor

`NullByte.Editor` contains:

- `EditorServer.cs` — WebSocket server.
- `EditorProtocol.cs` — JSON protocol.
- `EditorConnection.cs`, `EditorConnectionFile.cs` — connections.
- `EditorFileService.cs` — file operations.
- `EditorToken.cs` — authentication.
- `IExternalEditorStatus.cs`, `ExternalEditorStatus.cs`, `ExternalEditorConnectionInfo.cs` — narrow status model.

The editor crosses into the game through the same services used by the game itself.

## VS Code extension

`NullByte.vscode/src` contains:

- `extension.ts` — extension activation and dependency wiring.
- `connection.ts` — connection-file path resolution and validation.
- `client.ts` — editor protocol client.
- `commands.ts` — command class and command behavior.
- `argumentParser.ts` — shell-style script argument parsing.
- `codeLens.ts` — run and RAM-cost actions.
- `filesystem.ts`, `paths.ts` — virtual filesystem/path mapping.
- `status.ts` — connection/status UI.
- `diagnostics.ts` — diagnostics.
- `completion.ts`, `hover.ts`, `signature.ts`, `definition.ts` — language features.

## Where to look by feature

### Hack / grow / weaken

1. Core domain/server model.
2. `NullByte.Core/Services/ServerActions.cs`.
3. Relevant formula.
4. Console command or script API adapter.
5. Corresponding Core tests.

### Activities

1. `Domain/Activities/`.
2. `ActivityService.cs`.
3. `Systems/ActivitySystem.cs`.
4. Activity formulas/data.
5. `NullByte.Activities` scripting API.
6. Focused/background tests.

### Scripts

1. `Compilation/`.
2. `Runtime/`.
3. Core scripting API.
4. Script-related tests.

### Includes

Start with `IncludeScanner.cs`, `IncludeResolver.cs` and `SourceSnapshot.cs`, then inspect compiler/runtime tests.

### Files and folders

Start with `VirtualFileSystem.cs`, `VirtualPath.cs`, `PathResolver.cs`, then the editor file service and console file commands.

### Persistence

Start with `SaveGame.cs`, `SaveGameMapper.cs`, `SaveGameSerializer.cs`, `SaveMigration.cs` and the relevant migration. Then inspect `SaveManager` and migration/golden-file tests.

### Game lifecycle

Start with `GameHost.cs`, `HostRequestMailbox.cs`, `GameSession.cs` and `SaveManager.cs`.

### Editor integration

Start with `NullByte.Editor/EditorServer.cs`, then the protocol/client and the relevant Core/Scripting service.

## Context-loading rule

For a normal change, the useful working set is:

1. `ARCHITECTURE.md`
2. The relevant section of this file.
3. The current implementation files.
4. The corresponding tests.
5. `DECISIONS.md` if an architectural choice is involved.

Avoid loading the entire repository or a full Repomix dump unless a cross-cutting investigation genuinely requires it.
