# NullByte Architecture

## Purpose

NullByte is layered around a shared Core game model and services. UI, scripts and external tools are adapters over that shared gameplay implementation.

This file is the canonical home for architectural rules. `DECISIONS.md` records why they exist, `CODEBASE.md` says where things live, and `DEVELOPMENT.md` describes the workflow.

## Dependency direction

The intended direction is broadly:

```text
ConsoleUI ─┐
Editor ────┼──> Shell / Scripting ──> Core
VS Code ───┘

Core
├── Domain
├── Services
├── Simulation / Systems
├── Time / Threading
├── Persistence models
└── Script API contracts
```

The exact project references are the source of truth; this diagram describes responsibility rather than every assembly reference.

## Game-thread ownership

Mutable game state has one owner: the game thread.

External callers — console commands, editor requests and script execution — must cross into the game-thread boundary before reading or changing mutable gameplay state.

This avoids pervasive locking and makes simulation ordering explicit.

**Rule:** do not add locks around Core state to solve a problem that is actually a game-thread ownership problem.

## Runtime composition

The runtime is composed roughly as:

```text
GameHost
  └── GameSession
       ├── GameState
       ├── Core services
       ├── Simulation systems
       ├── ScriptService
       └── Persistence services

Console commands ───────────────┘
External editor ────────────────┘
Scripts ────────────────────────┘
```

`GameHost` owns replacement of the current session. The session owns the live game state and its services.

The WPF `NullByte` project is currently a minimal standalone shell and is not part of the playable runtime composition.

## Core services and systems

Services implement operations over game state; systems advance recurring simulation.

Examples:

- `ServerActions` — server/gameplay actions.
- `NetworkService` — network behavior.
- `ActivityService` — activity lifecycle and attempts.
- `BotnetService` — botnet behavior.
- `ActivitySystem` — activity progression.
- `BotnetSystem` — botnet progression.

The distinction is useful when deciding where new behavior belongs: a user/request-driven operation generally belongs in a service; recurring time-driven progression belongs in a system.

## Time and scheduling

Game time is abstracted through `IGameClock` and scheduling through `GameScheduler`.

Long-running gameplay must not block the game thread with `Thread.Sleep`, `.Wait()`, `.Result`, or equivalent synchronous waits.

Use game-time scheduling so that:

- simulation remains responsive;
- tests can use `FakeGameClock`;
- long-running operations can suspend without tying up a thread.

Inject deterministic seams such as clocks and randomness where behavior needs reliable tests.

## Activities

Activities use a shared lifecycle for study/training, jobs and crimes.

An activity can run in:

- **Focused mode** — the console is blocked while the activity runs.
- **Background mode** — the console remains available, with a 20% reward penalty.

New long-running activities should normally reuse this lifecycle rather than inventing a separate scheduler or persistence mechanism.

## Virtual file system

The VFS is authoritative for player files and folders.

There are two important path contexts:

- Console commands can work with the current working directory.
- Scripts and editor operations use server-root-relative paths where appropriate.

Path handling should go through `VirtualPath` and `PathResolver` rather than ad-hoc string manipulation.

The VS Code extension exposes the same in-game folders/files through its virtual filesystem.

## Scripting architecture

The script pipeline is:

```text
source
  ↓
include resolution / SourceSnapshot
  ↓
generated script wrapper
  ↓
Roslyn compilation
  ↓
diagnostics + RAM analysis
  ↓
assembly
  ↓
ScriptRunner
  ↓
INullByteApi
  ↓
Core services
  ↓
GameState
```

`ScriptService` coordinates compilation, starting/restoring processes and RAM measurement. `ScriptProcessManager` tracks running processes.

The script API delegates gameplay behavior to Core services. It must not become a second implementation of the game's rules.

### Snapshot boundary

Roslyn/editor work should operate on immutable source snapshots rather than live mutable game state.

A running script keeps the compiled snapshot it started with. Editing a source file later affects future compilations, not the already-running process.

### Script sandbox and safety

Player scripts run as compiled .NET code inside the game process. The scripting layer applies practical safeguards at compilation and runtime, but it is not a hardened security boundary.

The safeguards include:

- allow-listed metadata references to limit which framework assemblies scripts can compile against;
- semantic diagnostics for selected APIs that could access the host environment;
- diagnostics for common synchronous blocking APIs;
- a syntactic check that rejects loops without an asynchronous yield in their loop body;
- runtime failure handling that records the process failure, releases reserved RAM, unloads the collectible script load context and isolates lifecycle observers from one another.

The intended threat model is accidental mistakes and common attempts to access the host environment in a hobby game. It does not attempt to contain a determined attacker.

Important limitations:

- The banned-API list and reference allow-list reduce exposure; they do not prove that every reachable API is safe.
- The loop check is syntactic. It does not prove that every path through a loop yields, nor does it prevent expensive but finite computations.
- Script cancellation is cooperative and takes effect at asynchronous yield points. Arbitrary synchronous code cannot be safely interrupted by the current runtime.
- There is no separate process boundary or hard execution-time limit.

Treat these measures as guardrails for ordinary gameplay scripts, not as a substitute for operating-system-level isolation when running untrusted code.

## External editor architecture

The VS Code extension communicates with the game through a WebSocket/JSON protocol:

```text
VS Code
  ↓
NullByte.vscode client
  ↓ WebSocket / JSON
NullByte.Editor
  ↓ game-thread boundary
GameSession / VFS / ScriptService
```

Editor operations reuse the same Core/Scripting behavior as the console.

Editor-specific code should primarily translate protocol requests/responses and editor concerns. It should not contain separate gameplay rules.

`EditorServer` exposes a narrow `IExternalEditorStatus` to ConsoleUI instead of making the console depend on the full editor implementation.

## Host lifecycle

The host owns session replacement.

Operations such as shutdown, load and reset are represented as host-level requests through `HostRequestMailbox`.

This avoids disposing/replacing the active session from inside the command currently executing against it.

When a session is replaced:

- old subscriptions must be detached;
- the new session becomes the sole active session;
- external-editor status is refreshed/notified as appropriate.

## Persistence

Persistence is deliberately separate from live runtime objects:

```text
GameState
  ↓
SaveGameMapper
  ↓
versioned SaveGame
  ↓
SaveGameSerializer
  ↓
SaveManager
  ↓
ISaveStore
```

Save files are versioned and upgraded through explicit migrations.

Live CLR execution state is reconstructed when loading rather than serialized as running objects.

Migrations live in `NullByte.Core/Persistence/Migrations/`; the highest-numbered migration there defines the current save version.

## Configuration

Configuration follows:

```text
defaults
  ↓
settings.json
  ↓
CLI overrides
  ↓
validation
  ↓
immutable runtime options
```

Keep parsing, validation and runtime configuration concerns separated.

## Testing architecture

Tests are split with the corresponding production layers:

- `NullByte.Core.Tests`
- `NullByte.Scripting.Tests`
- `NullByte.Shell.Tests`
- `NullByte.ConsoleUI.Tests`
- `NullByte.Editor.Tests`

Prefer testing behavior at the lowest layer that owns it. Use higher-level tests for integration boundaries rather than duplicating every lower-level test.

Deterministic clocks and other injected seams should be preferred over wall-clock sleeps.

## Extension patterns

When adding a feature:

1. Put persistent concepts in Core domain models.
2. Put gameplay operations in Core services/formulas.
3. Put recurring progression in simulation systems.
4. Expose functionality to scripts through the Core scripting API.
5. Adapt it to the console/editor rather than implementing a second rule set.
6. Add tests at the owning layer and integration tests at important boundaries.
7. Update documentation when the architecture or a significant decision changes.

## Architectural warning signs

Treat these as reasons to stop and reconsider:

- gameplay rules implemented in a console command;
- gameplay rules implemented only in the scripting API;
- editor-specific versions of Core operations;
- locks added around GameState;
- blocking waits on the game thread;
- real-time delays in deterministic tests;
- serialization of live process/thread objects;
- duplicate path parsing rules;
- a new long-running activity with its own lifecycle;
- background Roslyn work reading mutable GameState directly.

## Core principles

1. **One gameplay implementation.** Core owns the rules.
2. **One state owner.** The game thread owns mutable state.
3. **Game time is explicit.** Long-running behavior uses the scheduler/clock.
4. **Adapters stay thin.** Console, scripts and editor translate into Core.
5. **Snapshots cross background boundaries.**
6. **Persistence is explicit and versioned.**
7. **Prefer small, testable seams over global coordination.**
