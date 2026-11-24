# NullByte AI Development Context

Read this file before modifying NullByte.

Use the other context documents as needed:

- `CURRENT_STATUS.md` - current milestone and next work.
- `CODEBASE.md` - source/test navigation.
- `ARCHITECTURE.md` - system structure, ownership and data flows.
- `DESIGN.md` - authoritative gameplay behavior and roadmap.

Current source is authoritative for implementation details.

## 1. Project

NullByte is a text-driven incremental hacking game inspired by Bitburner. The player starts with manual terminal commands and increasingly automates gameplay using C# `.cx` scripts compiled at runtime with Roslyn.

Core principle: **Scripting is the game.** Manual commands and scripts should use the same underlying gameplay rules.

Stack: C#/.NET 10, Roslyn, xUnit, TypeScript/VS Code, WebSockets.

## 2. Critical invariants

### Game-thread ownership

Mutable game state belongs to the game thread. External threads, editor requests and background work must cross the appropriate game-thread boundary before accessing it. Do not add locks around domain state to bypass this model.

### Shared game mechanics

Console commands and script APIs adapt shared game behavior. Do not implement separate gameplay rules for ConsoleUI, scripts or VS Code.

### Async gameplay and determinism

Long-running game operations suspend without blocking simulation. Avoid `Thread.Sleep`, `.Result`, `.Wait()` and synchronous waits on the game thread. Gameplay time uses the game clock/scheduler. Prefer injected time and randomness.

### Virtual filesystem

Player files belong to NullByte's virtual filesystem. Reuse `VirtualFileSystem`/`VirtualPath`. Console paths may use the player's current directory; scripts and editor operations use server-root-relative paths.

### Background work

Roslyn/editor work must not access mutable game state directly. Snapshot required data through the game-thread boundary, then do expensive work off-thread.

## 3. Dependency rules

- `NullByte.Core`: domain, services, formulas, simulation, persistence models and script-facing APIs.
- `NullByte.Scripting`: compilation, analysis, loading, runtime and language services.
- `NullByte.Shell`: session and persistence lifecycle.
- `NullByte.Editor`: external-editor adapter/protocol.
- `NullByte.ConsoleUI`: terminal host and composition.
- `NullByte.vscode`: external TypeScript client.

Core must not depend on presentation/editor infrastructure. Add abstractions when they represent a real boundary, lifecycle or test seam, not for architectural purity alone.

## 4. Scripting rules

Main `.cx` scripts receive implicit `nb` and `args`. Libraries use `#include` and receive dependencies explicitly. Compilation, RAM analysis and language services must model scripts consistently.

RAM is gameplay cost calculated semantically from API usage. Running processes contain compiled code; later source/library edits do not mutate them.

Do not expose unrestricted CLR capabilities merely because they simplify an API.

## 5. Persistence and lifecycle

Persistent formats are versioned. Persistent-state changes require mapping, serialization, migration, validation and tests.

Reset is true session replacement, not field clearing. On replacement consider processes, activities, autosave, disposal, observers, editor notifications and stale subscriptions.

Host-level transitions (`Shutdown`, `Load`, `Reset`) use the closed `HostRequest` hierarchy and host-owned atomic single-slot `HostRequestMailbox`. Commands request transitions; `GameHost` executes them outside the session command.

Application settings are host configuration, not saved game state. Effective settings are defaults -> optional `settings.json` -> CLI overrides -> centralized validation.

## 6. Editor integration

VS Code integration is optional. `NullByte.Editor` and `NullByte.vscode` are two sides of one protocol; inspect both when changing it.

Editor game-state access respects the game thread. Preserve loopback-only communication, per-run authentication, Origin checks, connection discovery and cleanup.

`IExternalEditorStatus` provides the ConsoleUI a read-only view of live editor connections without exposing transport internals.

Editor script execution and RAM queries reuse `IScriptService`; do not create editor-specific scripting behavior.

## 7. Activities

Activities share one lifecycle and may run focused or background. Current activities include study/training, jobs and crimes. Crimes are repeated timed attempts with success chance/rewards and use the same activity simulation/persistence model.

Do not create a parallel lifecycle for new long-running player activities when the existing activity model fits.

## 8. Testing

Use xUnit. Use NSubstitute only when it makes the test clearer; prefer lightweight real collaborators where practical.

Prefer observable behavior. Use fake clocks, deterministic randomness, game-thread harnesses, real Roslyn tests, persistence migrations/golden tests and integration/acceptance tests where appropriate. Bugs should normally receive regression tests.

## 9. Development workflow

Before writing exact code:

1. Read `CURRENT_STATUS.md`.
2. Use `CODEBASE.md` to identify the subsystem.
3. Inspect current implementation, contracts and relevant tests.
4. Load additional dependencies only when needed.
5. Agree on the target design before large changes.
6. Implement incrementally and test.
7. Update context documents only where their owned information changed.

Never invent existing constructors, methods, properties or interfaces. If docs and code disagree, call it out. Always use the current version of a file before modifying it.

## 10. Change discipline

Prefer the smallest coherent change. Reuse existing concepts. Avoid parallel mechanisms, speculative infrastructure, unrelated refactors, presentation logic in Core, duplicated rules, blocking the game thread and direct background access to game state.

For guided implementation, work file-by-file and explain responsibility, interaction and tests.

## 11. Guiding rules

> Mutable game state has one owner thread.

> Manual and scripted gameplay use the same underlying rules.

> Infrastructure adapts to the game domain, not the other way around.

> Inspect current code before prescribing exact implementation.

> Load only the source required for the task.
