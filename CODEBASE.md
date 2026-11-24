# NullByte Codebase Map

Use this file to identify the source and tests needed for a task.

Read:

- `AI_CONTEXT.md` for development rules.
- `CURRENT_STATUS.md` for active work.
- `ARCHITECTURE.md` for system structure.
- `DESIGN.md` for intended behavior.

Inspect current source before proposing exact APIs.

---

# 1. Projects

```text
NullByte.Core
  Domain, formulas, services, simulation, persistence models,
  virtual filesystem and script-facing API.

NullByte.Scripting
  Roslyn compilation, includes, RAM analysis, language services,
  assembly loading and script processes.

NullByte.Shell
  Game session, save management and autosave.

NullByte.ConsoleUI
  Executable host, terminal, commands, completion and built-in editor.

NullByte.Editor
  C# WebSocket server and editor protocol.

NullByte.vscode
  TypeScript VS Code extension.
```

Tests:

```text
NullByte.Core.Tests
NullByte.Scripting.Tests
NullByte.ConsoleUI.Tests
NullByte.Editor.Tests
```

Dependency direction is described in `ARCHITECTURE.md`.

---

# 2. Core map

## Domain

`NullByte.Core/Domain/`

- `GameState.cs` - authoritative mutable game state
- `Player.cs` - player state
- `PlayerSkills.cs` - skill state
- `Server.cs` - server state
- `Network.cs` - server collection/topology
- `VirtualFileSystem.cs` - in-game filesystem
- `VirtualPath.cs` - path validation/normalization
- `PathResolver.cs` - contextual path resolution
- `Botnet.cs`, `BotnetNode.cs` - Botnet state

Activities:

`NullByte.Core/Domain/Activities/`

Contains activity, employment, job, course, company and training models.

## Services

`NullByte.Core/Services/`

- `ServerActions.cs` - Hack/Grow/Weaken
- `NetworkService.cs` - network operations
- `ActivityService.cs` - activity transitions
- `BotnetService.cs` - purchases/upgrades
- `WorkingDirectory.cs` - console working directory
- `IGameOutput.cs` - game output abstraction

## Formulas

`NullByte.Core/Formulas/`

- `HackingFormulas.cs`
- `NetworkFormulas.cs`
- `SkillFormulas.cs`
- `ActivityFormulas.cs`
- `ActivityRates.cs`
- `BotnetFormulas.cs`

## Simulation and time

- `Simulation/GameLoop.cs` - simulation loop
- `Simulation/IGameThread.cs` - game-thread boundary
- `Threading/GameSynchronizationContext.cs`
- `Time/IGameClock.cs`
- `Time/RealGameClock.cs`
- `Time/FakeGameClock.cs`
- `Time/GameScheduler.cs`

Systems:

- `Systems/ISimulationSystem.cs`
- `Systems/ActivitySystem.cs`
- `Systems/BotnetSystem.cs`

## Script-facing API

`NullByte.Core/Scripting/`

- `INullByteApi.cs`, `NullByteApi.cs`
- `INullByteApiFactory.cs`, `NullByteApiFactory.cs`
- `INullByteFilesApi.cs`, `NullByteFilesApi.cs`
- `INullByteBotnetApi.cs`, `NullByteBotnetApi.cs`
- `IScript.cs` - compiled-script contract
- `IScriptOperations.cs` - process operations
- `RamCostAttribute.cs`
- `ScriptProcessInfo.cs`, `ScriptProcessState.cs`

## Persistence and world data

`NullByte.Core/Persistence/`

- `SaveGame.cs` - persistent model
- `SaveGameMapper.cs` - state/save mapping
- `SaveGameSerializer.cs`
- `SaveMigration.cs`
- `Migrations/` - version migrations

World/data:

- `Data/initial-network.json`
- `Data/activities.json`
- `World/` - loaders and initial-state creation

---

# 3. Scripting map

## Compilation

`NullByte.Scripting/Compilation/`

- `ScriptCompiler.cs` - Roslyn pipeline
- `IScriptCompiler.cs`
- `ScriptSourceGenerator.cs` - generated wrapper
- `CompiledScript.cs`
- `ScriptCompilationResult.cs`
- `ScriptDiagnostic.cs`
- `ScriptRamAnalyzer.cs` - semantic RAM calculation
- `ScriptRamResult.cs`
- `SourceSnapshot.cs` - immutable source set
- `IncludeScanner.cs` - parses include directives
- `IncludeResolver.cs` - resolves include graph

## Runtime

`NullByte.Scripting/Runtime/`

- `ScriptService.cs` - compile/start/restore orchestration
- `ScriptRunner.cs` - executes compiled scripts
- `ScriptProcess.cs`
- `ScriptProcessManager.cs`
- `ScriptStartResult.cs`
- `ScriptRestoreResult.cs`
- `ServerSourceProvider.cs` - reads server script source

## Assembly loading

`NullByte.Scripting/Loading/`

- `ScriptAssemblyLoader.cs`
- `ScriptAssembly.cs`
- `ScriptLoadContext.cs`

## Language service

- `LanguageService/ScriptLanguageService.cs`

Provides diagnostics, completion, hover, signature help and definition lookup.

---

# 4. Host and UI map

## Shell

`NullByte.Shell/`

- `GameSession.cs` - active simulation composition
- `Persistence/SaveManager.cs`
- `Persistence/AutosaveService.cs`
- `Persistence/ISaveStore.cs`
- `Persistence/FileSaveStore.cs`

## Console host

`NullByte.ConsoleUI/`

- `Program.cs` - process startup/composition
- `GameHost.cs` - host/session lifecycle
- `LoadRequest.cs`
- `ShutdownRequest.cs`
- `NanoEditor.cs`

Use the current source for the unified host request/mailbox and settings implementations; the packed repository may predate their latest forms.

## Commands

`NullByte.ConsoleUI/Commands/`

Infrastructure:

- `CommandDefinition.cs`
- `CommandRegistry.cs`
- `CommandDispatcher.cs`
- `CommandSetup.cs`
- `ICommandGroup.cs`

Groups:

- `ActionCommands.cs`
- `ActivityCommands.cs`
- `BotnetCommands.cs`
- `DebugCommands.cs`
- `FileCommands.cs`
- `NetworkCommands.cs`
- `PersistenceCommands.cs`
- `PlayerCommands.cs`
- `ScriptCommands.cs`
- `SystemCommands.cs`

## Terminal

`NullByte.ConsoleUI/Terminal/`

Key files:

- `IGameTerminal.cs`
- `ThreadSafeConsoleTerminal.cs`
- `CommandLineParser.cs`
- `CompletionEngine.cs`
- `CompletionCatalog.cs`
- `CompletionSnapshot.cs`
- `InputHistory.cs`
- `LivePanel.cs`
- `PanelFormatter.cs`

---

# 5. Editor map

## C# server

`NullByte.Editor/`

- `EditorServer.cs` - lifecycle, connections and notifications
- `EditorProtocol.cs` - request/response protocol
- `EditorConnection.cs` - active connection
- `EditorFileService.cs` - virtual-filesystem adapter
- `EditorConnectionFile.cs` - connection discovery
- `EditorToken.cs` - authentication token

## VS Code extension

`NullByte.vscode/`

- `src/extension.ts` - activation/composition
- `src/client.ts` - WebSocket protocol client
- `src/commands.ts` - extension commands
- `src/filesystem.ts` - VS Code filesystem provider
- `src/paths.ts` - URI/path helpers
- `src/status.ts` - connection status
- `src/diagnostics.ts`
- `src/completion.ts`
- `src/hover.ts`
- `src/signature.ts`
- `src/definition.ts`
- `package.json` - command/configuration contributions
- `syntaxes/nullbyte-script.tmLanguage.json`

Protocol changes normally require both C# and TypeScript changes.

---

# 6. Task index

## Hack/Grow/Weaken

```text
ServerActions.cs
HackingFormulas.cs
Server.cs
Player.cs
```

If scripts are affected:

```text
INullByteApi.cs
NullByteApi.cs
```

Tests:

```text
ServerActionsTests.cs
ServerActionsSimulationTests.cs
HackingFormulasTests.cs
```

## New script API method

```text
INullByteApi.cs
NullByteApi.cs
RamCostAttribute.cs
relevant Core service
ScriptRamAnalyzer.cs
```

Inspect API and RAM/compiler tests.

## Script compilation

```text
ScriptCompiler.cs
ScriptSourceGenerator.cs
CompiledScript.cs
ScriptCompilationResult.cs
ScriptDiagnostic.cs
ScriptRamAnalyzer.cs
```

Tests: `Scripting.Tests/Compilation/`

## Includes

```text
IncludeScanner.cs
IncludeResolver.cs
SourceSnapshot.cs
ServerSourceProvider.cs
ScriptCompiler.cs
ScriptRamAnalyzer.cs
ScriptLanguageService.cs
```

Tests:

```text
IncludeResolverTests.cs
IncludeCompilationTests.cs
IncludeRamTests.cs
IncludeLanguageServiceTests.cs
LibraryOnItsOwnTests.cs
```

## Script processes

```text
ScriptService.cs
ScriptRunner.cs
ScriptProcess.cs
ScriptProcessManager.cs
NullByteApiFactory.cs
IScriptOperations.cs
```

Tests: `Scripting.Tests/Runtime/`

## Virtual filesystem

```text
VirtualFileSystem.cs
VirtualPath.cs
```

Add the relevant adapter:

```text
FileCommands.cs
NullByteFilesApi.cs
EditorFileService.cs
filesystem.ts
```

Inspect filesystem, path and adapter tests.

## Console paths/completion

```text
WorkingDirectory.cs
PathResolver.cs
FileCommands.cs
CompletionEngine.cs
CompletionCatalog.cs
CompletionSnapshot.cs
```

Tests include:

```text
WorkingDirectoryTests.cs
PathCompletionTests.cs
ScriptCommandsPathTests.cs
CompletionEngineTests.cs
```

## Game loop/threading/time

```text
GameLoop.cs
IGameThread.cs
GameSynchronizationContext.cs
GameScheduler.cs
IGameClock.cs
```

Tests include:

```text
GameLoopTests.cs
GameLoopSystemTests.cs
GameLoopResilienceTests.cs
GameSynchronizationContextTests.cs
GameSchedulerTests.cs
GameThreadHarness.cs
```

## Activities

```text
Domain/Activities/
ActivityService.cs
ActivitySystem.cs
ActivityFormulas.cs
ActivityRates.cs
Data/activities.json
ActivityCommands.cs
```

Tests: activity formula, service, system and command tests.

## Botnet

```text
Botnet.cs
BotnetNode.cs
BotnetService.cs
BotnetSystem.cs
BotnetFormulas.cs
BotnetCommands.cs
NullByteBotnetApi.cs
```

Tests: Botnet formula, service, system, API and early-game tests.

## Save format

```text
SaveGame.cs
SaveGameMapper.cs
SaveGameSerializer.cs
SaveMigration.cs
Persistence/Migrations/
```

Tests:

```text
SaveGameTests.cs
SaveDirectoriesTests.cs
GoldenSaves.cs
```

## Save/load/reset

```text
GameHost.cs
GameSession.cs
PersistenceCommands.cs
SaveManager.cs
AutosaveService.cs
EditorServer.cs
```

Also inspect current host mailbox/request types.

## Startup/settings

```text
Program.cs
GameHost.cs
GameSession.cs
current settings and CLI parsing files
```

The latest local source is authoritative.

## Console command

```text
relevant Commands/Groups/*Commands.cs
CommandSetup.cs
CommandDefinition.cs
CommandRegistry.cs
CommandDispatcher.cs
```

Then inspect the service that owns the actual behavior.

## Editor connectivity

```text
EditorServer.cs
EditorConnection.cs
EditorConnectionFile.cs
EditorToken.cs
EditorProtocol.cs
client.ts
extension.ts
status.ts
```

## Editor protocol operation

```text
EditorProtocol.cs
EditorServer.cs
client.ts
relevant TypeScript feature module
```

Check game-thread access.

## VS Code command

```text
commands.ts
extension.ts
package.json
client.ts
EditorProtocol.cs
```

## Diagnostics/language features

```text
ScriptLanguageService.cs
EditorProtocol.cs
ScriptDiagnostic.cs
ScriptCompiler.cs
```

Add the relevant TypeScript file:

```text
diagnostics.ts
completion.ts
hover.ts
signature.ts
definition.ts
```

---

# 7. Test map

## Core

`NullByte.Core.Tests/`

Key areas:

```text
Formulas/
Services/
Simulation/
Systems/
Threading/
Time/
Persistence/
Scripting/
World/
```

Useful helpers:

- `Helpers/GameThreadHarness.cs`
- `TestDoubles/SequenceRandomSource.cs`

## Scripting

`NullByte.Scripting.Tests/`

Key areas:

```text
Compilation/
Runtime/
Loading/
LanguageService/
Acceptance/
```

## ConsoleUI

`NullByte.ConsoleUI.Tests/`

Key areas:

```text
Commands/
Terminal/
Persistence/
```

## Editor

`NullByte.Editor.Tests/`

Contains editor filesystem/folder behavior tests.

---

# 8. Context-loading workflow

For a task:

1. Read `AI_CONTEXT.md` and `CURRENT_STATUS.md`.
2. Use this file to identify the subsystem.
3. Load primary implementation files.
4. Load interfaces/direct collaborators.
5. Load relevant tests.
6. Expand only when another dependency proves necessary.
7. For architectural changes, also load `ARCHITECTURE.md`.
8. For gameplay decisions, load the relevant section of `DESIGN.md`.

Always use the current version of every file being modified.

A typical task should begin with 5-10 source/test files, not a full repository dump.

Update this map only when major entry points, projects or subsystem ownership change.