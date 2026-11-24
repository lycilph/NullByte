# NullByte Codebase Map

This document is a compact map of the NullByte repository.

Its purpose is to help developers and AI tools quickly identify which projects, directories, and source files are relevant to a task without loading the entire repository.

For architectural rules and invariants, read `AI_CONTEXT.md`.

For intended game behavior and milestone definitions, read the Game Design Document.

The source code remains authoritative for the current implementation.

---

# 1. Solution overview

The main solution is:

`NullByte.slnx`

The repository is divided into these primary components:

```text
NullByte.Core
    Domain model, simulation, game services, formulas,
    persistence contracts and script-facing API.

NullByte.Scripting
    Roslyn compilation, includes, RAM analysis,
    script loading/execution and language services.

NullByte.Shell
    Game session and persistence/application lifecycle.

NullByte.ConsoleUI
    Console host, commands, terminal and application startup.

NullByte.Editor
    Host-side VS Code/editor integration.

NullByte.vscode
    TypeScript VS Code extension.
```

There are corresponding test projects for the major .NET components.

---

# 2. High-level dependency map

The approximate dependency direction is:

```text
                    NullByte.ConsoleUI
                           |
                +----------+----------+
                |                     |
                v                     v
         NullByte.Shell        NullByte.Editor
                |                     |
                +----------+----------+
                           |
                           v
                  NullByte.Scripting
                           |
                           v
                    NullByte.Core
```

This is a conceptual map rather than a substitute for inspecting `.csproj` references.

The important architectural direction is that `NullByte.Core` contains the reusable game domain and must not depend on presentation or editor projects.

`NullByte.vscode` is a separate TypeScript application communicating with `NullByte.Editor` through the editor protocol.

---

# 3. NullByte.Core

`NullByte.Core` contains the game domain and simulation.

Start here for changes to actual game mechanics.

---

## 3.1 Configuration

### `Configuration/BalanceConfig.cs`

Central game-balance configuration.

Inspect when changing configurable gameplay rates, durations or balance constants.

Do not use this as general application configuration.

---

# 4. Core domain

Located under:

`NullByte.Core/Domain/`

---

## `GameState.cs`

Central container for mutable game state.

One of the most important objects in the application.

Inspect when a feature adds persistent state or needs access to major game subsystems.

Because `GameState` is mutable game state, normal game-thread ownership rules apply.

---

## `Player.cs`

Represents the player and player-owned state.

Inspect for changes involving player progression, money, discovered servers or related state.

---

## `PlayerSkills.cs`

Player hacking and related skill state.

Inspect together with `SkillFormulas.cs` when modifying skill progression.

---

## `SkillKind.cs`

Identifiers for supported skills.

---

## `Server.cs`

Represents a server in the game network.

Contains server-specific state used by hacking, files, RAM and process execution.

Inspect for changes involving server properties or per-server behavior.

---

## `Network.cs`

Owns and resolves the collection of servers in the game world.

Inspect for changes involving server lookup, discovery or network topology.

---

## `PathResolver.cs`

Resolves paths in contexts where a current location or other path context applies.

Inspect when changing console path behavior.

Do not assume its semantics automatically apply to scripts or editor paths.

---

## `VirtualFileSystem.cs`

Authoritative implementation of the in-game filesystem.

Inspect for:

- reading/writing files
- directories
- rename/delete operations
- file events
- timestamps
- filesystem invariants

Changes here potentially affect ConsoleUI, scripts, persistence and VS Code.

---

## `VirtualPath.cs`

Canonical path validation and normalization.

Inspect before implementing path parsing elsewhere.

Prefer reusing this implementation over duplicating virtual-path rules.

---

# 5. Activities domain

Located under:

`NullByte.Core/Domain/Activities/`

Important files include:

### `Activity.cs`

Base activity concept.

### `ActivityState.cs`

Tracks the player's current activity state.

### `ActivityProgress.cs`

Represents activity progress.

### `ActivityMode.cs`

Activity execution mode.

### `ActivityEndReason.cs`

Reasons an activity stopped.

### `ClassActivity.cs`

Study/class activity.

### `JobActivity.cs`

Employment/work activity.

### `ActivityCatalog.cs`

Catalog of available activities.

### `CareerPath.cs`

Career progression model.

### `Company.cs`

Company definition/state.

### `Course.cs`

Course/training definition.

### `Crime.cs`

Crime activity definition.

### `Employment.cs`

Player employment state.

### `FacilityKind.cs`

Kinds of facilities used by activities.

### `Job.cs`

Job definition.

### `JobRank.cs`

Job progression/rank.

### `TrainingFacility.cs`

Training facility definition.

For activity changes, normally inspect this folder together with:

```text
Services/ActivityService.cs
Systems/ActivitySystem.cs
Formulas/ActivityFormulas.cs
Formulas/ActivityRates.cs
```

---

# 6. Botnet domain

Important files:

### `Domain/Botnet.cs`

Player botnet state.

### `Domain/BotnetNode.cs`

Individual botnet node.

### `Domain/BotnetUpgradeKind.cs`

Available node upgrade categories.

Botnet behavior is distributed across:

```text
Domain/Botnet*
Services/BotnetService.cs
Systems/BotnetSystem.cs
Formulas/BotnetFormulas.cs
```

Inspect these together for botnet gameplay changes.

---

# 7. Core formulas

Located under:

`NullByte.Core/Formulas/`

Formula classes should contain deterministic calculations rather than orchestration.

---

## `HackingFormulas.cs`

Hack/Grow/Weaken-related calculations.

Inspect for changes to hacking chance, duration, money/security effects or related mechanics.

---

## `SkillFormulas.cs`

Skill progression calculations.

---

## `BotnetFormulas.cs`

Botnet cost/production/upgrade calculations.

---

## `ActivityFormulas.cs`

Activity-related calculations.

---

## `ActivityRates.cs`

Calculated activity rates.

---

## `NetworkFormulas.cs`

Network-related calculations.

---

## `UnmetRequirement.cs`

Represents requirements that are not currently satisfied.

Used where progression or activities have prerequisites.

---

# 8. Core services

Located under:

`NullByte.Core/Services/`

Services generally orchestrate domain operations.

---

## `ServerActions.cs`

Shared implementation of server actions.

This is a key file for Hack/Grow/Weaken-style gameplay.

Inspect this before adding hacking behavior to commands or scripts.

Related result models live in:

`ServerActionResults.cs`

---

## `NetworkService.cs`

Higher-level operations involving the server network.

Related result:

`NetworkAnalyzeResult.cs`

---

## `ActivityService.cs`

Starts/stops/manages player activities.

Related types include:

```text
ActivityStartResult.cs
ActivityEndInfo.cs
JobPromotion.cs
```

---

## `BotnetService.cs`

Player-facing botnet operations such as purchasing/upgrading infrastructure.

Related result:

`BotnetPurchaseResult.cs`

---

## `WorkingDirectory.cs`

Console/session working-directory behavior.

Inspect for console-relative path features.

Do not automatically reuse the working-directory model for script execution.

---

## `IGameOutput.cs`

Abstraction for game-facing textual output.

Useful when game functionality needs to produce output without depending directly on ConsoleUI.

---

# 9. Simulation

Located under:

`NullByte.Core/Simulation/`

---

## `GameLoop.cs`

The central simulation loop.

A critical architectural file.

Inspect when changing:

- tick processing
- scheduled work
- game-loop lifecycle
- simulation-system execution
- game-thread behavior

Avoid modifying the game loop for isolated gameplay features unless necessary.

---

## `IGameThread.cs`

Abstraction representing access to game-thread execution.

Important at boundaries where work originates outside the simulation thread.

Editor and other external integrations use this boundary instead of directly accessing mutable game state.

---

# 10. Simulation systems

Located under:

`NullByte.Core/Systems/`

---

## `ISimulationSystem.cs`

Contract for systems updated as part of simulation progression.

---

## `ActivitySystem.cs`

Advances active player activities.

Inspect together with activity domain/service/formulas.

---

## `BotnetSystem.cs`

Advances passive botnet behavior.

Inspect together with botnet domain/service/formulas.

---

# 11. Threading

Located under:

`NullByte.Core/Threading/`

---

## `GameSynchronizationContext.cs`

Custom synchronization context used by the simulation.

A critical architectural component.

Inspect when investigating:

- async continuation behavior
- game-thread affinity
- asynchronous script/game operations
- synchronization bugs

Changes here have wide consequences.

Do not modify it as a local workaround without understanding `GameLoop` and scheduling behavior.

---

# 12. Game time and scheduling

Located under:

`NullByte.Core/Time/`

---

## `IGameClock.cs`

Abstraction for game time.

---

## `RealGameClock.cs`

Production implementation based on real elapsed time.

---

## `FakeGameClock.cs`

Deterministic/test clock.

Very useful when writing tests for time-based behavior.

---

## `GameScheduler.cs`

Schedules game-time operations.

Inspect together with:

```text
GameLoop.cs
GameSynchronizationContext.cs
IGameClock.cs
```

when changing timing or async behavior.

---

# 13. Randomness

Located under:

`NullByte.Core/Randomness/`

Important concepts include:

### `IRandomSource.cs`

Randomness abstraction used to keep game mechanics testable.

### Seeded random implementation

Used when deterministic/reproducible random behavior is required.

Prefer these abstractions over introducing direct global `Random` use into gameplay logic.

---

# 14. Script-facing API

Located under:

`NullByte.Core/Scripting/`

This folder defines the game API available to scripts and the contracts between Core and the scripting runtime.

---

## `INullByteApi.cs`

Primary script-facing API.

Start here when exposing a new game capability to scripts.

---

## `NullByteApi.cs`

Primary implementation of `INullByteApi`.

Bridges script calls into existing Core operations.

---

## `INullByteApiFactory.cs`

Contract for constructing script API instances.

---

## `NullByteApiFactory.cs`

Creates script API instances for running processes.

Inspect when process-specific context changes.

---

## `INullByteBotnetApi.cs`

Script-facing botnet API.

---

## `NullByteBotnetApi.cs`

Botnet API implementation.

---

## `INullByteFilesApi.cs`

Script-facing filesystem API.

---

## `NullByteFilesApi.cs`

Filesystem API implementation.

Uses the virtual filesystem rather than the host operating system.

---

## `IScript.cs`

Runtime contract implemented by compiled player scripts.

The Roslyn source generator/wrapper targets this contract.

Changes require inspection of the Scripting compilation pipeline.

---

## `IScriptOperations.cs`

Contract for script/process-related operations exposed through the API.

---

## `RamCostAttribute.cs`

Annotates script-facing API methods with their gameplay RAM cost.

Used by Roslyn RAM analysis.

---

## `ScriptProcessInfo.cs`

Public/process-facing information about a running script.

---

## `ScriptProcessState.cs`

Process lifecycle state.

---

## `BotnetNodeInfo.cs`

Script-facing botnet information model.

---

# 15. Core persistence

Located under:

`NullByte.Core/Persistence/`

---

## `SaveGame.cs`

Versioned serialized save representation.

Inspect when persistent game state changes.

---

## `SaveGameMapper.cs`

Maps between live game state and save models.

A key file for persistence changes.

---

## `SaveGameSerializer.cs`

Serializes/deserializes saves.

---

## `SaveMigration.cs`

Migration infrastructure.

---

## `SaveFormatException.cs`

Represents invalid/unsupported save data.

---

## Migrations

Located under:

`Persistence/Migrations/`

Current migration classes include:

```text
V1ToV2Migration.cs
V2ToV3Migration.cs
```

When changing persisted structures, inspect existing migrations and golden persistence tests.

Do not simply change `SaveGame` and assume old saves remain compatible.

---

# 16. Core data

Located under:

`NullByte.Core/Data/`

---

## `initial-network.json`

Initial server/network data.

Inspect when changing initial world topology or server data.

---

## `activities.json`

Activity definitions.

Inspect when changing data-driven activities, employment or training content.

---

# 17. NullByte.Scripting

This project owns compilation and execution of player `.cx` scripts.

For script behavior, first determine whether the change concerns:

```text
Compilation
LanguageService
Loading
Runtime
```

Then inspect the corresponding folder.

---

# 18. Script compilation

Located under:

`NullByte.Scripting/Compilation/`

---

## `ScriptCompiler.cs`

Central Roslyn compilation pipeline.

One of the most important scripting files.

Start here for changes involving:

- compiling `.cx`
- Roslyn syntax/semantic behavior
- metadata references
- diagnostics
- source generation
- includes
- RAM analysis integration

Do not modify compilation behavior without checking related compiler tests.

---

## `IScriptCompiler.cs`

Compiler abstraction used by runtime components.

---

## `CompiledScript.cs`

Represents successfully compiled script output and related metadata.

---

## `ScriptCompilationResult.cs`

Compilation success/failure result.

---

## `ScriptDiagnostic.cs`

NullByte representation of compiler diagnostics.

---

## `ScriptDiagnosticSeverity.cs`

Diagnostic severity model.

---

## `ScriptSourceGenerator.cs`

Generates the C# wrapper around player top-level code.

Inspect when changing:

- implicit `nb`
- implicit `args`
- generated namespace/class structure
- default imports
- entry-point behavior
- diagnostic line mapping affected by wrappers

---

## `ScriptRamAnalyzer.cs`

Roslyn semantic analysis used to calculate gameplay RAM cost.

Inspect whenever:

- adding script API methods
- changing RAM semantics
- changing includes
- altering semantic analysis behavior

---

## `ScriptRamResult.cs`

Result model for RAM analysis.

---

## `SourceSnapshot.cs`

Immutable snapshot of source used outside direct game-state access.

Important in game-thread/background Roslyn boundaries.

---

# 19. Script includes

Also under:

`NullByte.Scripting/Compilation/`

---

## `IncludeScanner.cs`

Identifies `#include` directives in source.

---

## `IncludeResolver.cs`

Resolves include graphs and included source files.

Start here for:

- recursive includes
- missing includes
- cycles
- duplicate includes
- depth behavior
- included-library validation

Include behavior also affects:

```text
ScriptCompiler.cs
ScriptRamAnalyzer.cs
ScriptLanguageService.cs
ServerSourceProvider.cs
```

Do not change include syntax or semantics in only one subsystem.

---

# 20. Script loading

Located under:

`NullByte.Scripting/Loading/`

---

## `ScriptAssembly.cs`

Represents the loaded compiled script assembly.

---

## `ScriptAssemblyLoader.cs`

Loads compiled Roslyn output.

Inspect when changing script instance creation or assembly lifecycle.

---

## `ScriptLoadContext.cs`

Collectible assembly load context.

Important for unloading generated player assemblies and avoiding long-term retention of compiled script assemblies.

Changes here should be tested for unload behavior.

---

# 21. Script runtime

Located under:

`NullByte.Scripting/Runtime/`

---

## `ScriptService.cs`

High-level orchestration for running scripts.

One of the primary scripting entry points.

Start here for changes to:

- run/exec behavior
- compiling before execution
- RAM validation
- process startup
- process restoration
- script lookup

---

## `ScriptRunner.cs`

Executes a compiled script against an `INullByteApi`.

Inspect for changes to execution lifecycle and argument handling.

---

## `ScriptProcess.cs`

Runtime model for a running script process.

---

## `ScriptProcessManager.cs`

Owns/tracks running script processes.

Start here for:

- PID handling
- process lookup
- process lifetime
- kill behavior
- per-server processes
- RAM accounting associated with running processes

---

## `ScriptStartResult.cs`

Result of attempting to start a script.

---

## `ScriptRestoreResult.cs`

Result of restoring a persisted script process.

---

## `ServerSourceProvider.cs`

Provides script/include source from a server's virtual filesystem.

Important for compilation of server-hosted source and include resolution.

---

# 22. Script language service

Located under:

`NullByte.Scripting/LanguageService/`

---

## `ScriptLanguageService.cs`

Roslyn-backed editor language service.

Start here for:

- diagnostics
- completion
- hover
- signature help
- definition lookup

Language-service behavior should remain consistent with actual compilation.

When changing script compilation semantics, determine whether this file needs a corresponding change.

---

# 23. NullByte.Shell

This project owns reusable game-session and persistence lifecycle behavior above Core.

---

## `GameSession.cs`

Represents/runs an active game session and assembles important runtime components.

A major composition/lifecycle file.

Inspect when changing:

- creation of a new game
- session replacement
- game loop setup
- lifecycle of simulation services
- session options
- composition of Core and Scripting runtime services

---

# 24. Shell persistence

Located under:

`NullByte.Shell/Persistence/`

---

## `SaveManager.cs`

High-level save/load orchestration.

Start here for manual save/load behavior.

---

## `AutosaveService.cs`

Autosave lifecycle and behavior.

Inspect for reset/new-game changes involving autosave cleanup.

---

## `ISaveStore.cs`

Abstraction over save storage.

---

## `FileSaveStore.cs`

Filesystem-backed save storage.

The host filesystem is appropriate here because these are application save files, not the game's virtual filesystem.

Do not confuse save storage with in-game files.

---

# 25. NullByte.ConsoleUI

Current executable/interactive host.

Use this project for terminal presentation, commands and application startup.

Do not place reusable domain behavior here.

---

# 26. Console application startup

---

## `Program.cs`

Process entry point and top-level composition.

Start here for:

- CLI arguments
- application startup
- constructing save/session infrastructure
- optional editor startup
- shutdown order

Avoid putting ordinary gameplay logic in `Program.cs`.

---

## `GameHost.cs`

Console-host lifecycle around the active game/session.

An important file for:

- startup
- session replacement
- reset
- load
- shutdown
- coordination with commands/editor integration

Inspect together with `GameSession.cs` for lifecycle work.

---

## `LoadRequest.cs`

Request object used for load/session replacement flow.

---

## `ShutdownRequest.cs`

Coordinates requested application shutdown.

---

## `NanoEditor.cs`

Built-in simple editor for game files.

Changes to editing behavior may need comparison with VS Code integration to avoid divergent filesystem semantics.

---

# 27. Console command infrastructure

Located under:

`NullByte.ConsoleUI/Commands/`

---

## `CommandDefinition.cs`

Description/definition of a command.

---

## `CommandRegistry.cs`

Registry of available commands.

---

## `CommandDispatcher.cs`

Parses/dispatches command execution through registered definitions.

---

## `CommandSetup.cs`

Central command composition/registration.

Start here when adding a new command to determine how command groups are wired.

---

## `ICommandGroup.cs`

Contract/pattern used by grouped commands.

---

## `ActivityFormatter.cs`

Console formatting for activity information.

Keep presentation-specific activity formatting here rather than in Core.

---

# 28. Console command groups

Located under:

`NullByte.ConsoleUI/Commands/Groups/`

---

## `ActionCommands.cs`

Commands for direct game actions.

Inspect together with `ServerActions.cs`.

---

## `ActivityCommands.cs`

Player activity commands.

Related argument parsing:

`ActivityArguments.cs`

Inspect together with Core activity service/domain files.

---

## `BotnetCommands.cs`

Botnet CLI commands.

Inspect together with `BotnetService.cs`.

---

## `DebugCommands.cs`

Development/debugging commands.

Use this for developer-facing diagnostic commands that do not belong in the normal game interface.

---

## `FileCommands.cs`

Virtual-filesystem commands.

Inspect together with:

```text
VirtualFileSystem.cs
VirtualPath.cs
WorkingDirectory.cs
```

---

## `NetworkCommands.cs`

Network/server commands.

Inspect together with `NetworkService.cs`.

---

## `PersistenceCommands.cs`

Save/load/reset-related user commands.

Inspect together with:

```text
GameHost.cs
SaveManager.cs
AutosaveService.cs
```

---

## `PlayerCommands.cs`

Commands displaying or manipulating player-facing information.

---

## `ScriptCommands.cs`

Console commands for script execution/process interaction.

Related argument parsing:

`RunArguments.cs`

Inspect together with:

```text
ScriptService.cs
ScriptProcessManager.cs
```

---

## `SystemCommands.cs`

Host/system-level console commands.

---

# 29. Console terminal infrastructure

Located under:

`NullByte.ConsoleUI/Terminal/`

---

## `IGameTerminal.cs`

Terminal abstraction.

Useful for command testing without a real console.

---

## `ThreadSafeConsoleTerminal.cs`

Production console terminal implementation.

Handles terminal output/input interaction and synchronization.

Do not interpret its thread safety as permission to access game state from arbitrary threads.

---

## `CommandLineParser.cs`

Console command-line parsing.

---

## Completion subsystem

Important files:

```text
ArgumentCompletion.cs
CompletionCatalog.cs
CompletionEngine.cs
CompletionKind.cs
CompletionResult.cs
CompletionSnapshot.cs
ICompletionProvider.cs
```

Inspect these for terminal auto-completion features.

---

## `InputHistory.cs`

Command/input history.

---

## `Hotkey.cs`

Hotkey representation/handling.

---

## `LivePanel.cs`

Live terminal panel functionality.

---

## `PanelFormatter.cs`

Formatting for panel output.

---

# 30. NullByte.Editor

C# host-side editor server.

This project bridges external editor requests to the current game.

---

## `EditorServer.cs`

Central editor-server lifecycle.

Start here for:

- server startup
- WebSocket endpoints
- editor connections
- event broadcasting
- watched game state
- reset/change notifications
- editor protocol wiring

Pay particular attention to event subscriptions when game state is replaced.

---

## `EditorProtocol.cs`

Protocol-level request/response definitions and dispatch behavior.

Inspect whenever adding or changing an editor operation.

Protocol changes usually require corresponding TypeScript changes in `NullByte.vscode`.

---

## `EditorFileService.cs`

Implements editor-facing virtual filesystem operations.

This is an adapter over the game's virtual filesystem.

Do not recreate filesystem semantics independently here.

---

## `EditorConnection.cs`

Represents an active editor connection.

Inspect for connection lifecycle and message transport behavior.

---

## `EditorConnectionFile.cs`

Handles the local connection-information file used for extension discovery/connection.

---

## `EditorToken.cs`

Authentication token used for local editor connections.

---

# 31. NullByte.vscode

TypeScript VS Code extension.

This project is not part of the .NET build.

Important configuration files:

```text
package.json
tsconfig.json
.vscode/launch.json
.vscode/tasks.json
```

---

# 32. VS Code extension entry point

## `src/extension.ts`

VS Code extension activation/composition.

Start here when adding a new VS Code feature to determine how services and commands are registered.

---

# 33. VS Code editor client

## `src/client.ts`

WebSocket client for communication with `NullByte.Editor`.

Start here for:

- connection behavior
- authentication
- request/response transport
- protocol handling

Inspect together with the C# `EditorProtocol.cs`.

---

# 34. VS Code virtual filesystem

## `src/filesystem.ts`

VS Code `FileSystemProvider` implementation exposing NullByte's virtual filesystem.

Inspect together with:

```text
EditorFileService.cs
EditorProtocol.cs
VirtualFileSystem.cs
```

for filesystem integration changes.

---

## `src/paths.ts`

VS Code-side path handling/utilities.

Be careful that path rules remain compatible with the C# virtual path implementation.

---

# 35. VS Code language features

The extension contains separate modules for editor features.

---

## `src/diagnostics.ts`

Displays script diagnostics.

Server-side behavior originates from the language service/editor protocol.

---

## `src/completion.ts`

Code completion integration.

---

## `src/hover.ts`

Hover information.

---

## `src/signature.ts`

Signature help.

---

## `src/definition.ts`

Go-to-definition support.

For any of these features, inspect the corresponding C# language-service operation before changing the TypeScript consumer.

---

# 36. VS Code commands and status

## `src/commands.ts`

VS Code commands exposed by the extension.

Start here when adding actions such as running a script, querying RAM or other editor-triggered operations.

---

## `src/status.ts`

Extension status-bar/connection-state behavior.

Inspect for UI feedback about editor connectivity.

---

# 37. VS Code language configuration

## `syntaxes/nullbyte-script.tmLanguage.json`

TextMate syntax highlighting for `.cx` scripts.

---

## `language-configuration.json`

VS Code language editing configuration.

---

## `package.json`

Extension manifest.

Inspect when adding:

- commands
- configuration
- activation behavior
- contributed language features
- keyboard shortcuts
- extension metadata

A command implemented in TypeScript may also require a corresponding contribution in `package.json`.

---

# 38. Test projects

The repository contains dedicated test projects including:

```text
NullByte.Core.Tests
NullByte.Scripting.Tests
NullByte.ConsoleUI.Tests
NullByte.Editor.Tests
```

Use the tests associated with the subsystem being changed as part of code discovery.

Tests are useful documentation of intended behavior and should normally be inspected before changing an important implementation.

---

# 39. Core test map

`NullByte.Core.Tests` has substantial coverage grouped by architecture.

---

## Domain and filesystem

Important areas include tests for:

- `VirtualFileSystem`
- `VirtualPath`
- game/domain behavior

Shared support includes:

`NullByte/Core/Tests/FileSystemTestSupport.cs`

---

## Formulas

Located under:

`Formulas/`

Includes:

```text
ActivityFormulasTests.cs
BotnetFormulasTests.cs
HackingFormulasTests.cs
SkillFormulasTests.cs
```

Use these when modifying game calculations.

---

## Services

Located under:

`Services/`

Includes:

```text
ActivityServiceTests.cs
BotnetServiceTests.cs
NetworkServiceTests.cs
ServerActionsTests.cs
ServerActionsSimulationTests.cs
WorkingDirectoryTests.cs
```

---

## Simulation

Located under:

`Simulation/`

Includes:

```text
GameLoopTests.cs
GameLoopSystemTests.cs
GameLoopResilienceTests.cs
BotnetEarlyGameTests.cs
```

Use these for changes affecting game-loop behavior.

---

## Systems

Located under:

`Systems/`

Includes:

```text
ActivitySystemTests.cs
BotnetSystemTests.cs
```

---

## Threading

Located under:

`Threading/`

Important test:

`GameSynchronizationContextTests.cs`

---

## Time

Located under:

`Time/`

Includes:

```text
GameSchedulerTests.cs
RealGameClockTests.cs
```

---

## Persistence

Located under:

`Persistence/`

Includes:

```text
SaveGameTests.cs
SaveDirectoriesTests.cs
GoldenSaves.cs
```

Persistence changes should normally inspect these tests.

---

## Randomness

Located under:

`Randomness/`

Important test:

`SeededRandomSourceTests.cs`

---

## Script-facing Core API

Located under:

`Scripting/`

Includes:

```text
NullByteApiTests.cs
NullByteBotnetApiTests.cs
```

---

## World/loading

Located under:

`World/`

Includes:

```text
ActivityCatalogLoaderTests.cs
InitialGameStateFactoryIntegrationTests.cs
NetworkLoaderTests.cs
```

---

## Test helpers

Important support includes:

```text
Helpers/GameThreadHarness.cs
TestDoubles/SequenceRandomSource.cs
```

`GameThreadHarness` is particularly useful for testing behavior that must run under the real game-thread model.

---

# 40. Scripting tests

Located under:

`NullByte.Scripting.Tests/`

---

## Compilation tests

Located under:

`Compilation/`

Important files include:

```text
ScriptCompilerTests.cs
IncludeCompilationTests.cs
IncludeResolverTests.cs
IncludeRamTests.cs
LibraryOnItsOwnTests.cs
FakeSources.cs
```

Use these whenever changing compilation or include behavior.

---

## Runtime tests

Located under:

`Runtime/`

Includes:

```text
ScriptProcessManagerTests.cs
ScriptRunnerTests.cs
ScriptServiceTests.cs
ScriptServicePathTests.cs
```

---

## Loading tests

Located under:

`Loading/`

Includes:

```text
ScriptAssemblyLoaderTests.cs
ScriptUnloadTests.cs
```

`ScriptUnloadTests` is particularly relevant when modifying assembly/load-context lifetime.

---

## Language-service tests

Located under:

`LanguageService/`

Important test:

`IncludeLanguageServiceTests.cs`

---

## Acceptance tests

Located under:

`Acceptance/`

Important test:

`ScriptAcceptanceTests.cs`

Use acceptance tests when behavior spans compiler, loading and runtime rather than only one component.

---

# 41. ConsoleUI tests

Located under:

`NullByte.ConsoleUI.Tests/`

Important areas include:

```text
Commands/
Persistence/
Terminal/
```

Representative tests include:

```text
ActivityArgumentsTests.cs
ActivityCommandsTests.cs
ActivityFormatterTests.cs
BotnetCommandTests.cs
FileCommandsTests.cs
RunCommandTests.cs
ScriptCommandsPathTests.cs
Command/completion tests
InputHistoryTests.cs
PathCompletionTests.cs
```

For a console command change, inspect both the command implementation and its closest existing tests before designing the change.

---

# 42. Editor tests

Located under:

`NullByte.Editor.Tests/`

Important current tests include:

```text
EditorFileServiceTests.cs
EditorFolderTests.cs
```

Editor features touching filesystem presentation should normally be tested here and, where appropriate, at lower Core filesystem levels.

---

# 43. Common task-to-file map

Use this section as the first lookup when deciding what source to load for a development task.

---

## Change Hack/Grow/Weaken mechanics

Start with:

```text
NullByte.Core/Services/ServerActions.cs
NullByte.Core/Formulas/HackingFormulas.cs
NullByte.Core/Domain/Server.cs
```

Then inspect:

```text
ServerActionsTests.cs
HackingFormulasTests.cs
```

If scripts are affected, also inspect:

```text
INullByteApi.cs
NullByteApi.cs
```

---

## Add a new script API method

Start with:

```text
NullByte.Core/Scripting/INullByteApi.cs
NullByte.Core/Scripting/NullByteApi.cs
NullByte.Core/Scripting/RamCostAttribute.cs
```

Then inspect:

```text
NullByte.Scripting/Compilation/ScriptRamAnalyzer.cs
relevant NullByteApi tests
relevant compiler/RAM tests
```

If the method introduces new game functionality, first identify the Core service that should actually own the behavior.

---

## Change script compilation

Start with:

```text
ScriptCompiler.cs
ScriptSourceGenerator.cs
IScriptCompiler.cs
CompiledScript.cs
ScriptCompilationResult.cs
```

Then inspect relevant compilation tests.

Add include files if includes are involved.

---

## Change `#include`

Start with:

```text
IncludeScanner.cs
IncludeResolver.cs
ScriptCompiler.cs
ScriptRamAnalyzer.cs
SourceSnapshot.cs
ServerSourceProvider.cs
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

---

## Change script execution/processes

Start with:

```text
ScriptService.cs
ScriptRunner.cs
ScriptProcess.cs
ScriptProcessManager.cs
```

Then inspect:

```text
IScriptOperations.cs
NullByteApiFactory.cs
ScriptProcessManagerTests.cs
ScriptRunnerTests.cs
ScriptServiceTests.cs
```

---

## Change virtual filesystem behavior

Start with:

```text
VirtualFileSystem.cs
VirtualPath.cs
```

Then determine which adapters are affected:

```text
FileCommands.cs
NullByteFilesApi.cs
EditorFileService.cs
filesystem.ts
```

Always inspect filesystem/path tests.

---

## Change console working-directory behavior

Start with:

```text
WorkingDirectory.cs
PathResolver.cs
FileCommands.cs
```

Then inspect:

```text
WorkingDirectoryTests.cs
PathCompletionTests.cs
ScriptCommandsPathTests.cs
```

Remember that script/editor paths deliberately have different semantics.

---

## Add a console command

Start with:

```text
relevant Commands/Groups/*Commands.cs
CommandSetup.cs
CommandDefinition.cs
CommandRegistry.cs
CommandDispatcher.cs
```

Then identify the Core/Scripting service that owns the actual operation.

---

## Change game-loop/threading behavior

Start with:

```text
GameLoop.cs
IGameThread.cs
GameSynchronizationContext.cs
GameScheduler.cs
IGameClock.cs
```

Also inspect:

```text
GameLoopTests.cs
GameLoopSystemTests.cs
GameSynchronizationContextTests.cs
GameSchedulerTests.cs
GameThreadHarness.cs
```

Treat this as a high-impact architectural change.

---

## Add or change an activity

Start with:

```text
Domain/Activities/*
ActivityService.cs
ActivitySystem.cs
ActivityFormulas.cs
ActivityRates.cs
activities.json
```

Then inspect corresponding tests.

---

## Change botnet mechanics

Start with:

```text
Botnet.cs
BotnetNode.cs
BotnetService.cs
BotnetSystem.cs
BotnetFormulas.cs
```

Then inspect:

```text
BotnetServiceTests.cs
BotnetSystemTests.cs
BotnetFormulasTests.cs
BotnetEarlyGameTests.cs
```

---

## Change save format

Start with:

```text
SaveGame.cs
SaveGameMapper.cs
SaveGameSerializer.cs
SaveMigration.cs
Persistence/Migrations/*
```

Then inspect:

```text
SaveGameTests.cs
SaveDirectoriesTests.cs
GoldenSaves.cs
```

Also inspect any domain types being persisted.

---

## Change save/load commands

Start with:

```text
PersistenceCommands.cs
GameHost.cs
GameSession.cs
SaveManager.cs
AutosaveService.cs
```

---

## Change reset/new-game behavior

Start with:

```text
GameHost.cs
GameSession.cs
PersistenceCommands.cs
AutosaveService.cs
SaveManager.cs
EditorServer.cs
```

Important concerns:

```text
session replacement
process shutdown
autosave deletion
editor reattachment/reset notification
stale event subscriptions
```

---

## Change application startup/configuration

Start with:

```text
Program.cs
GameHost.cs
GameSession.cs
```

Then locate the current settings/argument implementation before proposing APIs.

Do not infer configuration architecture solely from the Game Design Document.

---

## Change VS Code connectivity

C# side:

```text
EditorServer.cs
EditorConnection.cs
EditorConnectionFile.cs
EditorToken.cs
EditorProtocol.cs
```

TypeScript side:

```text
src/client.ts
src/extension.ts
src/status.ts
```

Also inspect `package.json` when extension configuration or commands are involved.

---

## Add an editor protocol operation

Start with:

```text
NullByte.Editor/EditorProtocol.cs
NullByte.Editor/EditorServer.cs
```

Then inspect:

```text
NullByte.vscode/src/client.ts
```

and the TypeScript feature module consuming the operation.

If game state is accessed, verify the operation crosses the game-thread boundary correctly.

---

## Add a VS Code command

Start with:

```text
src/commands.ts
src/extension.ts
package.json
```

If the command talks to the game, also inspect:

```text
src/client.ts
EditorProtocol.cs
EditorServer.cs
```

---

## Change VS Code diagnostics

Start with:

```text
ScriptLanguageService.cs
EditorProtocol.cs
src/diagnostics.ts
```

Likely supporting compilation files:

```text
ScriptDiagnostic.cs
ScriptCompiler.cs
ScriptSourceGenerator.cs
```

---

## Change completion/hover/signature/definition

Start with:

```text
ScriptLanguageService.cs
EditorProtocol.cs
```

plus the corresponding TypeScript file:

```text
completion.ts
hover.ts
signature.ts
definition.ts
```

Include compiler/include infrastructure if the problem involves cross-file semantics.

---

# 44. How to use this map during AI-assisted development

For a new task, do not load the entire repository by default.

Use this process:

### Step 1: Identify the subsystem

Use the task map above.

### Step 2: Load the primary files

Usually 2-5 files.

### Step 3: Load contracts and direct collaborators

Usually another 2-5 files.

### Step 4: Load relevant tests

Tests often reveal intended behavior more efficiently than additional implementation files.

### Step 5: Design the change

Only after the current implementation has been examined.

### Step 6: Expand context if required

If an implementation references an important unknown collaborator, load that collaborator.

Do not pre-emptively load unrelated projects.

---

# 45. Core reading sets

For recurring work, these are useful compact context bundles.

---

## Simulation bundle

```text
GameState.cs
GameLoop.cs
IGameThread.cs
GameSynchronizationContext.cs
GameScheduler.cs
IGameClock.cs
```

---

## Hacking bundle

```text
Server.cs
Player.cs
ServerActions.cs
HackingFormulas.cs
NullByteApi.cs
```

---

## Filesystem bundle

```text
VirtualFileSystem.cs
VirtualPath.cs
PathResolver.cs
WorkingDirectory.cs
```

Add the appropriate adapter:

```text
FileCommands.cs
NullByteFilesApi.cs
EditorFileService.cs
```

---

## Scripting compiler bundle

```text
IScript.cs
ScriptCompiler.cs
ScriptSourceGenerator.cs
CompiledScript.cs
ScriptCompilationResult.cs
ScriptRamAnalyzer.cs
```

---

## Include bundle

```text
IncludeScanner.cs
IncludeResolver.cs
SourceSnapshot.cs
ServerSourceProvider.cs
ScriptCompiler.cs
ScriptRamAnalyzer.cs
ScriptLanguageService.cs
```

---

## Script runtime bundle

```text
INullByteApi.cs
NullByteApi.cs
ScriptService.cs
ScriptRunner.cs
ScriptProcess.cs
ScriptProcessManager.cs
```

---

## Persistence bundle

```text
SaveGame.cs
SaveGameMapper.cs
SaveGameSerializer.cs
SaveMigration.cs
SaveManager.cs
AutosaveService.cs
```

---

## Session lifecycle bundle

```text
Program.cs
GameHost.cs
GameSession.cs
SaveManager.cs
AutosaveService.cs
EditorServer.cs
```

---

## Editor server bundle

```text
EditorServer.cs
EditorProtocol.cs
EditorConnection.cs
EditorFileService.cs
ScriptLanguageService.cs
IGameThread.cs
```

---

## VS Code extension bundle

```text
extension.ts
client.ts
commands.ts
filesystem.ts
status.ts
package.json
```

Add the relevant language-feature module when needed.

---

# 46. Context priority

When context space is limited, load information in this order:

1. `AI_CONTEXT.md`
2. Relevant portion of the Game Design Document
3. This codebase map
4. Primary implementation files
5. Relevant interfaces/contracts
6. Relevant tests
7. Direct dependencies discovered while reading the code
8. Additional implementation only when required

Do not sacrifice the current version of a file being modified in order to include unrelated background material.

For implementation work:

> Current source code beats remembered source code.

---

# 47. Maintenance

Update this document when:

- projects are added/removed
- major folders are reorganized
- important architectural entry points change
- a subsystem gains a new primary abstraction
- the recommended starting point for a common task changes

Do not update it for every new helper class.

This file should remain a compact navigation aid rather than becoming a complete API reference.

If keeping it current becomes expensive, reduce detail rather than abandoning it.