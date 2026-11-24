# NullByte Architecture

This document describes the technical architecture of NullByte.

It explains the major runtime components, their responsibilities, how they interact, and the architectural boundaries that should remain stable as the game evolves.

For other perspectives:

- `AI_CONTEXT.md` defines development invariants and guidance for AI-assisted work.
- `CODEBASE.md` maps architectural concepts to specific source files.
- `CURRENT_STATUS.md` records current implementation and milestone progress.
- The Game Design Document defines intended game behavior and roadmap.

The current source code remains authoritative for implementation details.

---

# 1. Architectural goals

NullByte is designed around several major architectural goals.

## Deterministic simulation

Gameplay should behave predictably and be testable without relying directly on wall-clock delays or uncontrolled randomness.

## Single ownership of mutable game state

Mutable game state belongs to one game thread.

External components communicate with that owner rather than modifying game state concurrently.

## Shared gameplay behavior

Manual commands and player scripts invoke the same underlying game mechanics.

Gameplay rules should exist once.

## Runtime scripting as a first-class subsystem

Player-written C# scripts are part of the game itself rather than an auxiliary development feature.

Compilation, execution, process management, RAM and scripting APIs are therefore explicit architectural subsystems.

## UI independence

Core gameplay does not depend on the current console interface.

The existing ConsoleUI and VS Code integration are adapters around the game rather than owners of gameplay behavior.

## Testability

Time, randomness, output, persistence and other boundaries are designed so important behavior can be tested deterministically.

---

# 2. System architecture

At a high level:

```text
+------------------------------------------------------+
|                    Presentation                      |
|                                                      |
|  NullByte.ConsoleUI              NullByte.vscode     |
+-------------+----------------------------+-----------+
              |                            |
              |                            | WebSocket
              |                            |
              |                   +--------v---------+
              |                   | NullByte.Editor  |
              |                   +--------+---------+
              |                            |
+-------------v----------------------------v-----------+
|                 Host / Session Layer                 |
|                                                      |
|            NullByte.Shell / GameHost                 |
+--------------------------+---------------------------+
                           |
              +------------+-------------+
              |                          |
+-------------v-------------+ +----------v------------+
|   NullByte.Scripting      | |       Core APIs       |
|                           | |                       |
| Compiler                  | | Services              |
| Runtime                   | | Domain                |
| Process manager           | | Simulation            |
| Language service          | | Systems               |
+-------------+-------------+ +----------+------------+
              |                          |
              +------------+-------------+
                           |
                 +---------v---------+
                 |   NullByte.Core   |
                 |                   |
                 | Authoritative     |
                 | game state        |
                 +-------------------+
```

The boundaries are more important than the exact shape of this diagram.

The architecture should continue to make it possible to replace or add presentation layers without rewriting Core gameplay.

---

# 3. Project boundaries

## NullByte.Core

`NullByte.Core` contains the game itself.

It owns:

- mutable game state
- domain entities
- simulation loop
- simulation systems
- gameplay services
- formulas
- game time
- scheduling
- randomness abstractions
- virtual filesystem
- persistence models and mapping
- script-facing gameplay API contracts

Core must not depend on:

- ConsoleUI
- VS Code
- terminal behavior
- WebSockets
- presentation-specific concepts

---

## NullByte.Scripting

`NullByte.Scripting` owns execution of player-written C# code.

It contains four major areas:

```text
Compilation
Loading
Runtime
LanguageService
```

It depends on Core contracts where scripts must interact with gameplay.

It should not become an alternative implementation of gameplay mechanics.

---

## NullByte.Shell

`NullByte.Shell` owns reusable application/session lifecycle responsibilities above the game domain.

Examples include:

- active game session
- save management
- autosave
- persistence storage

This layer provides a natural boundary between Core simulation and presentation-specific hosts.

---

## NullByte.ConsoleUI

`NullByte.ConsoleUI` is currently the primary executable host.

It owns:

- application startup
- terminal
- command parsing
- command registration
- command presentation
- completion
- built-in editor
- host-level lifecycle orchestration

It should delegate actual game behavior downward.

---

## NullByte.Editor

`NullByte.Editor` connects external editors to a running game.

Currently this means VS Code.

It owns:

- WebSocket server
- authentication
- connection lifecycle
- protocol
- virtual-filesystem adaptation
- editor requests
- editor notifications

It is an adapter around existing game and scripting functionality.

---

## NullByte.vscode

The VS Code extension is an external client of `NullByte.Editor`.

It owns:

- connection to the running game
- VS Code virtual filesystem provider
- language-feature integration
- extension commands
- status display
- syntax highlighting

It does not contain game state or game rules.

---

# 4. Runtime composition

At application startup, ConsoleUI acts as the composition root.

Conceptually:

```text
Program
  |
  +--> Terminal
  |
  +--> Persistence infrastructure
  |
  +--> GameHost
          |
          +--> GameSession
          |      |
          |      +--> GameState
          |      +--> GameLoop
          |      +--> Clock / Scheduler
          |      +--> Services
          |      +--> Systems
          |      +--> Scripting
          |
          +--> Commands
          |
          +--> Autosave
          |
          +--> optional EditorServer
```

`Program` is responsible for process-level startup and shutdown.

`GameHost` coordinates host-level lifecycle.

`GameSession` represents the active playable simulation and its runtime services.

The exact composition may evolve, but these responsibilities should remain separate.

---

# 5. Game state

`GameState` represents the authoritative mutable state of the current game.

It contains or references major state such as:

- player
- network
- servers
- virtual filesystem
- botnet
- activities

The key architectural rule is:

> There is one authoritative game state for the active session.

Presentation components should not maintain shadow copies of domain state as alternative sources of truth.

Read-only DTOs or snapshots are appropriate where necessary, particularly across thread boundaries.

---

# 6. Game-thread model

NullByte deliberately avoids a general multi-threaded game-state model.

The simulation has a single owner thread.

Conceptually:

```text
                     External threads
                           |
             +-------------+-------------+
             |                           |
          Console                     Editor
             |                           |
             +-------------+-------------+
                           |
                    game-thread
                     boundary
                           |
                           v
                 +------------------+
                 |    Game thread   |
                 |                  |
                 | GameState        |
                 | Services         |
                 | Systems          |
                 | Scheduler        |
                 +------------------+
```

Mutable game state is read and modified on this thread.

The architecture should not gradually evolve into:

```text
Thread A ----\
Thread B -----+--> lock(GameState)
Thread C ----/
```

Locks may still be appropriate for infrastructure such as output or connection collections, but they are not the game-state consistency model.

---

# 7. Asynchronous simulation

Single-threaded state ownership does not mean gameplay operations are synchronous.

Operations such as:

- Hack
- Grow
- Weaken
- Sleep

may span game time.

They suspend and resume without blocking the game thread.

Conceptually:

```text
Script
   |
   | await Hack(...)
   v
Schedule completion
   |
   +---------------> game loop continues
                         |
                         | other scripts
                         | botnet
                         | activities
                         | timers
                         v
                    game time advances
                         |
                         v
                  Hack becomes ready
                         |
                         v
                 continuation resumes
```

This model allows many logical operations to coexist while maintaining a single owner for mutable state.

---

# 8. Synchronization context

`GameSynchronizationContext` enables asynchronous continuations to return to the game thread.

Together with the game loop and scheduler it provides the runtime model required by asynchronous game actions and scripts.

Conceptually:

```text
Game thread
    |
    v
async operation starts
    |
    v
await
    |
    +------ operation suspended
              |
              v
        simulation continues
              |
              v
        operation completes
              |
              v
      continuation queued
              |
              v
       GameSynchronizationContext
              |
              v
          Game thread
```

Changes to the synchronization context, scheduler or game loop must therefore be treated as interconnected architectural changes.

---

# 9. Time architecture

NullByte distinguishes between:

## Game time

Used for gameplay progression.

Examples:

- hacking duration
- sleep
- activity progress
- botnet progress
- scheduled game work

Game time flows through `IGameClock` and the scheduler.

## Wall-clock time

Used where the concept represents real application time rather than gameplay progression.

For example, file metadata may use real timestamps.

The distinction is semantic rather than simply technical.

---

# 10. Simulation systems

Continuous/background game mechanics are modeled as simulation systems.

Current examples include:

- activity progression
- botnet progression

Systems participate in simulation advancement through a common simulation-system concept.

This prevents each passive subsystem from creating its own timer/thread.

Preferred pattern:

```text
GameLoop
   |
   +--> System A
   +--> System B
   +--> System C
```

Avoid:

```text
System A --> own timer/thread
System B --> own timer/thread
System C --> own timer/thread
```

unless a subsystem genuinely exists outside game simulation.

---

# 11. Gameplay services

Services model game operations that act on domain state.

Examples include:

- `ServerActions`
- `NetworkService`
- `ActivityService`
- `BotnetService`

Services provide a shared point of entry for gameplay behavior.

This allows both:

```text
Console command
      |
      v
    Service
```

and:

```text
Player script API
      |
      v
    Service
```

to reach the same implementation.

Commands and script APIs therefore remain adapters rather than alternative game engines.

---

# 12. Formula layer

Pure or mostly pure calculations are separated into formula classes.

Examples include:

- hacking formulas
- skill formulas
- network formulas
- botnet formulas
- activity formulas

This provides:

- easier deterministic testing
- clearer separation between state mutation and calculation
- centralized balancing logic

Prefer:

```text
Service
   |
   +--> inspect state
   +--> call Formula
   +--> apply result
```

over embedding complex numerical formulas directly into command or API handlers.

---

# 13. Randomness

Gameplay randomness uses an abstraction rather than uncontrolled global randomness.

Conceptually:

```text
Gameplay service
      |
      v
IRandomSource
      |
      +--> production random source
      |
      +--> deterministic test source
```

This enables repeatable tests of otherwise probabilistic behavior.

Randomness should be treated similarly to time: an explicit dependency when it materially affects game behavior.

---

# 14. Virtual filesystem architecture

NullByte has its own filesystem independent of the host operating system.

Conceptually:

```text
                       VirtualFileSystem
                              |
             +----------------+----------------+
             |                |                |
          Console          Scripts          Editor
             |                |                |
       FileCommands    NullByteFilesApi  EditorFileService
```

All three views should ultimately use the same filesystem rules.

`VirtualPath` centralizes path normalization and validation.

This prevents subtle differences between how ConsoleUI, scripts and VS Code interpret the same game path.

---

# 15. Path contexts

Not every consumer has the same path-resolution context.

## Console

The player has a current:

- server
- directory

Console commands may resolve paths relative to that directory.

## Scripts

Script paths are server-root-relative.

Scripts do not inherit the player's interactive console working directory.

## External editor

Editor URIs identify both server and virtual path.

They map into the same underlying virtual filesystem but do not depend on console working-directory state.

This distinction must remain explicit.

---

# 16. Scripting architecture

Player scripts move through several layers:

```text
.cx source
    |
    v
Include resolution
    |
    v
Source generation
    |
    v
Roslyn compilation
    |
    +--> Diagnostics
    |
    +--> RAM analysis
    |
    v
Assembly emission
    |
    v
Collectible load context
    |
    v
IScript implementation
    |
    v
ScriptRunner
    |
    v
NullByte API
    |
    v
Core services / game state
```

Each layer has a distinct responsibility.

---

# 17. Script source model

Player `.cx` files use C# top-level statements.

Players are not required to write the runtime class or entry method.

Instead the compilation pipeline generates a wrapper implementing the runtime script contract.

Conceptually the player writes:

```text
await nb.Hack("server");
```

while the generated structure behaves roughly like:

```text
GeneratedScript : IScript
    |
    +--> RunAsync(nb, args)
```

The generated code is an implementation detail.

Diagnostics should remain expressed in terms of the player's source files.

---

# 18. Script API architecture

Scripts interact with the game through explicit API interfaces.

The primary concept is:

`INullByteApi`

with specialized APIs such as:

- file API
- botnet API

The runtime creates an API instance appropriate to the script process.

Conceptually:

```text
Compiled script
      |
      v
 INullByteApi
      |
      +--> files
      +--> botnet
      +--> game actions
      |
      v
 Core behavior
```

This API is both:

- a gameplay surface
- a security boundary

Adding a new script-facing capability should therefore be intentional.

---

# 19. Script RAM architecture

RAM is calculated from source semantics rather than actual CLR memory usage.

Conceptually:

```text
Script sources
      |
      v
Roslyn semantic model
      |
      v
Find charged API usage
      |
      v
Static script RAM cost
      |
      x thread count
      |
      v
Required server RAM
```

`RamCostAttribute` associates gameplay RAM costs with API capabilities.

Because the analysis is semantic, aliases, overloads and includes should resolve through the compiler model rather than text matching.

---

# 20. Script processes

A running script is represented as a game-level process.

Conceptually:

```text
ScriptProcess
    |
    +--> PID
    +--> host
    +--> script
    +--> args
    +--> threads
    +--> RAM
    +--> state
    +--> cancellation
```

`ScriptProcessManager` owns process tracking and lookup.

`ScriptService` coordinates compilation and process startup.

`ScriptRunner` executes compiled code.

These responsibilities should remain distinct:

```text
ScriptService
    |
    +--> compile
    +--> validate RAM
    +--> construct/register process
    |
    v
ScriptRunner
    |
    v
running script

ScriptProcessManager
    |
    +--> track lifecycle
```

---

# 21. Assembly loading

Compiled scripts are loaded into collectible assembly-load contexts.

The goal is to allow generated user code to become unloadable after its runtime lifetime ends.

Conceptually:

```text
Roslyn emitted assembly
        |
        v
ScriptLoadContext
        |
        v
ScriptAssembly
        |
        v
execution
        |
        v
release references
        |
        v
eligible for unload
```

Long-lived infrastructure must avoid unintentionally retaining references to generated types or assemblies.

---

# 22. Include architecture

Player libraries use:

`#include "path"`

The include system builds a collection/graph of source files before compilation.

Conceptually:

```text
main.cx
   |
   +--> utils.cx
   |      |
   |      +--> math.cx
   |
   +--> constants.cx
```

Resolution responsibilities include:

- locating source
- recursive traversal
- cycle detection
- depth enforcement
- duplicate suppression
- library validation

The resulting source set participates consistently in:

- compilation
- diagnostics
- RAM analysis
- language-service behavior

Includes are source composition, not independent runtime modules.

Running processes contain the code compiled at startup and do not dynamically observe later modifications to included source.

---

# 23. Source snapshots

Roslyn analysis may be computationally expensive and can run away from the game thread.

Live virtual filesystem state cannot simply be accessed from arbitrary compiler threads.

The architecture therefore separates:

```text
Game-thread state
      |
      | copy required source
      v
SourceSnapshot
      |
      v
background Roslyn work
```

This pattern is important beyond compilation.

Whenever expensive background work needs game-owned data, prefer:

1. briefly enter/call the game-thread boundary
2. copy the necessary immutable information
3. perform expensive work outside the game thread

rather than holding or sharing mutable domain objects across threads.

---

# 24. Language-service architecture

The scripting language service reuses Roslyn concepts to provide editor functionality.

Supported areas include:

- diagnostics
- completion
- hover
- signature help
- definition lookup

Conceptually:

```text
Editor request
      |
      v
EditorServer
      |
      v
obtain source snapshot
      |
      v
ScriptLanguageService
      |
      v
Roslyn
      |
      v
protocol result
      |
      v
VS Code
```

The language service should understand the same script environment as actual compilation.

A script should not normally appear valid in the editor but fail because the editor modeled a fundamentally different compilation environment.

---

# 25. Editor architecture

External editor integration consists of two applications.

```text
+-----------------------+
|   VS Code extension   |
|                       |
| TypeScript            |
| FileSystemProvider    |
| Commands              |
| Language integrations |
+-----------+-----------+
            |
            | WebSocket / JSON
            |
+-----------v-----------+
|    NullByte.Editor    |
|                       |
| EditorServer          |
| EditorProtocol        |
| EditorFileService     |
| Language integration  |
+-----------+-----------+
            |
            | game-thread boundary
            |
+-----------v-----------+
|      Game session     |
+-----------------------+
```

The protocol is the contract between the two implementations.

Protocol changes should therefore be treated like changes to a public interface.

---

# 26. Editor connection model

`EditorServer` may host multiple simultaneous connections.

Connection information belongs to the editor-server subsystem.

Other components that need visibility into connections should receive a narrow read-only abstraction/provider rather than reaching into transport details.

This is particularly relevant for debug tooling.

Connection state is dynamic.

Consumers should generally query current state rather than retaining a snapshot indefinitely.

---

# 27. Editor authentication

The editor integration is deliberately local.

The architecture includes:

- loopback-only server
- per-run authentication token
- connection information file
- ownership-based cleanup
- Origin restrictions

The VS Code extension discovers the currently running game through the connection information.

These mechanisms provide practical local protection without claiming to form a hardened hostile-code security boundary.

---

# 28. Editor filesystem model

VS Code exposes the NullByte filesystem through a VS Code `FileSystemProvider`.

Conceptually:

```text
VS Code Explorer
      |
      v
filesystem.ts
      |
      v
client.ts
      |
    WebSocket
      |
      v
EditorProtocol
      |
      v
EditorFileService
      |
      v
VirtualFileSystem
```

Changes made through VS Code therefore modify in-game files.

Changes originating in the game are broadcast back so VS Code can refresh its view.

This is synchronization of one authoritative filesystem, not mirroring between two filesystems.

---

# 29. Game replacement and observers

A load or reset may replace the active game/session.

Long-lived infrastructure, such as the editor server, cannot assume the original `GameState` remains current forever.

Conceptually:

```text
EditorServer
    |
    +--> watches GameState A

        reset/load

    +--> detach from GameState A
    |
    +--> attach to GameState B
    |
    +--> notify clients
```

Any subsystem subscribing to events on replaceable state must account for:

- detaching old subscriptions
- obtaining the new state
- attaching new subscriptions
- notifying interested consumers when appropriate

Stale subscriptions are both a correctness and lifetime-management problem.

---

# 30. Session lifecycle

A game session owns a coherent set of runtime objects.

Conceptually:

```text
GameSession
    |
    +--> GameState
    +--> Clock
    +--> Scheduler
    +--> GameLoop
    +--> Simulation systems
    +--> Gameplay services
    +--> Script runtime
```

The session should be replaceable as a unit where a true new game or load requires it.

This provides cleaner lifecycle semantics than mutating every object in an existing session back to an initial state.

---

# 31. Host lifecycle

`GameHost` sits above the active session.

Its responsibilities include coordinating lifecycle that survives or manages session replacement.

Conceptually:

```text
GameHost
    |
    +--> current GameSession
    +--> command infrastructure
    +--> autosave lifecycle
    +--> load/reset handling
    +--> host shutdown
```

This distinction is especially important for new-game and load behavior.

The host is not itself the game state.

---

# 32. Host requests / mailbox

Operations that originate outside the running session but require host-level lifecycle changes need a safe request mechanism.

Examples include:

- load
- reset
- shutdown

These operations should be represented through a common host-directed request/mailbox concept rather than introducing independent polling flags for every new operation.

Conceptually:

```text
Command / external initiator
          |
          v
     Host request
          |
          v
        Mailbox
          |
          v
       GameHost
          |
          +--> load
          +--> reset
          +--> shutdown
```

The mailbox separates:

- requesting an operation
- executing the lifecycle transition

The current source should always be inspected for the exact request hierarchy and APIs before extending it.

---

# 33. Reset architecture

"Reset" means start a genuinely new game.

It should not mutate the existing `GameState` back toward defaults field-by-field.

Conceptually:

```text
Current session
      |
      | validate reset is allowed
      v
stop/dispose old game runtime
      |
      v
remove autosave
      |
      v
create new session/state
      |
      v
reattach host integrations
      |
      v
notify observers
```

Any active operation that prevents a safe reset must be handled according to the defined reset behavior rather than silently discarded.

Reset is a host/session lifecycle operation, not a Core domain method.

---

# 34. Persistence architecture

Persistence crosses several architectural layers.

```text
GameState
    |
    v
SaveGameMapper
    |
    v
versioned SaveGame model
    |
    v
SaveGameSerializer
    |
    v
SaveManager
    |
    v
ISaveStore
    |
    v
FileSaveStore
```

Each layer has a separate responsibility.

## Mapping

Converts between runtime domain state and persistent models.

## Serialization

Converts persistent models to/from stored representation.

## Migration

Transforms old save format versions forward.

## Storage

Reads/writes save files on the actual host filesystem.

These concerns should not be collapsed into domain entities.

---

# 35. Save migration

Save compatibility is explicitly versioned.

Conceptually:

```text
Old save
   |
   v
read version
   |
   v
migration N -> N+1
   |
   v
migration N+1 -> N+2
   |
   v
current save model
   |
   v
GameState
```

A persistent model change must therefore consider whether it requires:

- no migration
- defaulting
- explicit migration
- rejection of unsupported data

Golden-save tests help protect this contract.

---

# 36. Autosave

Autosave belongs to host/session infrastructure rather than Core game mechanics.

It periodically invokes the same persistence architecture without requiring user interaction.

Autosave lifecycle must align with session lifecycle.

Important transitions include:

- application startup
- session replacement
- reset
- shutdown

A true reset removes the old autosave so a newly reset game does not later restore the previous state.

---

# 37. Application configuration

Application configuration is separate from game balance.

Conceptually:

```text
settings.json ----+
                  |
                  v
                merge
                  ^
                  |
CLI arguments ----+
                  |
                  v
             validation
                  |
                  v
          application options
                  |
                  v
              startup
```

Responsibilities should remain distinct:

- parse
- load
- merge
- validate
- apply
- optionally write default/current settings

Game balance configuration should remain a separate concept even when both are configuration objects.

---

# 38. Console command architecture

Console commands are grouped feature adapters.

The general flow is:

```text
terminal input
      |
      v
CommandLineParser
      |
      v
CommandDispatcher
      |
      v
CommandRegistry
      |
      v
command handler
      |
      v
Core/Scripting/Host service
```

A command generally owns:

- parsing command-specific arguments
- selecting the required operation
- user-facing output formatting

It should not own substantial game-rule implementations.

---

# 39. Completion architecture

Console completion is separated from command execution.

The completion subsystem builds completion candidates/snapshots appropriate to the current input and game context.

This lets terminal input remain responsive while avoiding arbitrary access to mutable game state from the input thread.

As with Roslyn operations, snapshots are preferable when information will outlive the immediate game-thread access.

---

# 40. Activity architecture

Activities represent longer-running player behaviors such as jobs and study.

The architecture separates:

```text
Activity definitions/domain
          |
          v
   ActivityService
          |
          v
   ActivityState
          |
          v
   ActivitySystem
          |
          v
progress over game time
```

Formulas calculate rates and outcomes.

The service manages transitions.

The simulation system advances active activity state.

The ConsoleUI provides commands and presentation.

This pattern should be reused for future long-running activity types that naturally belong to the same lifecycle.

---

# 41. Botnet architecture

Botnet progression follows a similar layered pattern:

```text
Botnet domain state
        |
        +--> BotnetService
        |       |
        |       +--> purchases/upgrades
        |
        +--> BotnetSystem
        |       |
        |       +--> passive production
        |
        +--> BotnetFormulas
                |
                +--> deterministic calculations
```

Console commands and scripts adapt to these services rather than implementing independent botnet rules.

---

# 42. Testing architecture

Tests follow subsystem boundaries.

Important testing strategies include:

## Pure formula tests

For deterministic numerical rules.

## Service tests

For state transitions and operations.

## Simulation tests

For behavior that depends on game-loop progression.

## Fake clock tests

For time-dependent behavior without real delays.

## Deterministic randomness

For probabilistic mechanics.

## Compiler tests

Using actual Roslyn behavior.

## Acceptance tests

For scripting behavior crossing compilation, loading and execution.

## Golden persistence tests

For compatibility of serialized state.

## Integration tests

For boundaries that are more valuable tested together than mocked apart.

Mocks/substitutes should support tests, not define the architecture.

---

# 43. Error handling

Errors should be represented at the architectural layer that can meaningfully interpret them.

Examples:

- invalid game action: domain/service result
- invalid script: compiler diagnostic
- invalid save: persistence exception/result
- bad editor request: protocol response
- invalid console input: command-facing message

Avoid using exceptions for ordinary expected gameplay failure when a result type communicates the condition more clearly.

Exceptions remain appropriate for genuinely exceptional or invalid-runtime situations.

---

# 44. Data flow example: manual Hack

```text
Player types hack command
        |
        v
Console command
        |
        v
ServerActions
        |
        +--> HackingFormulas
        |
        +--> GameScheduler / Clock
        |
        v
GameState / Server / Player
        |
        v
result returned
        |
        v
Console output
```

The ConsoleUI does not calculate hacking outcomes itself.

---

# 45. Data flow example: scripted Hack

```text
Player script
     |
     v
INullByteApi
     |
     v
NullByteApi
     |
     v
ServerActions
     |
     +--> HackingFormulas
     |
     +--> GameScheduler / Clock
     |
     v
GameState / Server / Player
```

Manual and scripted Hack converge on the same gameplay implementation.

---

# 46. Data flow example: script execution

```text
Console / Editor requests run
        |
        v
ScriptService
        |
        +--> obtain source
        |
        +--> compile
        |      |
        |      +--> includes
        |      +--> wrapper generation
        |      +--> Roslyn
        |      +--> RAM analysis
        |
        +--> validate host RAM
        |
        +--> create ScriptProcess
        |
        +--> register process
        |
        v
ScriptRunner
        |
        v
IScript.RunAsync
        |
        v
INullByteApi
        |
        v
Core services
```

---

# 47. Data flow example: editor file read

```text
VS Code
   |
   v
filesystem.ts
   |
   v
client.ts
   |
   | WebSocket
   v
EditorServer / EditorProtocol
   |
   | game-thread boundary
   v
EditorFileService
   |
   v
VirtualFileSystem
   |
   v
file contents
```

The editor does not read Core state directly from the WebSocket thread.

---

# 48. Data flow example: language-service request

```text
VS Code
   |
   v
language feature module
   |
   v
client.ts
   |
   | WebSocket
   v
Editor protocol
   |
   v
snapshot required source
   |
   | leave game thread
   v
ScriptLanguageService
   |
   v
Roslyn
   |
   v
result
   |
   v
VS Code
```

Only the short source-acquisition phase needs the game-owned state.

Expensive Roslyn analysis should not block the simulation thread unnecessarily.

---

# 49. Data flow example: save

```text
Console / Autosave
        |
        v
SaveManager
        |
        v
SaveGameMapper
        |
        v
versioned save model
        |
        v
SaveGameSerializer
        |
        v
ISaveStore
        |
        v
host filesystem
```

Loading follows the reverse direction with migration and validation before creating/restoring game state.

---

# 50. Boundaries to preserve

The following boundaries are intentional.

## Core vs presentation

Core does not know about terminal, VS Code or WPF.

## Domain vs persistence

Domain objects do not define the storage format.

## Game thread vs external threads

Mutable state does not become shared concurrent state.

## Gameplay vs script infrastructure

Roslyn infrastructure does not become the owner of game mechanics.

## Virtual filesystem vs host filesystem

Player files are isolated from ordinary host file access.

## Simulation vs wall-clock infrastructure

Gameplay progression uses game time where semantically appropriate.

## Editor server vs editor client

Communication uses an explicit protocol contract.

---

# 51. Extension points

When introducing new functionality, prefer existing architectural extension points.

## New gameplay mechanic

Usually:

```text
Domain
  +
Formula if required
  +
Service
  +
System if continuously simulated
```

Then expose through commands and/or scripts.

## New script API capability

Usually:

```text
existing Core service
      |
      v
INullByteApi contract
      |
      v
NullByteApi
      |
      v
RAM metadata/tests
```

## New passive simulation

Usually:

```text
domain state
    +
ISimulationSystem implementation
    +
formulas/services as appropriate
```

## New editor operation

Usually:

```text
existing functionality
      |
      v
EditorProtocol
      |
      v
client.ts
      |
      v
VS Code command/provider
```

## New host lifecycle operation

Usually:

```text
request/mailbox
      |
      v
GameHost
      |
      v
session/persistence lifecycle
```

---

# 52. Architectural warning signs

A proposed change deserves reconsideration if it introduces any of the following.

## Core depends on UI

For example:

```text
Core -> ConsoleUI
Core -> VS Code
```

## Game state needs widespread locks

Usually indicates the game-thread boundary has been bypassed.

## Console and scripts implement the same rule independently

Gameplay behavior probably belongs in Core.

## Background compiler code directly holds mutable GameState

Use snapshots instead.

## Every passive system creates its own real timer

Prefer the simulation system/game scheduler architecture.

## Save compatibility changes accidentally

Persistent schema changes should be explicit.

## Editor and compiler disagree about script semantics

Both should reuse the same compilation concepts.

## New host operation introduces another dedicated polling flag

Prefer the common host request/mailbox model.

## Infrastructure begins depending on concrete generated script types

This can prevent collectible script assemblies from unloading.

---

# 53. Architectural decision process

Before making a substantial structural change:

1. Describe the current flow.
2. Identify the actual limitation.
3. Identify which component should own the new responsibility.
4. Check existing extension points.
5. Preserve dependency direction.
6. Consider game-thread ownership.
7. Consider persistence impact.
8. Consider scripting impact.
9. Consider editor impact.
10. Consider testing strategy.
11. Implement the smallest architecture that solves the current requirement.

Do not introduce speculative abstractions solely because a future feature might need them.

---

# 54. Architecture summary

NullByte can be understood as five cooperating layers:

```text
1. Presentation
   ConsoleUI / VS Code

2. Integration and host
   Editor / Shell / GameHost

3. Player scripting
   compiler / runtime / processes / language service

4. Game application logic
   services / simulation systems / formulas

5. Game domain
   GameState / Player / Network / Servers / Filesystem / Activities / Botnet
```

Across those layers, three architectural rules dominate:

### One owner for mutable game state

The simulation/game thread owns game state.

### One implementation of gameplay behavior

UI commands and player scripts converge on shared Core services.

### Infrastructure adapts to the game

Roslyn, persistence, ConsoleUI and editor integrations surround the game domain rather than forcing the domain to depend on them.

Those properties are the foundation on which future NullByte features should continue to build.