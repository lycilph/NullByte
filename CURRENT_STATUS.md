# NullByte Current Status

This document records the current development state of NullByte.

It is intended to answer:

- What is implemented?
- What is currently being worked on?
- What comes next?
- Which known issues or decisions need attention?

Update this document after completing a meaningful deliverable or when development focus changes.

For stable architectural guidance, read `AI_CONTEXT.md`.

For repository navigation, read `CODEBASE.md`.

For intended behavior and milestone definitions, read the Game Design Document.

The source code remains authoritative for implementation status.

---

# Current focus

## Active milestone

**M11: Visual Studio Code editor integration improvements**

The foundation for VS Code integration is implemented.

The current focus is extending that integration with development and gameplay operations beyond virtual-file editing and language-service functionality.

---

# Current M11 status

## M11.1: Show connected external editors

**Status: In progress**

### Goal

Add a debug command that reports all currently connected external editor clients.

### Intended behavior

The command should:

- list every active editor connection
- show useful connection information
- report clearly when no editors are connected
- use the existing debug-command infrastructure
- read connection state from the editor server rather than maintaining a second connection registry

### Current implementation state

The repository contains:

- `EditorServer`
- `EditorConnection`
- an active connection collection in the editor server
- `DebugCommands`
- debug-command registration infrastructure

The current `DebugCommands` implementation does not yet include a command for listing editor connections.

### Current implementation issue

Command registration and composition were being extended to supply editor connection information to the debug command.

The last observed issue was that an injected delegate/factory intended to provide editor connection information was `null` when commands were being registered.

Before continuing, inspect the current versions of:

```text
NullByte.ConsoleUI/Commands/Groups/DebugCommands.cs
NullByte.ConsoleUI/Commands/CommandSetup.cs
NullByte.ConsoleUI/GameHost.cs
NullByte.Editor/EditorServer.cs
NullByte.Editor/EditorConnection.cs
```

The target design should avoid making command groups depend directly on editor transport internals where a small read-only connection-information abstraction or provider is sufficient.

Do not continue from an assumed API. Re-evaluate the exact current command-registration and host-composition code first.

---

## M11.2: Full-range diagnostic underlines

**Status: Planned**

### Goal

Make editor diagnostics underline the complete relevant source range rather than only a partial location.

### Likely areas

```text
NullByte.Scripting/Compilation/ScriptDiagnostic.cs
NullByte.Scripting/Compilation/ScriptCompiler.cs
NullByte.Scripting/LanguageService/ScriptLanguageService.cs
NullByte.Editor/EditorProtocol.cs
NullByte.vscode/src/diagnostics.ts
```

### Important considerations

- preserve correct line and column mapping through generated script wrappers
- preserve correct locations for included files
- verify Roslyn start/end positions on both the C# and TypeScript sides
- ensure the VS Code diagnostic range uses the same coordinate convention as the server response

---

## M11.3: Run current script from VS Code

**Status: Planned**

### Goal

Allow the developer/player to execute the currently open `.cx` file from VS Code.

### Likely areas

C#:

```text
NullByte.Editor/EditorProtocol.cs
NullByte.Editor/EditorServer.cs
NullByte.Scripting/Runtime/ScriptService.cs
```

TypeScript:

```text
NullByte.vscode/src/commands.ts
NullByte.vscode/src/client.ts
NullByte.vscode/src/extension.ts
NullByte.vscode/package.json
```

### Important considerations

- determine the server and script path from the virtual document URI
- execute through the existing `ScriptService`
- marshal game-state access onto the game thread
- return useful compilation/start diagnostics
- do not create a second script-execution path specifically for VS Code
- define how script arguments and thread count are supplied, if supported in the first version

---

## M11.4: Get script RAM from VS Code

**Status: Planned**

### Goal

Allow VS Code to request and display the static RAM cost of the current `.cx` script.

### Existing capability

`ScriptService` already contains script-RAM measurement behavior.

The editor integration should call the existing scripting operation rather than reproducing Roslyn RAM analysis.

### Likely areas

C#:

```text
NullByte.Editor/EditorProtocol.cs
NullByte.Editor/EditorServer.cs
NullByte.Scripting/Runtime/ScriptService.cs
NullByte.Scripting/Compilation/ScriptRamAnalyzer.cs
```

TypeScript:

```text
NullByte.vscode/src/commands.ts
NullByte.vscode/src/client.ts
NullByte.vscode/src/extension.ts
NullByte.vscode/package.json
```

### Important considerations

- resolve the virtual file to the correct in-game server and path
- preserve include behavior
- return compiler diagnostics when RAM cannot be calculated
- show the result through an appropriate VS Code notification or output surface
- do not report `0` as though it were a valid measured result when compilation failed

---

## M11.5: Launch VS Code from the game

**Status: Planned**

### Goal

Provide a game command that launches VS Code and opens/connects to the current NullByte workspace.

### Likely areas

```text
NullByte.ConsoleUI/Commands/Groups/SystemCommands.cs
NullByte.ConsoleUI/Commands/CommandSetup.cs
NullByte.ConsoleUI/GameHost.cs
NullByte.Editor/EditorConnectionFile.cs
NullByte.vscode/
```

### Decisions still required

- exact console command name and syntax
- whether the command invokes `code` directly
- how the extension receives or discovers connection information
- what should happen when VS Code or the extension is not installed
- whether an already-running VS Code instance should be reused
- whether this is supported on Windows only initially

Because launching an external process is host behavior, it must not be added to `NullByte.Core`.

---

# Implemented foundation

The following areas are represented in the current repository and form the foundation for ongoing development.

---

## Game simulation

**Status: Implemented**

Includes:

- single-threaded game simulation
- custom game synchronization context
- game clock abstraction
- game scheduler
- simulation systems
- deterministic and accelerated test support
- resilience tests for the game loop

Key files:

```text
NullByte.Core/Simulation/GameLoop.cs
NullByte.Core/Simulation/IGameThread.cs
NullByte.Core/Threading/GameSynchronizationContext.cs
NullByte.Core/Time/IGameClock.cs
NullByte.Core/Time/RealGameClock.cs
NullByte.Core/Time/FakeGameClock.cs
NullByte.Core/Time/GameScheduler.cs
```

---

## Network and hacking gameplay

**Status: Implemented**

Includes:

- servers and network
- player server discovery
- Hack/Grow/Weaken-style operations
- network analysis
- formulas for hacking and network behavior
- skill progression
- tests for formulas and server actions

Key files:

```text
NullByte.Core/Domain/Network.cs
NullByte.Core/Domain/Server.cs
NullByte.Core/Services/NetworkService.cs
NullByte.Core/Services/ServerActions.cs
NullByte.Core/Formulas/HackingFormulas.cs
NullByte.Core/Formulas/NetworkFormulas.cs
NullByte.Core/Formulas/SkillFormulas.cs
```

---

## Console interface

**Status: Implemented and actively extended**

Includes:

- command registry and dispatcher
- command groups
- terminal abstraction
- input history
- auto-completion
- live panels
- built-in file editor
- debug commands

Key files:

```text
NullByte.ConsoleUI/Commands/
NullByte.ConsoleUI/Terminal/
NullByte.ConsoleUI/NanoEditor.cs
```

---

## Virtual filesystem

**Status: Implemented**

Includes:

- per-server virtual files
- directories
- path normalization and validation
- working-directory support for console commands
- change notifications
- persistence
- editor filesystem integration
- tests for paths and filesystem behavior

Key files:

```text
NullByte.Core/Domain/VirtualFileSystem.cs
NullByte.Core/Domain/VirtualPath.cs
NullByte.Core/Domain/PathResolver.cs
NullByte.Core/Services/WorkingDirectory.cs
```

---

## Roslyn scripting

**Status: Implemented**

Includes:

- `.cx` scripts
- top-level player code
- generated script wrapper
- implicit `nb` and `args`
- Roslyn diagnostics
- in-memory compilation
- collectible assembly loading
- script execution
- runtime script API

Key files:

```text
NullByte.Scripting/Compilation/ScriptCompiler.cs
NullByte.Scripting/Compilation/ScriptSourceGenerator.cs
NullByte.Scripting/Loading/ScriptAssemblyLoader.cs
NullByte.Scripting/Loading/ScriptLoadContext.cs
NullByte.Scripting/Runtime/ScriptRunner.cs
NullByte.Core/Scripting/INullByteApi.cs
NullByte.Core/Scripting/NullByteApi.cs
```

---

## Script processes and RAM

**Status: Implemented**

Includes:

- script processes
- process IDs
- per-host process tracking
- arguments
- thread count
- process state
- cancellation/kill behavior
- static RAM analysis
- RAM costs on script API methods
- server RAM enforcement

Key files:

```text
NullByte.Scripting/Runtime/ScriptProcess.cs
NullByte.Scripting/Runtime/ScriptProcessManager.cs
NullByte.Scripting/Runtime/ScriptService.cs
NullByte.Scripting/Compilation/ScriptRamAnalyzer.cs
NullByte.Core/Scripting/RamCostAttribute.cs
```

---

## Script includes and libraries

**Status: Implemented**

Includes:

- `#include "path"` syntax
- recursive includes
- missing-include diagnostics
- include cycle detection
- include depth limits
- duplicate handling
- validation of library source
- RAM analysis across included files
- language-service support
- tests for compilation, resolution, RAM and editor behavior

Key files:

```text
NullByte.Scripting/Compilation/IncludeScanner.cs
NullByte.Scripting/Compilation/IncludeResolver.cs
NullByte.Scripting/Compilation/SourceSnapshot.cs
NullByte.Scripting/Runtime/ServerSourceProvider.cs
```

Relevant tests:

```text
IncludeCompilationTests.cs
IncludeResolverTests.cs
IncludeRamTests.cs
IncludeLanguageServiceTests.cs
LibraryOnItsOwnTests.cs
```

---

## Botnet

**Status: Implemented**

Includes:

- botnet state
- nodes
- upgrades
- purchase behavior
- passive simulation
- formulas
- console commands
- script API
- tests including early-game behavior

Key files:

```text
NullByte.Core/Domain/Botnet.cs
NullByte.Core/Domain/BotnetNode.cs
NullByte.Core/Services/BotnetService.cs
NullByte.Core/Systems/BotnetSystem.cs
NullByte.Core/Formulas/BotnetFormulas.cs
NullByte.ConsoleUI/Commands/Groups/BotnetCommands.cs
NullByte.Core/Scripting/NullByteBotnetApi.cs
```

---

## Activities

**Status: Implemented**

Includes:

- activity state and progress
- jobs
- employment and ranks
- training/study
- companies and facilities
- activity formulas and rates
- activity simulation system
- console commands and formatting
- data-driven activity catalog
- tests

Key files:

```text
NullByte.Core/Domain/Activities/
NullByte.Core/Services/ActivityService.cs
NullByte.Core/Systems/ActivitySystem.cs
NullByte.Core/Formulas/ActivityFormulas.cs
NullByte.Core/Formulas/ActivityRates.cs
NullByte.Core/Data/activities.json
NullByte.ConsoleUI/Commands/Groups/ActivityCommands.cs
```

---

## Persistence

**Status: Implemented**

Includes:

- save models
- serialization
- game-state mapping
- versioned save format
- migrations
- manual save/load commands
- autosave
- filesystem-backed save storage
- golden save tests

Current migrations include:

```text
V1ToV2Migration.cs
V2ToV3Migration.cs
```

Key files:

```text
NullByte.Core/Persistence/
NullByte.Shell/Persistence/
NullByte.ConsoleUI/Commands/Groups/PersistenceCommands.cs
```

---

## Game session and host lifecycle

**Status: Implemented and evolving**

Includes:

- game-session composition
- host startup
- game-loop lifecycle
- terminal lifecycle
- autosave lifecycle
- load/session replacement
- shutdown coordination
- optional editor-server startup

Key files:

```text
NullByte.Shell/GameSession.cs
NullByte.ConsoleUI/GameHost.cs
NullByte.ConsoleUI/Program.cs
NullByte.ConsoleUI/LoadRequest.cs
NullByte.ConsoleUI/ShutdownRequest.cs
```

This area is sensitive because it coordinates ownership and disposal across multiple subsystems.

---

## VS Code virtual filesystem integration

**Status: Implemented**

Includes:

- local WebSocket server
- authentication token
- connection discovery file
- editor protocol
- virtual filesystem provider
- file operations
- folder operations
- file-change notifications
- game-reset notifications
- server discovery updates
- connection-state UI

C# side:

```text
NullByte.Editor/
```

TypeScript side:

```text
NullByte.vscode/src/client.ts
NullByte.vscode/src/filesystem.ts
NullByte.vscode/src/status.ts
NullByte.vscode/src/extension.ts
```

---

## VS Code language features

**Status: Implemented**

Includes:

- diagnostics
- completion
- hover
- signature help
- definition lookup
- `.cx` syntax highlighting
- include-aware language-service behavior

C# side:

```text
NullByte.Scripting/LanguageService/ScriptLanguageService.cs
NullByte.Editor/EditorProtocol.cs
```

TypeScript side:

```text
NullByte.vscode/src/diagnostics.ts
NullByte.vscode/src/completion.ts
NullByte.vscode/src/hover.ts
NullByte.vscode/src/signature.ts
NullByte.vscode/src/definition.ts
```

M11 includes improvements and new commands on top of this foundation.

---

# Recently completed architectural work

The following work has been completed in the development sequence associated with this repository, but its exact presence should be verified against the current source snapshot before modifying it.

---

## True game reset

**Status: Completed in the latest working codebase**

The reset operation was designed as a true new-game operation.

Expected completed behavior includes:

- replacement of the active game/session
- rejection while an incompatible foreground activity is running
- stopping or replacing old runtime state
- autosave deletion
- editor notification when active game state changes
- tests using xUnit and NSubstitute where applicable

Before changing reset behavior, inspect the current versions of:

```text
GameHost.cs
GameSession.cs
PersistenceCommands.cs
AutosaveService.cs
EditorServer.cs
```

---

## Unified host request/mailbox concept

**Status: Completed in the latest working codebase**

The host's separate request concepts were reviewed and consolidated around a common mailbox/request model.

The purpose was to:

- represent host-directed requests consistently
- avoid adding one independent request type and polling mechanism per operation
- preserve single-threaded game-state ownership
- make reset/load/shutdown flow easier to extend

Before extending this mechanism, inspect its current implementation rather than recreating the earlier individual request pattern.

---

## Application settings architecture

**Status: Completed in the latest working codebase**

The completed target architecture includes:

- command-line parsing
- `settings.json`
- executable-directory settings location
- merging settings with CLI overrides
- centralized validation
- refactored `Program.cs`
- removal of obsolete parsing helpers
- writing/default settings-file support
- unit tests

Application settings remain separate from gameplay balance configuration.

The attached repository snapshot may not reflect every part of this later settings work. Use the latest local source as authoritative before modifying startup/configuration code.

---

# Known architectural constraints

These constraints are active and must be preserved unless explicitly reconsidered.

---

## Game-thread ownership

Mutable game state belongs to the game thread.

External operations must cross the game-thread boundary.

Do not solve ordinary state ownership by adding locks around domain objects.

---

## Shared game mechanics

Manual commands and script APIs use the same underlying Core behavior.

Do not implement separate versions of game rules for ConsoleUI, scripts or VS Code.

---

## Game time

Gameplay timing uses the game clock and scheduler.

Do not introduce blocking delays into simulation code.

---

## Virtual filesystem

Game files are not host operating-system files.

All in-game file consumers should reuse the Core virtual-filesystem rules.

---

## Script compilation

Compilation and language services must remain behaviorally aligned.

If compilation semantics change, inspect editor-language-service behavior.

---

## Editor protocol

Protocol changes normally require coordinated changes in both:

```text
NullByte.Editor
NullByte.vscode
```

---

## Persistence

Persistent-model changes require explicit consideration of versioning and migration.

---

# Known issues and open questions

## M11 connection-provider composition

The connection-debug command currently needs a clean way to obtain editor connection information.

The immediate issue is the observed null delegate/factory during command registration.

Resolve the composition/lifetime problem rather than using a global/static connection registry.

---

## Runtime/editor connection lifetime

Any connection-information abstraction must behave correctly when:

- no editor server was started
- the editor server starts after some host components
- connections are added or removed
- the game session is replaced
- the application shuts down

The debug command should read current data at execution time rather than capture a stale connection list.

---

## Repository snapshot freshness

The uploaded Repomix snapshot appears to contain the major game, scripting, editor and activity architecture.

However, some recently completed work, particularly the later reset, mailbox and settings changes, may exist only in the newer local working codebase.

Before implementation work, always use the current source files from the repository being modified.

---

# Next recommended development steps

## Immediate

1. Reopen the current M11.1 implementation files.
2. Trace command creation and registration from `GameHost` through `CommandSetup`.
3. Identify why the connection provider becomes null.
4. settle the smallest stable read-only connection-information boundary.
5. implement the editor-connection debug command.
6. manually verify zero, one and multiple connections.
7. add tests if the composition can be tested without excessive host scaffolding.
8. mark M11.1 complete here.

## After M11.1

Proceed with:

1. M11.2 full-range diagnostics
2. M11.3 run current script from VS Code
3. M11.4 get script RAM from VS Code
4. M11.5 launch VS Code from the game

For each deliverable:

- settle the protocol shape before implementation
- implement C# and TypeScript sides together
- preserve game-thread ownership
- reuse existing scripting operations
- update tests where practical
- update this file after completion

---

# Upcoming milestone

## M12: Sandbox and safety

**Status: Not started**

Expected work includes stronger restrictions and safety around player scripts.

Likely areas include:

- restricted references
- semantic analyzers
- blocking API prevention
- dangerous namespace/API restrictions
- synchronous infinite-loop protection
- runtime containment behavior
- tests for prohibited script behavior

M12 should begin only after the current M11 integration improvements are complete or deliberately deferred.

---

# Validation baseline

Before declaring a change complete:

## .NET

- solution compiles
- all applicable xUnit tests pass
- no new nullable warnings are introduced without justification
- game-thread invariants are preserved
- persisted-data impact has been considered

## VS Code extension

- TypeScript compiles
- extension development host starts
- connection status behaves correctly
- affected commands/features work against a running game
- C# and TypeScript protocol payloads agree

## Script behavior

Where applicable:

- normal script compilation succeeds
- diagnostics remain correctly mapped
- includes still work
- RAM analysis remains consistent
- running scripts release process/RAM resources correctly

---

# Status update template

Use this template when completing a deliverable:

```text
## M11.x: Deliverable name

Status: Completed

Completed:
- concrete outcome
- concrete outcome
- tests or manual verification

Key files changed:
- path
- path

Decisions:
- important architectural decision

Follow-up:
- remaining optional or deferred work
```

Update the active focus and next steps at the same time.

---

# Last updated

**2026-11-24**

Current recorded focus:

**M11.1: Debug command for connected external editors**