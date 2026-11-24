# NullByte AI Development Context

This document provides the minimum architectural and development context needed to work effectively on the NullByte codebase.

It is intended for AI-assisted development. Read this document before proposing architectural changes or modifying source code.

The Game Design Document is the authoritative source for intended game behavior and milestone scope. This file focuses on how the existing codebase is structured, the architectural constraints that must be preserved, and how changes should be approached.

---

# 1. Project overview

NullByte is a text-driven incremental hacking game inspired by Bitburner.

The core progression loop is:

1. Perform actions manually.
2. Earn money and hacking experience.
3. Acquire infrastructure and resources.
4. Write C# scripts to automate repetitive actions.
5. Gradually replace manual play with increasingly sophisticated automation.

The central design principle is:

> Scripting is the game.

Player scripts are written in C#-like `.cx` files and compiled at runtime using Roslyn.

Scripts use the same underlying game mechanics as manual console commands. There should not normally be separate game rules for scripted and manual actions.

The current host is a console application. VS Code can optionally connect to the running game and edit the game's virtual files. A WPF front end may be added later without replacing the Core or Scripting architecture.

---

# 2. Technology

Primary technologies:

- C#
- .NET 10
- Roslyn
- xUnit
- NSubstitute where mocking is useful
- TypeScript for the VS Code extension
- WebSockets for communication between NullByte and the VS Code extension

Nullable reference types are enabled.

Player scripts use `.cx` as their file extension.

Scripts are compiled as regular C# through a generated wrapper. Roslyn script mode is deliberately not used.

---

# 3. Solution responsibilities

The solution is divided into components with deliberately different responsibilities.

## NullByte.Core

Owns the game domain and simulation.

Examples include:

- game state
- player
- servers and network
- hacking actions
- formulas
- botnet
- activities
- virtual filesystem
- game clock
- scheduling
- process state
- persistence models
- script-facing API contracts

Core must not depend on a particular user interface.

Do not introduce ConsoleUI, VS Code, WPF, or other presentation concerns into Core.

---

## NullByte.Scripting

Owns runtime compilation and execution of player-written scripts.

Responsibilities include:

- Roslyn compilation
- script source wrapping
- include resolution
- script diagnostics
- RAM analysis
- assembly loading
- script execution
- process management
- source snapshots
- language-service support used by editors

The scripting implementation is separate from the game domain wherever practical.

---

## NullByte.Editor

Provides host-side integration with external editors, currently VS Code.

Responsibilities include:

- WebSocket server
- editor protocol
- authentication
- editor connection lifecycle
- virtual-file-system operations
- marshalling operations onto the game thread
- language-service requests
- game-to-editor change notifications

It may depend on Core and Scripting.

It must respect the same game-thread ownership rules as every other external component.

---

## NullByte.ConsoleUI

The current interactive game host.

Responsibilities include:

- terminal interaction
- command parsing
- commands
- completion
- rendering
- application startup
- user interaction
- hosting a game session

Console commands should orchestrate domain functionality rather than contain independent implementations of game rules.

---

## NullByte.Shell

Contains host/session-level functionality that is not specific to terminal presentation.

This includes functionality such as:

- game sessions
- save management
- autosaving

Keep reusable session/application-lifecycle concerns here rather than tying them unnecessarily to ConsoleUI.

---

## NullByte.vscode

A separate TypeScript VS Code extension.

It exposes the game's virtual filesystem as a VS Code filesystem and provides editor functionality for `.cx` scripts.

It communicates with the running NullByte process rather than accessing game state directly.

It is built using the Node/npm toolchain rather than as part of the .NET solution.

---

## Test projects

Tests are separated according to the component under test.

Prefer testing domain and architectural behavior directly rather than reproducing implementation details in tests.

xUnit is the default test framework.

NSubstitute is acceptable when a substitute makes the test clearer, but prefer real lightweight collaborators where they better demonstrate actual behavior.

---

# 4. Most important architectural invariant: game-thread ownership

NullByte uses a deterministic, effectively single-threaded simulation.

The most important threading rule is:

> Mutable game state belongs to the game thread.

Game state should not be read or mutated directly from arbitrary threads.

This applies to callers such as:

- terminal/input infrastructure
- editor connections
- WebSocket handlers
- Roslyn/background work
- future GUI threads

Work originating outside the game thread must cross the appropriate game-thread boundary before accessing game state.

The design intentionally avoids solving game-state concurrency with locks.

Do not introduce locking around domain state as a shortcut around the game-thread architecture.

---

# 5. Synchronization and asynchronous work

The game uses a custom synchronization model so asynchronous gameplay operations can suspend without blocking the simulation.

Long-running game actions such as:

- Hack
- Grow
- Weaken
- Sleep

are asynchronous game-time operations.

They should not block a real thread for their duration.

Timers and continuations are coordinated with the game clock/scheduler and return execution to the game thread.

An `await` in a player script must therefore allow other scripts, botnet production, timers, and game activity to continue.

Avoid:

- `Thread.Sleep`
- synchronous waits on Tasks
- `.Result`
- `.Wait()`
- blocking the game thread while waiting for external work

unless there is a very specific architectural reason.

---

# 6. Game time versus wall-clock time

NullByte distinguishes game time from real wall-clock time.

Gameplay operations use the game clock.

This makes it possible to:

- accelerate simulation
- test without real delays
- maintain deterministic timer behavior

Some infrastructure intentionally uses wall-clock time instead.

For example, virtual-file-system modification times represent real file-like timestamps and use an injected `TimeProvider`.

Do not assume that every concept of "time" in the application should use the game clock.

Choose the clock according to the semantic meaning of the timestamp.

---

# 7. Game rules should have one implementation

Manual commands and scripts should reach the same underlying game behavior.

For example, there should not be:

- one implementation of Hack for console commands
- another implementation of Hack for scripts

Commands and the scripting API are adapters around shared functionality.

When adding functionality, prefer:

```text
UI / command / script API
        |
        v
shared domain/service operation
        |
        v
game state
```

rather than duplicating game rules in each entry point.

---

# 8. Virtual filesystem

Scripts do not access the user's real filesystem.

Each game server has its own virtual filesystem.

`VirtualFileSystem` owns filesystem rules and should remain the authoritative implementation of file operations.

Consumers include:

- console commands
- scripting
- persistence
- VS Code integration

Do not independently recreate path or filesystem semantics in these consumers when the existing filesystem abstraction can provide them.

Important path rules include:

- `/` is the separator.
- Paths are case-sensitive.
- A leading `/` represents the same server-root-relative path as the equivalent path without it.
- A path may not contain invalid empty segments, backslashes, control characters, or unresolved `.` / `..` segments.
- Paths are limited to 8 levels.
- Paths are limited to 256 characters.
- A path cannot simultaneously represent a file and a directory.

`VirtualPath` is responsible for canonical path handling where applicable.

A failed compound filesystem operation should not leave the filesystem partially modified.

---

# 9. Console current directory versus script paths

The console supports a current working directory.

Console commands may therefore resolve relative paths against the player's current directory.

Scripts do not have this concept.

The following operate from the server root:

- script API file operations
- `nb.Run`
- `nb.Exec`
- editor protocol paths

Do not accidentally apply the console's current-directory semantics to scripts or editor requests.

---

# 10. Player scripting model

Player scripts are C# source files using the `.cx` extension.

They contain top-level statements.

The compiler generates a wrapper implementing the script runtime contract so the player does not explicitly define an entry point.

Two implicit concepts are available to the main script:

- `nb`
- `args`

The scripting experience should feel like ordinary C# while exposing only the intended NullByte API.

Default namespaces/usings are supplied by the compilation pipeline.

Compiler diagnostics should refer to the player's files and line numbers rather than generated wrapper implementation details.

---

# 11. Roslyn compilation pipeline

The compilation pipeline conceptually performs these stages:

1. Resolve script source and includes.
2. Parse source.
3. Validate include/library structure.
4. Generate the script wrapper.
5. Add default usings.
6. Construct the Roslyn compilation with the permitted references.
7. Perform NullByte-specific analysis such as RAM usage.
8. Emit an in-memory assembly.
9. Load through a collectible assembly-load context.
10. Instantiate and execute the generated script.

Diagnostics are part of the public scripting experience and should remain useful to the player.

When changing this pipeline, consider effects on both runtime compilation and editor language services.

The editor deliberately reuses the same compilation concepts so that code accepted by the editor should closely match code accepted by the game.

---

# 12. Includes

NullByte supports reusable script libraries through:

`#include "path"`

Includes are resolved against files on the server where the script is being compiled.

Important rules:

- Includes use server-root-relative paths.
- Includes may themselves include other files.
- Each distinct file is included once.
- Include cycles are errors.
- Include depth is limited to 8.
- Missing includes are compile errors.
- Included libraries may contain using directives and type declarations.
- Included libraries may not contain top-level executable statements.
- Libraries do not receive implicit `nb` or `args`.
- Library methods receive dependencies such as `INullByteApi` explicitly.
- Editing a library does not modify already-running compiled processes.
- `scp` does not implicitly copy included dependencies.

Include diagnostics use the NB1001-NB1006 range defined in the design document.

The compiler, RAM analysis, runtime execution, and language service must interpret includes consistently.

---

# 13. Source snapshots

Game files belong to game state and therefore inherit the game-thread restrictions.

Compilation and language-service work may happen away from the game thread.

Do not allow background compilation to reach directly into live mutable game filesystem state.

Instead, source needed for background work is copied/snapshotted while correctly accessing game state, after which Roslyn can operate on that immutable data off-thread.

Preserve this boundary when extending compilation or editor functionality.

---

# 14. Script RAM

RAM is a gameplay mechanic, not ordinary process memory.

A script has a static RAM cost determined through Roslyn analysis.

The model consists of:

- a base script cost
- costs assigned to exposed API methods
- costs discovered through semantic analysis

RAM-cost metadata is associated with API methods using `RamCost` attributes.

For includes, analysis covers all participating files.

Each distinct charged API method is counted once across the compilation according to the current rules.

Runtime thread count multiplies the script's RAM requirement.

Do not replace semantic RAM analysis with textual searches for method names.

---

# 15. Script processes

Running scripts are game-level processes.

A process has concepts such as:

- PID
- script path
- host
- arguments
- thread count
- RAM consumption
- cancellation/lifetime

Killing a script must eventually release its resources correctly.

Scripts are compiled code. Modifying a source or included library does not mutate code that is already executing.

After persistence restoration, processes may need to be reconstructed/recompiled from the restored source rather than treated like serialized CLR execution state.

---

# 16. Script safety

Player scripts are untrusted from the game's perspective.

The intended sandboxing approach uses:

- curated assembly references
- semantic API restrictions
- analyzers
- prevention of dangerous blocking operations
- eventually protection against synchronous infinite loops

Dangerous capabilities include areas such as:

- arbitrary filesystem access
- networking
- launching processes
- reflection
- arbitrary assembly loading
- uncontrolled threading
- blocking Task operations

This sandbox is intended as hobby-game containment, not as a hardened operating-system security boundary.

Do not accidentally expose an unrestricted runtime API merely because it simplifies a scripting feature.

---

# 17. Editor integration

VS Code integration is optional.

Starting the base game should not require an editor server.

Editor communication uses WebSockets and a JSON protocol.

The editor exposes NullByte's in-game filesystem rather than a real disk directory.

Conceptually:

```text
VS Code extension
        |
    WebSocket
        |
NullByte.Editor
        |
 game-thread boundary
        |
VirtualFileSystem / game state
```

Language-service operations may perform expensive Roslyn work away from the game thread.

Short operations required to obtain game-state-backed data must first cross the game-thread boundary and snapshot the required information.

---

# 18. Editor security

The editor server is a local integration.

Important properties include:

- loopback-only listening
- per-run authentication token
- connection metadata written to a connection file
- browser-style connections rejected through Origin checks
- cleanup of the owned connection file when shutting down

Do not remove these checks merely to simplify editor connectivity.

As with script sandboxing, this is local hobby-project protection and not intended to be a hardened security boundary.

---

# 19. Editor protocol consistency

The VS Code extension and `NullByte.Editor` form two sides of the same protocol.

Changes to protocol operations or payloads therefore normally require considering both projects.

Current protocol responsibilities include operations such as:

- file stat
- directory listing
- reading files
- writing files
- creating directories
- deleting
- renaming
- diagnostics
- completion
- hover
- signature help
- definition

The server can also push change/reset notifications.

When modifying an operation, inspect both the C# server implementation and TypeScript client before assuming a payload shape.

Do not guess the protocol from one side alone.

---

# 20. Persistence

Game persistence uses an explicitly versioned save format.

Changes to persistent domain models must consider:

- serialization
- deserialization
- migration
- validation
- autosaves
- manual saves
- golden-file tests

Do not casually change serialized structures under the assumption that ordinary JSON serialization details are unimportant.

Backward compatibility is intentional.

Current save evolution includes migrations for file timestamps and virtual directories.

Damaged or invalid persisted data should be rejected rather than silently transformed into ambiguous game state.

---

# 21. Reset behavior

Reset/new-game behavior represents a true replacement of the running game state rather than piecemeal mutation of the old world.

Reset behavior must consider lifecycle responsibilities such as:

- active game activity
- running processes
- autosave handling
- host/session state
- editor notification
- references to the previous game state

External integrations that observe the active game must be notified when the game instance/state they were watching has been replaced.

Be especially careful about stale event subscriptions when replacing game state.

---

# 22. Application settings

Application startup settings and game balance are separate concepts.

Do not merge infrastructure configuration into gameplay balance merely because both may originate from configuration data.

Startup/settings handling should have clear responsibilities for:

- command-line parsing
- settings-file loading
- merging CLI overrides
- validation
- writing/default settings where supported
- applying the resulting settings during host startup

Keep parsing, validation, and runtime behavior separate enough that each remains testable.

---

# 23. Activities

The game supports longer-running player activities such as work or study.

Activity architecture must respect the same simulation principles as hacking operations:

- game state remains owned by the game thread
- elapsed progress uses the appropriate game-time model
- activities should coexist correctly with background simulation
- transitions between active/inactive activities must maintain valid state

Before introducing a new form of player activity, inspect the existing activity abstractions rather than creating an unrelated second lifecycle model.

---

# 24. Console command architecture

Console commands are grouped by responsibility.

The command framework includes concepts for:

- command definitions
- registry
- dispatch
- parsing
- command groups
- argument completion

When introducing a command:

1. Reuse the existing command registration model.
2. Keep parsing and presentation in the command layer.
3. Delegate game behavior to the relevant underlying service/domain abstraction.
4. Respect game-thread assumptions.
5. Add completion behavior when the command benefits from it.
6. Test meaningful parsing/behavior paths where practical.

Do not evolve individual commands into isolated miniature architectures.

---

# 25. Testing philosophy

NullByte should remain highly testable.

Important techniques include:

- xUnit
- deterministic clocks
- seeded randomness
- real lightweight domain objects
- NSubstitute where isolation is useful
- golden persistence tests
- compiler tests using real Roslyn behavior
- integration-style tests for important architectural boundaries

Important test areas include:

- formulas
- game-state invariants
- game-thread execution
- scheduling
- virtual filesystem rules
- persistence and migrations
- command dispatch
- path resolution
- Roslyn compilation
- includes
- RAM analysis
- script process lifetime
- language service
- editor protocol

Avoid tests that merely duplicate implementation logic.

Prefer observable behavior.

When fixing a defect, add a regression test when the failure can reasonably be reproduced in an automated test.

---

# 26. Determinism

Determinism is an important architectural property.

Where gameplay involves randomness, prefer injected/controlled random sources so tests can reproduce outcomes.

Where gameplay involves time, prefer the game clock rather than reading wall-clock time directly.

Avoid introducing hidden global sources of:

- time
- randomness
- mutable state

into Core gameplay logic.

---

# 27. Dependency direction

When adding functionality, preserve clean dependency direction.

In particular:

- Core must not depend on ConsoleUI.
- Core must not depend on VS Code.
- Core must not depend on presentation-specific behavior.
- Scripting may depend on the Core contracts needed to expose gameplay to scripts.
- Editor may depend on Core and Scripting.
- Hosts may wire the components together.

Before adding a project reference, consider whether the dependency could instead be represented by an interface at the lower-level boundary.

Avoid introducing abstractions solely for architectural purity, however. Use an interface when there is an actual boundary, testing need, or interchangeable implementation.

---

# 28. Development style

Favor explicit, comprehensible code over elaborate abstraction.

When extending the architecture:

- reuse existing concepts where they fit
- avoid parallel implementations of the same idea
- keep responsibilities narrow
- make thread ownership obvious
- prefer immutable snapshots across thread boundaries
- keep game rules centralized
- preserve deterministic testing
- avoid speculative infrastructure for features that do not exist yet

Do not perform broad refactors while implementing an unrelated feature unless the existing structure blocks a correct implementation.

If a larger refactor appears worthwhile, identify it separately and explain why it is needed.

---

# 29. Working with this codebase as an AI

Do not assume APIs from architectural descriptions alone.

Before proposing exact code changes:

1. Identify the subsystem involved.
2. Inspect its current implementation.
3. Inspect interfaces and directly relevant dependencies.
4. Inspect existing tests.
5. Follow existing naming and structural conventions.
6. Then propose the change.

The source code is authoritative for the current implementation.

The Game Design Document is authoritative for intended behavior.

If they disagree, explicitly point out the discrepancy rather than silently choosing one.

Do not invent methods, constructors, properties, or interfaces that have not been verified in the current codebase.

---

# 30. Scope source files deliberately

The entire repository normally does not need to be supplied for an individual development task.

Start from:

- this document
- the Game Design Document
- the repository/codebase map
- current project status

Then identify the implementation files relevant to the task.

For implementation work, request or inspect:

1. primary implementation files
2. their interfaces/contracts
3. directly involved dependencies
4. relevant tests

Expand outward only when the implementation reveals another necessary dependency.

Whenever a file is going to be modified, use its current repository version rather than relying on an older copy from previous conversations.

---

# 31. Before changing architecture

For a meaningful architectural change, first establish:

### Current architecture

How does the relevant feature work today?

### Desired behavior

What user/game behavior must change?

### Invariants

Which rules from this document must remain true?

### Target architecture

Which components should own the new responsibilities?

### Migration

Can the architecture be introduced incrementally without destabilizing unrelated systems?

Do this analysis before producing a large code drop.

For smaller, local modifications, avoid unnecessary architectural ceremony.

---

# 32. Important invariants checklist

Before considering a significant change complete, check the relevant items:

- Game state is only accessed through the correct game-thread boundary.
- Gameplay rules are not duplicated between manual and scripted entry points.
- Long-running game actions do not block the game thread.
- Game-time behavior is deterministic/testable.
- Virtual filesystem rules remain centralized.
- Background Roslyn/editor work does not directly access mutable game state.
- Script diagnostics still map to player-visible files and positions.
- Script RAM semantics remain consistent.
- Included files behave consistently across runtime compilation and editor tooling.
- Editor protocol changes are reflected on both sides.
- Persistent-model changes preserve or migrate older saves.
- Reset/load does not leave stale observers attached to replaced game state.
- New Core code does not acquire presentation dependencies.
- Existing tests still pass.
- New behavior has appropriate tests.

---

# 33. Current development direction

The project has progressed well beyond the initial foundational milestones.

Implemented areas represented in the current codebase include substantial portions of:

- game loop and synchronization
- network and hacking mechanics
- console interface
- botnet
- Roslyn scripting
- processes and script RAM
- save/load
- virtual filesystem
- VS Code integration
- script includes
- activities
- editor-integration improvements

The current design milestone is M11, focused on improvements to VS Code/editor integration.

M11 targets include:

- debugging/inspecting external editor connections
- improved diagnostic ranges
- running scripts from VS Code
- querying script RAM from VS Code
- launching VS Code from the game

Do not assume every listed M11 item has already been implemented. Inspect the current source before determining milestone status.

Later planned work includes stronger script sandboxing and safety, progression/balance work, and the first playable release.

---

# 34. Guiding principle

When uncertain between a convenient implementation and one that preserves the existing architecture, prefer the architecture unless there is a concrete reason to change it.

In particular, preserve these three properties:

> The game simulation has one owner thread.

> Manual and scripted gameplay use the same underlying rules.

> The scripting and editor systems adapt to the game domain rather than reshaping the game domain around their infrastructure.

These constraints are foundational to NullByte and should only change as an explicit architectural decision.