# NullByte Development Guide

Architectural rules live in `ARCHITECTURE.md`; this file covers the workflow, tooling and conventions.

## Build, run and test

```text
dotnet build NullByte.slnx
dotnet test NullByte.Core.Tests          # or any other NullByte.*.Tests project
dotnet run --project NullByte.ConsoleUI  # run the game
```

- All .NET projects target `net10.0`, except the WPF shell `NullByte`, which targets `net10.0-windows`. Building the whole solution therefore needs Windows; on other platforms build the individual projects.
- The VS Code extension is built separately from `NullByte.vscode/`: `npm run compile`, `npm test`, `npm run package`.

## Conventions

- Nullable reference types and implicit usings are enabled in every project.
- Tests use xUnit v3. NSubstitute is available in `NullByte.Core.Tests` and `NullByte.Scripting.Tests`.
- Test names follow `Subject_Condition_Result` (for example `Post_FromWorkerThread_ExecutesOnGameThread`); command tests are sentences (see Testing principles).
- Test folders mirror the production project's folders. Shared helpers such as `GameThreadHarness` live in the test project's `Infrastructure/` folder.
- Production assemblies expose internals to their test project through `InternalsVisibleTo`.
- Use `FakeGameClock` and other injected seams rather than real delays.

## Before changing code

Follow the context-loading rule at the end of `CODEBASE.md`: `ARCHITECTURE.md`, the relevant part of `CODEBASE.md`, the implementation files, their tests, and `DECISIONS.md` if an architectural choice is involved.

Identify the smallest coherent working set:

- the owning Core domain/service code;
- the adapter(s) affected by the feature;
- the closest tests;
- persistence/migration code only when state shape changes;
- editor code only when the external-editor contract changes.

The repository is the authoritative implementation; the documentation is a map.

## Where planned work lives

- `ROADMAP.md` — milestones (player-visible capabilities).
- `todo.md` — current tasks, upcoming cleanup and future ideas.
- `NullByte Game Design Document.md` — detailed design. It is large; read only the relevant section.

## Feature workflow

### 1. Establish the behavior

Define what the player should observe, including timing, persistence, failure cases and interactions with existing systems.

### 2. Locate the existing mechanism

Search for the nearest existing feature with similar behavior. Reuse its service, lifecycle, formula, scheduler or persistence mechanism where appropriate.

### 3. Design the smallest coherent change

Keep gameplay rules in Core and adapters thin. Avoid introducing a second mechanism for a problem the architecture already solves.

### 4. Implement incrementally

A useful order is:

1. domain/state;
2. Core service/formula/system;
3. scripting API if required;
4. console/editor adapter if required;
5. persistence if required;
6. tests.

### 5. Test at the owning layer

Add unit tests where the behavior is owned, then add integration coverage for important boundaries.

### 6. Review the architecture

Check the change against the warning signs in `ARCHITECTURE.md`.

### 7. Update documentation

- `CODEBASE.md` when structure or entry points change.
- `ARCHITECTURE.md` when boundaries or responsibilities change.
- `DECISIONS.md` when a significant architectural choice is made.
- `ROADMAP.md` when a milestone changes.

## Common change patterns

### Gameplay feature

Use the existing Core domain/service/formula structure. Then expose the capability to console/scripts/editor as needed.

### Script feature

Trace:

```text
source → compiler/analysis → runtime → INullByteApi → Core
```

Check both compile-time and runtime behavior.

### Editor feature

Trace:

```text
VS Code → protocol → NullByte.Editor → game-thread boundary → Core/Scripting
```

### Persistence feature

Trace:

```text
GameState ↔ SaveGameMapper ↔ SaveGame ↔ serializer ↔ SaveManager ↔ store
```

If the saved representation changes, add a migration and golden-file coverage.

### Activity feature

Prefer the existing ActivityService/System lifecycle. Decide explicitly whether the activity supports focused mode, background mode or both.

## Testing principles

- Test deterministic outcomes, not wall-clock timing.
- Test save migrations against representative old saves.
- Test scripting behavior at both analysis/compilation and runtime boundaries when applicable.
- Test editor behavior through the editor service/protocol boundary where useful.
- Keep unit tests close to the layer that owns the behavior.
- Put shared test setup in the project's `Infrastructure/` folder instead of building sessions and fixtures inside test classes. Console tests start from `ConsoleTestSetup.CreateHarness`; Core tests start from `CoreTestSetup.CreateWorld` (with `TestServers` for domain-level tests and `GameLoopHost` for the real-thread game loop tests); Scripting tests start from `ScriptTestSetup.CreateWorld` (with `ScriptApiProbe` standing in for the player API, and `TestScripts` / `CompileAssert` for compiling).
- Use NSubstitute for collaborators a test only needs to stub or verify (for example `INullByteApi` or `ISaveStore`). Keep hand-written fakes (`FakeTerminal`, `FakeGameClock`, `ScriptedRandom`, in-memory stores) where the test asserts on their recorded state.
- Do not touch the real file system or wait on real time in a test unless that is the behavior under test; use `TemporaryDirectory`, `FakeTimeProvider` or a fake clock.
- Name command tests as sentences (`Rm_deletes_a_file_but_explains_directories`) and unit tests as `Subject_Condition_Result`.

## AI-assisted development workflow

1. Use the architecture documents to establish the relevant boundaries.
2. Use `CODEBASE.md` to identify likely files.
3. Inspect only those files and their tests (through the GitHub integration where available).
4. Discuss and confirm the design when the change affects architecture or player-visible behavior.
5. Implement the smallest coherent change.
6. Run/review relevant tests.
7. Inspect the resulting diff.
8. Update documentation if needed.

Never commit without explicit approval. Avoid attaching full Repomix exports; targeted inspection keeps the context focused and the current repository state authoritative. `AIPrompts.md` has a starting prompt and Repomix exclusion settings.

## Completion checklist

Before declaring a change complete:

- [ ] Behavior is implemented in the correct layer.
- [ ] Existing mechanisms were reused where appropriate.
- [ ] Relevant tests were added/updated.
- [ ] Timing is deterministic.
- [ ] Persistence is compatible or migrated.
- [ ] Console/script/editor paths use shared Core behavior.
- [ ] No game-thread blocking was introduced.
- [ ] Documentation reflects any structural/architectural change.
- [ ] The final diff contains only intended changes.
