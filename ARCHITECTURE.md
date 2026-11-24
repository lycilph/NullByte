# NullByte Architecture

Technical architecture and major system boundaries. See `CODEBASE.md` for files and `DESIGN.md` for gameplay semantics.

## 1. Layers and projects

```text
Presentation        ConsoleUI / VS Code
Integration/host    GameHost / Shell / Editor
Scripting           compiler / runtime / language service
Application logic   services / systems / formulas
Domain              GameState / Player / Network / VFS / Activities
```

Dependencies flow downward. `NullByte.Core` does not depend on presentation/editor infrastructure.

Projects:

- `NullByte.Core`: domain, services, formulas, simulation, time, VFS, persistence models, script API.
- `NullByte.Scripting`: Roslyn compilation/includes/RAM, loading, processes, language services.
- `NullByte.Shell`: session and persistence lifecycle.
- `NullByte.ConsoleUI`: process startup, configuration, commands and terminal host.
- `NullByte.Editor`: optional external-editor server/protocol.
- `NullByte.vscode`: TypeScript client.

## 2. Runtime composition

```text
Program
  |
GameHost
  +-- HostRequestMailbox
  +-- GameSession
  |     +-- GameState
  |     +-- GameLoop / Clock / Scheduler
  |     +-- Services / Systems
  |     +-- Script runtime
  +-- Commands / completion / autosave
  +-- optional EditorServer
```

`Program` owns process startup. `GameHost` owns application-level session transitions. `GameSession` owns one playable simulation.

## 3. Game-thread model

Mutable game state has one owner: the game thread. Console/editor/background callers cross `IGameThread` or the appropriate host boundary. Locks are not the domain consistency model.

Long actions suspend while simulation continues. `GameSynchronizationContext`, `GameScheduler`, `IGameClock` and `GameLoop` coordinate awaited continuations. Gameplay uses game time; wall-clock time is used only when semantically appropriate, such as file metadata.

Passive mechanics use `ISimulationSystem`; current systems include activities and Botnet.

## 4. Application logic

Services coordinate gameplay operations; formulas contain deterministic calculations; systems advance continuous mechanics.

Manual and scripted behavior converge on shared Core services:

```text
Console command --+
                  +--> Core service/formula --> GameState
Script API -------+
```

## 5. Activities

Activities use one lifecycle for study/training, jobs and crimes. `ActivityService` owns transitions, `ActivitySystem` advances them, formulas calculate rates/rewards, and domain activity types hold state.

Activities can be focused or background. Crimes are timed attempts that may repeat; each attempt rolls success and applies rewards. They use the existing activity mode, simulation, event and persistence model rather than a parallel subsystem.

## 6. Virtual filesystem

`VirtualFileSystem` is authoritative for in-game files/folders; `VirtualPath` owns canonical path rules.

- Console paths may resolve against current directory.
- Script paths are server-root-relative.
- Editor wire paths include server plus virtual path.

Filesystem consumers adapt these contexts instead of implementing separate filesystem rules.

## 7. Scripting

```text
.cx source
  -> include resolution / source snapshot
  -> generated wrapper
  -> Roslyn compilation
       + diagnostics
       + semantic RAM analysis
  -> emitted assembly / collectible load context
  -> IScript
  -> ScriptRunner
  -> INullByteApi
  -> Core services
```

`ScriptService` implements `IScriptService` and coordinates compile/start/restore/RAM measurement. `ScriptProcessManager` owns process tracking; `ScriptRunner` executes compiled scripts.

Included source affects compilation, diagnostics, RAM and language services. Background Roslyn work consumes immutable source snapshots rather than live game state.

## 8. Editor architecture

```text
VS Code extension
      |
WebSocket / JSON
      |
NullByte.Editor
      |
game-thread boundary
      |
GameSession / VFS / ScriptService
```

Protocol changes are coordinated across C# and TypeScript. File operations use the authoritative VFS. Language work snapshots required sources and runs Roslyn off-thread.

`EditorServer` tracks live connections and exposes a narrow read-only `IExternalEditorStatus` contract to ConsoleUI. This lets debug commands report editor state without depending on WebSocket internals.

Editor Run and RAM operations reuse `IScriptService`; editor tooling does not implement separate script execution or RAM behavior. The VS Code extension provides commands and a Run CodeLens over `.cx` files.

## 9. Host lifecycle

`GameHost` is the only component that replaces/shuts down the active session. Commands submit one of the closed `HostRequest` variants (`Shutdown`, `Load`, `Reset`) to a host-owned atomic single-slot `HostRequestMailbox`.

The host processes the request after the current command returns, preventing a session from disposing itself from its game thread. A second pending request is rejected rather than queued.

Reset creates a fresh session, removes autosave/backup and preserves named saves. Load validates before replacement. Successful session replacement notifies observers such as Editor; stale subscriptions must be detached.

## 10. Persistence

```text
GameState
  -> SaveGameMapper
  -> versioned SaveGame
  -> SaveGameSerializer
  -> SaveManager
  -> ISaveStore
```

Save schemas evolve through explicit migrations. The current code includes migrations through V3->V4. Activities, including crime state where applicable, participate in persistence. Autosave uses the same persistence gate so reset deletion cannot race a save.

## 11. Application configuration

Application configuration is owned by ConsoleUI and is separate from gameplay balance.

```text
defaults -> optional settings.json -> CLI overrides -> validation -> ApplicationOptions
```

`settings.json` lives beside the executable. Settings are immutable during a run. `Program` maps them to focused runtime options (`FileSaveStore`, `BalanceConfig`, editor options). `--new` and `--write-default-settings` are launch instructions, not persistent settings.

## 12. Testing architecture

Use subsystem-level tests: pure formulas, services/state transitions, fake-clock simulation, deterministic randomness, real Roslyn compilation, script acceptance tests, editor integration tests and save migration/golden tests.

High-value architectural tests cover game-thread behavior, host mailbox/reset, configuration precedence/validation, activity persistence, include semantics and editor-to-script-service integration.

## 13. Extension patterns

- Gameplay: domain + formula/service + `ISimulationSystem` when continuous.
- Script API: existing Core behavior -> API contract/implementation -> RAM metadata/tests.
- Editor operation: existing behavior -> `EditorProtocol` -> `client.ts` -> VS Code feature.
- Host transition: new `HostRequest` variant -> `GameHost` handler.
- Long-running player action: extend the activity model when its lifecycle fits.

## 14. Warning signs

Reconsider changes that introduce:

- Core -> presentation/editor dependencies
- locks around ordinary game state
- duplicated command/script rules
- background reads of mutable game state
- independent real timers for game simulation
- accidental save incompatibility
- different compiler/editor script semantics
- lifecycle polling flags outside `HostRequestMailbox`
- editor-specific script execution/RAM logic
- long-lived references to collectible generated assemblies

## 15. Principles

> Mutable game state has one owner thread.

> Manual and scripted actions share game mechanics.

> Long-running gameplay suspends without blocking simulation.

> Background work receives snapshots, not mutable state.

> Persistence evolves explicitly.

> Infrastructure adapts to the game domain.
