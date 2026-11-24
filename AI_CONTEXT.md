# NullByte AI Development Context

Read this file before modifying NullByte.

Use the other context documents as needed:

- `CURRENT_STATUS.md` - current milestone, recent work and known issues.
- `CODEBASE.md` - where relevant implementation and tests live.
- `ARCHITECTURE.md` - system structure, ownership and data flows.
- `DESIGN.md` - authoritative gameplay behavior, product decisions and roadmap.

The current source code is authoritative for implementation details.

---

# 1. Project

NullByte is a text-driven incremental hacking game inspired by Bitburner.

The player begins with manual terminal commands and increasingly automates gameplay using C# `.cx` scripts compiled at runtime with Roslyn.

Core principle:

> Scripting is the game.

Manual commands and scripts should use the same underlying gameplay rules.

Technologies include:

- C# / .NET 10
- Roslyn
- xUnit
- NSubstitute where useful
- TypeScript for the VS Code extension
- WebSockets between the game and VS Code

---

# 2. Critical invariants

## Game-thread ownership

Mutable game state belongs to the game thread.

External threads, editor requests and background work must cross the appropriate game-thread boundary before accessing it.

Do not introduce locks around domain state to bypass this model.

## Shared game mechanics

Console commands and script APIs are adapters around shared game behavior.

Do not create separate implementations of the same gameplay rule for ConsoleUI, scripts or VS Code.

## Async gameplay

Long-running game operations suspend without blocking simulation.

Avoid:

- `Thread.Sleep`
- `.Result`
- `.Wait()`
- synchronous waits on the game thread

Gameplay timing should use the game clock/scheduler where appropriate.

## Determinism

Prefer injected time and randomness.

Avoid hidden global sources of time, randomness or mutable state in gameplay logic.

## Virtual filesystem

Player files belong to NullByte's virtual filesystem, not the host filesystem.

Reuse `VirtualFileSystem` and `VirtualPath` semantics rather than recreating path/file rules in individual consumers.

Console paths may use the player's current directory.

Scripts and editor operations use server-root-relative paths.

## Thread-boundary snapshots

Background Roslyn/editor work must not directly access mutable game state.

Acquire required data through the game-thread boundary, create an immutable snapshot, then perform expensive work off-thread.

---

# 3. Dependency rules

Keep dependency direction clean.

- `NullByte.Core` owns domain and simulation.
- `NullByte.Scripting` owns compilation/runtime tooling around the Core scripting API.
- `NullByte.Shell` owns reusable session/persistence lifecycle.
- `NullByte.Editor` adapts game/scripting functionality for external editors.
- `NullByte.ConsoleUI` hosts the terminal application.
- `NullByte.vscode` is an external TypeScript client.

Core must not depend on presentation, VS Code or terminal behavior.

Before adding a project reference, check whether responsibility belongs at a lower existing boundary.

Do not create abstractions solely for architectural purity. Add them when they represent a real boundary, lifecycle, interchangeability or testing need.

---

# 4. Scripting rules

Player `.cx` scripts are compiled with Roslyn.

Main scripts receive implicit:

- `nb`
- `args`

Reusable libraries use `#include`.

Compilation, runtime RAM analysis and editor language services must interpret scripts consistently.

When changing scripting semantics, consider all affected paths:

- compilation
- diagnostics
- includes
- RAM
- runtime
- language service

Do not expose unrestricted CLR capabilities merely because they simplify an API.

RAM is a gameplay resource calculated semantically from script API usage, not actual CLR memory consumption.

Running scripts contain compiled code. Editing their source or included libraries does not mutate already-running processes.

---

# 5. Persistence and lifecycle

Persistent formats are versioned.

Changes to persisted state require explicit consideration of:

- mapping
- serialization
- migration
- validation
- golden tests

Reset means creating a genuinely new game/session, not clearing fields on the old `GameState`.

When replacing game/session state, consider:

- running processes
- activities
- autosave
- lifecycle/disposal
- external observers
- stale event subscriptions
- editor reset/change notifications

Host-level operations such as load, reset and shutdown should use the established request/mailbox model rather than introducing independent polling flags.

Inspect the current implementation before extending that model.

---

# 6. Editor integration

VS Code integration is optional.

The base game must work without an editor server.

`NullByte.Editor` and `NullByte.vscode` are opposite sides of the same protocol.

When changing a protocol operation, inspect both C# and TypeScript implementations.

Editor operations accessing game state must respect game-thread ownership.

Editor connection/security behavior should preserve:

- loopback-only communication
- per-run authentication
- connection discovery
- Origin checks
- connection-file cleanup

Do not remove these merely to simplify connectivity.

---

# 7. Testing

Use xUnit.

Use NSubstitute when substitution materially improves the test; prefer real lightweight collaborators when they better demonstrate behavior.

Prefer observable behavior over implementation-detail tests.

Important techniques include:

- fake game clocks
- deterministic randomness
- game-thread harnesses
- real Roslyn compilation
- golden persistence tests
- integration/acceptance tests for cross-component behavior

Fixes should normally include regression tests when practical.

After structural changes, run the relevant project tests and preferably the full solution test suite.

---

# 8. How to approach development tasks

Before writing exact code:

1. Read `CURRENT_STATUS.md`.
2. Use `CODEBASE.md` to identify the subsystem.
3. Inspect the current primary implementation files.
4. Inspect relevant interfaces/contracts.
5. Inspect relevant tests.
6. Load additional dependencies only when discovered to be necessary.
7. Establish the target design.
8. Implement incrementally.
9. Add/update tests.
10. Update documentation if architecture, design or status changed.

Do not assume APIs from previous conversations or documentation.

Before modifying a file, use its current repository version.

Never invent constructors, methods, properties or interfaces and present them as existing code.

If documentation and source disagree, identify the discrepancy explicitly.

---

# 9. Change discipline

Prefer the smallest coherent change that solves the current requirement.

Reuse existing concepts where they fit.

Avoid:

- parallel implementations of existing concepts
- speculative infrastructure
- unrelated refactoring
- unnecessary abstractions
- presentation logic in Core
- game rules in commands/editor code
- blocking the game thread
- direct background access to game-owned state

If the existing architecture prevents a correct implementation, explain why and agree on the architectural change before producing a large code drop.

---

# 10. Implementation style

Favor explicit, readable C# over clever abstraction.

Match existing:

- naming
- namespace organization
- dependency style
- result/error patterns
- nullable conventions
- test structure

For guided implementation, work incrementally and file-by-file.

Explain:

- what each change does
- why the responsibility belongs there
- how it interacts with existing architecture
- what tests validate it

When providing code, keep ordinary lines intact unless they become meaningfully long.

---

# 11. Completion checklist

For significant changes, verify the relevant items:

- game-thread ownership remains correct
- game rules are not duplicated
- async gameplay does not block simulation
- time/randomness remain testable
- virtual filesystem semantics remain centralized
- background work uses snapshots where required
- scripting/compiler/editor semantics remain aligned
- RAM behavior remains consistent
- protocol changes cover C# and TypeScript sides
- persistence changes include migration considerations
- replaced state leaves no stale observers
- dependency direction remains valid
- relevant tests pass
- current documentation is updated when necessary

---

# 12. Guiding rules

When uncertain, preserve these principles:

> Mutable game state has one owner thread.

> Manual and scripted gameplay use the same underlying rules.

> Infrastructure adapts to the game domain, not the other way around.

> Inspect current code before prescribing exact implementation.

> Load only the source required for the task.