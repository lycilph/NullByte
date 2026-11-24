# NullByte Codebase Map

Use this file to identify the current source and tests needed for a task. Read `AI_CONTEXT.md` for rules, `CURRENT_STATUS.md` for active work, `ARCHITECTURE.md` for structure and `DESIGN.md` for intended behavior.

## 1. Projects

```text
NullByte.Core        Domain, formulas, services, simulation, VFS, persistence models, script API
NullByte.Scripting   Roslyn compilation, includes, RAM, language service, runtime/processes
NullByte.Shell       Game session, save storage/management, autosave
NullByte.ConsoleUI   Executable host, configuration, terminal, commands, completion
NullByte.Editor      WebSocket editor server/protocol/adapters
NullByte.vscode      TypeScript VS Code extension
```

Tests: `NullByte.Core.Tests`, `NullByte.Scripting.Tests`, `NullByte.Shell.Tests`, `NullByte.ConsoleUI.Tests`, `NullByte.Editor.Tests`.

## 2. Core map

### Domain

`NullByte.Core/Domain/`

- `GameState.cs` - authoritative mutable state
- `Player.cs`, `PlayerSkills.cs`
- `Server.cs`, `Network.cs`
- `VirtualFileSystem.cs`, `VirtualPath.cs`, `PathResolver.cs`
- `Botnet.cs`, `BotnetNode.cs`
- `Activities/` - activity, job, course, company, crime and training models

### Services/formulas

`NullByte.Core/Services/`

- `ServerActions.cs` - Hack/Grow/Weaken
- `NetworkService.cs`
- `ActivityService.cs` - activity lifecycle, jobs/courses/crimes
- `CrimeAttempt.cs`
- `BotnetService.cs`
- `WorkingDirectory.cs`

`NullByte.Core/Formulas/`: hacking, network, skills, Botnet, activities and `CrimeRewards.cs`.

### Simulation/time

- `Simulation/GameLoop.cs`, `IGameThread.cs`
- `Threading/GameSynchronizationContext.cs`
- `Time/IGameClock.cs`, `RealGameClock.cs`, `FakeGameClock.cs`, `GameScheduler.cs`
- `Systems/ActivitySystem.cs`, `BotnetSystem.cs`

### Script-facing API

`NullByte.Core/Scripting/`: `INullByteApi`, `NullByteApi`, files/Botnet APIs, `IScript`, `IScriptOperations`, `RamCostAttribute`, process info/state.

### Persistence/world

- `Persistence/SaveGame.cs`, `SaveGameMapper.cs`, `SaveGameSerializer.cs`, `SaveMigration.cs`
- `Persistence/Migrations/V1ToV2Migration.cs` through `V3ToV4Migration.cs`
- `Data/initial-network.json`, `Data/activities.json`
- `World/` loaders and `InitialGameStateFactory.cs`

## 3. Scripting map

### Compilation

`NullByte.Scripting/Compilation/`

- `ScriptCompiler.cs` - Roslyn pipeline
- `ScriptSourceGenerator.cs` - generated wrapper
- `ScriptRamAnalyzer.cs` - semantic RAM
- `ScriptDiagnostic.cs`
- `IncludeScanner.cs`, `IncludeResolver.cs`
- `SourceSnapshot.cs`

### Runtime/loading

- `Runtime/IScriptService.cs` - editor/host-facing script service contract
- `Runtime/ScriptService.cs` - compile/start/restore/measure orchestration
- `Runtime/ScriptRunner.cs`
- `Runtime/ScriptProcess.cs`, `ScriptProcessManager.cs`
- `Runtime/ServerSourceProvider.cs`
- `Loading/ScriptAssemblyLoader.cs`, `ScriptLoadContext.cs`

### Language service

`LanguageService/ScriptLanguageService.cs` - diagnostics, completion, hover, signatures and definitions.

## 4. Host/UI map

### Shell

- `GameSession.cs`
- `Persistence/SaveManager.cs`, `AutosaveService.cs`, `ISaveStore.cs`, `FileSaveStore.cs`

### Console host

- `Program.cs` - process composition/config mapping
- `GameHost.cs` - active session lifecycle
- `HostRequest.cs`, `HostRequestMailbox.cs`
- `NanoEditor.cs`

### Configuration

`NullByte.ConsoleUI/Configuration/`

- `AppSettings.cs`, `ApplicationOptions.cs`
- `CommandLineOptions.cs`, `CommandLineParser.cs`
- `SettingsFileLoader.cs`, `SettingsLoader.cs`
- `SettingsValidator.cs`, `SettingsValidationException.cs`

### Commands

`Commands/Groups/`: action, activity, Botnet, debug, file, network, persistence, player, script and system commands. Wiring is in `CommandSetup.cs`.

### Terminal

`Terminal/`: `IGameTerminal`, `ThreadSafeConsoleTerminal`, terminal `CommandLineParser`, completion infrastructure, history, hotkeys and live panels.

## 5. Editor map

### C# server

`NullByte.Editor/`

- `EditorServer.cs` - lifecycle/connections/events; implements `IExternalEditorStatus`
- `IExternalEditorStatus.cs`, `ExternalEditorStatus.cs`, `ExternalEditorConnectionInfo.cs`
- `EditorProtocol.cs` - JSON protocol
- `EditorFileService.cs` - VFS plus run/RAM operations
- `EditorConnection.cs`, `EditorConnectionFile.cs`, `EditorToken.cs`

### VS Code

`NullByte.vscode/src/`

- `extension.ts` - composition
- `client.ts` - WebSocket client
- `commands.ts` - extension commands
- `runCodeLens.ts` - script Run CodeLens
- `filesystem.ts`, `paths.ts`, `status.ts`
- `diagnostics.ts`, `completion.ts`, `hover.ts`, `signature.ts`, `definition.ts`
- `package.json` - contributions/configuration

## 6. Task index

### Hack/Grow/Weaken
`ServerActions.cs`, `HackingFormulas.cs`, `Server.cs`, `Player.cs`; tests: `ServerActions*`, `HackingFormulasTests`.

### Activities/jobs/study/crime
`Domain/Activities/`, `ActivityService.cs`, `ActivitySystem.cs`, `ActivityFormulas.cs`, `CrimeRewards.cs`, `activities.json`, `ActivityCommands.cs`; tests under Core `Activities/`, Services/Systems, persistence and ConsoleUI Commands.

### Script API
`INullByteApi.cs`, `NullByteApi.cs`, relevant Core service, `RamCostAttribute.cs`, `ScriptRamAnalyzer.cs`.

### Compilation/includes
`ScriptCompiler.cs`, `ScriptSourceGenerator.cs`, `IncludeScanner.cs`, `IncludeResolver.cs`, `SourceSnapshot.cs`, `ServerSourceProvider.cs`, `ScriptRamAnalyzer.cs`, `ScriptLanguageService.cs`; tests under `Scripting.Tests/Compilation` and `LanguageService`.

### Script runtime/processes
`IScriptService.cs`, `ScriptService.cs`, `ScriptRunner.cs`, `ScriptProcess.cs`, `ScriptProcessManager.cs`, `NullByteApiFactory.cs`; tests under `Scripting.Tests/Runtime`.

### Virtual filesystem/paths
`VirtualFileSystem.cs`, `VirtualPath.cs`, then relevant adapter: `FileCommands.cs`, `NullByteFilesApi.cs`, `EditorFileService.cs`, `filesystem.ts`.

### Game loop/threading/time
`GameLoop.cs`, `IGameThread.cs`, `GameSynchronizationContext.cs`, `GameScheduler.cs`, `IGameClock.cs`; use `GameThreadHarness.cs` and relevant Simulation/Threading/Time tests.

### Persistence/save format
`SaveGame.cs`, `SaveGameMapper.cs`, serializer, migrations; tests include `SaveGameTests.cs`, `SaveActivityTests.cs`, `SaveDirectoriesTests.cs`, `GoldenSaves.cs`.

### Save/load/reset
`GameHost.cs`, `HostRequest.cs`, `HostRequestMailbox.cs`, `GameSession.cs`, `PersistenceCommands.cs`, `SaveManager.cs`, `AutosaveService.cs`, `EditorServer.cs`.

### Startup/settings
`Program.cs` plus `Configuration/`; tests under `ConsoleUI.Tests/Configuration`.

### Editor connection status
`DebugCommands.cs`, `CommandSetup.cs`, `IExternalEditorStatus.cs`, `EditorServer.cs`.

### Editor run/RAM
`EditorFileService.cs`, `EditorProtocol.cs`, `IScriptService.cs`, `ScriptService.cs`, `client.ts`, `commands.ts`, `runCodeLens.ts`, `package.json`; tests include `EditorFileServiceRunTests.cs`.

### Diagnostics/language features
`ScriptDiagnostic.cs`, `ScriptCompiler.cs`, `ScriptLanguageService.cs`, `EditorProtocol.cs`, plus the relevant TypeScript provider.

## 7. Context workflow

1. Read `AI_CONTEXT.md` and `CURRENT_STATUS.md`.
2. Use this map to select primary source, contracts and tests.
3. Expand only when dependencies prove necessary.
4. Load `ARCHITECTURE.md` for structural changes and `DESIGN.md` for gameplay decisions.
5. Always use current versions of files being modified.

A typical task should start with 5-10 source/test files, not a full repository dump.
