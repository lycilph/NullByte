# NullByte Architecture

This document describes NullByte's technical architecture and major system boundaries.

See:

- `AI_CONTEXT.md` - development invariants
- `CODEBASE.md` - source/test navigation
- `CURRENT_STATUS.md` - active development state
- `DESIGN.md` - gameplay and product design

Source code is authoritative for implementation details.

---

# 1. Architecture overview

NullByte is organized around five conceptual layers:

```text
Presentation
  ConsoleUI / VS Code
        |
Integration and hosting
  GameHost / Shell / Editor
        |
Scripting
  Compiler / Runtime / Language Service
        |
Application logic
  Services / Systems / Formulas
        |
Domain
  GameState / Player / Network / Filesystem
```

Dependencies should generally flow downward.

Core gameplay must remain independent of presentation technology.

---

# 2. Project responsibilities

## NullByte.Core

Owns:

- domain state
- gameplay services
- formulas
- simulation
- game time and scheduling
- virtual filesystem
- persistence models
- script-facing gameplay APIs

It does not depend on ConsoleUI, VS Code or editor infrastructure.

## NullByte.Scripting

Owns:

- Roslyn compilation
- source generation
- includes
- RAM analysis
- assembly loading
- script execution/processes
- language services

It uses Core APIs rather than implementing separate game mechanics.

## NullByte.Shell

Owns reusable host/session concerns:

- game sessions
- save management
- autosave

## NullByte.ConsoleUI

Current executable host.

Owns:

- startup
- terminal
- command infrastructure
- completion
- host lifecycle coordination

Commands delegate gameplay to lower layers.

## NullByte.Editor

Host-side external editor integration.

Owns:

- WebSocket server
- authentication
- protocol
- connections
- editor-facing filesystem operations
- editor notifications

## NullByte.vscode

TypeScript VS Code client.

Owns:

- connection to Editor
- virtual filesystem provider
- language-feature presentation
- commands
- connection status

It contains no authoritative game state.

---

# 3. Runtime composition

Conceptually:

```text
Program
  |
GameHost
  |
  +-- GameSession
  |     +-- GameState
  |     +-- GameLoop
  |     +-- Clock/Scheduler
  |     +-- Services
  |     +-- Systems
  |     +-- Scripting runtime
  |
  +-- Commands
  +-- Autosave
  +-- optional EditorServer
```

`Program` owns process startup/shutdown.

`GameHost` coordinates host-level lifecycle.

`GameSession` owns the active playable simulation.

A load/reset may replace the active session without replacing the host.

---

# 4. Game-thread model

Mutable game state has one owner: the game thread.

```text
Console ------+
              |
Editor -------+--> game-thread boundary --> GameState
              |
Other work ---+
```

External components must marshal game-state access through the appropriate boundary.

Domain locking is not the state-consistency model.

Infrastructure may use synchronization internally where appropriate, but that does not permit arbitrary cross-thread game-state access.

---

# 5. Async simulation and time

Single-thread ownership does not mean operations must block.

Long-running actions such as Hack, Grow, Weaken and script Sleep suspend while simulation continues.

```text
operation
   |
 await
   |
scheduler
   |
game continues
   |
completion becomes ready
   |
continuation resumes on game thread
```

`GameSynchronizationContext`, `GameScheduler`, `IGameClock` and `GameLoop` cooperate to provide this model.

Gameplay progression uses game time.

Wall-clock time may be used where the semantic concept is real-world/application time, such as appropriate file metadata.

Never assume the two are interchangeable.

---

# 6. Application logic

Game behavior is separated into complementary concepts.

## Services

Perform and coordinate gameplay operations.

Examples:

- server actions
- network operations
- activities
- Botnet operations

## Formulas

Contain deterministic calculations.

Services typically obtain state, invoke formulas and apply resulting changes.

## Simulation systems

Advance passive/continuous mechanics during simulation.

Examples:

- activities
- Botnet production

Prefer simulation systems over independent real timers for game mechanics.

---

# 7. Shared gameplay path

Manual commands and scripts converge on shared Core behavior.

```text
Console command ----+
                    |
                    v
               Core service
                    ^
                    |
Script API ----------+
```

Presentation and scripting layers should not duplicate gameplay rules.

This is a major architectural boundary.

---

# 8. Virtual filesystem

NullByte owns an in-game filesystem independent of the operating system.

```text
Console ------+
              |
Scripts ------+--> VirtualFileSystem
              |
Editor -------+
```

`VirtualFileSystem` is authoritative for filesystem behavior.

`VirtualPath` centralizes path rules.

Different consumers may have different resolution contexts:

- console paths may resolve against current directory
- script paths are server-root-relative
- editor paths identify server and virtual path

These differences belong in adapters/resolvers, not separate filesystem implementations.

---

# 9. Scripting pipeline

Player `.cx` source follows this conceptual pipeline:

```text
source
  |
includes
  |
generated wrapper
  |
Roslyn compilation
  +-- diagnostics
  +-- RAM analysis
  |
assembly emission
  |
collectible load context
  |
IScript
  |
ScriptRunner
  |
INullByteApi
  |
Core services
```

The generated wrapper supplies the runtime contract and implicit main-script context.

Compilation, RAM analysis and editor language services must model compatible script semantics.

---

# 10. Script runtime

Responsibilities remain separated:

```text
ScriptService
   |
   +-- compile
   +-- validate execution/RAM
   +-- start/restore
   |
ScriptProcessManager
   |
   +-- process ownership/lifecycle
   |
ScriptRunner
   |
   +-- execute compiled script
```

Processes represent gameplay processes, not serialized CLR execution state.

Compiled script assemblies use collectible load contexts. Long-lived infrastructure should avoid retaining generated assemblies/types unnecessarily.

---

# 11. Includes and source snapshots

Includes compose multiple virtual source files into one compilation.

They affect:

- compilation
- diagnostics
- RAM
- language services

Roslyn work may execute outside the game thread.

It must not read live game-owned filesystem state directly.

Use:

```text
game thread
   |
copy required source
   |
immutable snapshot
   |
background Roslyn work
```

This snapshot pattern applies generally when expensive external work needs game-owned information.

---

# 12. Editor architecture

Editor integration has two sides:

```text
VS Code extension
      |
WebSocket / JSON
      |
NullByte.Editor
      |
game-thread boundary
      |
GameSession
```

The protocol is the contract between C# and TypeScript.

Protocol changes normally require coordinated changes on both sides.

The editor exposes the existing virtual filesystem rather than maintaining a second copy.

Game-originated filesystem changes are broadcast so clients can refresh.

---

# 13. Editor language services

Language operations conceptually follow:

```text
VS Code
  |
Editor protocol
  |
snapshot source
  |
ScriptLanguageService
  |
Roslyn
  |
result
```

Only source acquisition requires access to game-owned data.

Expensive Roslyn work should happen outside the game thread.

The language service should model the same script environment as runtime compilation.

---

# 14. Replaceable game state

Load/reset can replace the active game/session.

Long-lived observers must handle replacement:

```text
watch old state
   |
detach
   |
replace session
   |
attach new state
   |
notify consumers
```

This is particularly important for editor event subscriptions.

Stale subscriptions can cause incorrect behavior and prevent old state from being released.

---

# 15. Host lifecycle and requests

Operations such as:

- load
- reset
- shutdown

are host lifecycle operations rather than ordinary domain actions.

They use the host request/mailbox mechanism.

```text
command/requester
      |
mailbox
      |
GameHost
      |
session lifecycle
```

This separates requesting a transition from executing it at the correct lifecycle boundary.

Reset creates a new game/session rather than clearing the old `GameState` field-by-field.

---

# 16. Persistence

Persistence is layered:

```text
GameState
   |
SaveGameMapper
   |
versioned SaveGame
   |
SaveGameSerializer
   |
SaveManager
   |
ISaveStore
   |
host filesystem
```

Responsibilities:

- mapping: runtime state ↔ persistent model
- serialization: persistent model ↔ stored representation
- migration: old save version → current version
- storage: physical save files

Persistent schema changes must explicitly consider migration and compatibility.

Autosave uses the same persistence model and follows session lifecycle.

---

# 17. Configuration

Application configuration and gameplay balance are separate concerns.

Application settings conceptually flow through:

```text
settings file --+
                +--> merge --> validate --> startup
CLI overrides --+
```

Balance configuration belongs to gameplay.

Do not collapse both concepts into one configuration system simply because both contain values.

---

# 18. Testing architecture

Testing follows subsystem boundaries.

Preferred approaches include:

- pure formula tests
- service/state-transition tests
- simulation tests
- fake game clocks
- deterministic randomness
- real Roslyn compiler tests
- scripting acceptance tests
- persistence golden tests
- integration tests for meaningful boundaries

Tests should validate externally observable behavior rather than unnecessarily freezing implementation details.

---

# 19. Extension patterns

## New gameplay feature

Typically:

```text
Domain
+ Formula when needed
+ Service
+ SimulationSystem when continuous
```

Expose afterward through commands/scripts.

## New script API

```text
existing Core behavior
   |
INullByteApi
   |
implementation
   |
RAM metadata/tests
```

## New editor operation

```text
existing functionality
   |
EditorProtocol
   |
client.ts
   |
VS Code feature
```

## New host lifecycle operation

```text
request/mailbox
   |
GameHost
   |
session/persistence transition
```

Prefer existing extension points before introducing parallel mechanisms.

---

# 20. Architectural warning signs

Reconsider a change when it introduces:

- Core dependencies on presentation/editor code
- widespread locks around game state
- duplicated rules between commands and scripts
- background access to mutable game state
- independent real timers for simulation mechanics
- accidental save-format incompatibility
- different script semantics between compiler and editor
- host lifecycle operations implemented as independent polling flags
- long-lived references to generated script assemblies
- editor-specific implementations of existing gameplay behavior

---

# 21. Core architectural principles

When evaluating architecture, preserve these unless deliberately changing them:

> Mutable game state has one owner thread.

> Manual and scripted actions use shared game mechanics.

> Long-running gameplay suspends without blocking simulation.

> Expensive background work receives snapshots, not mutable game state.

> Core gameplay is independent of presentation.

> Persistence formats evolve explicitly.

> Editor and scripting infrastructure adapt to the game rather than owning it.

Prefer the smallest architecture that satisfies the current requirement.

Do not add speculative abstractions for hypothetical future needs.