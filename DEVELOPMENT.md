# NullByte Development Guide

## Before changing code

Start with:

1. `ARCHITECTURE.md`
2. The relevant section of `CODEBASE.md`
3. Current implementation files
4. Relevant tests
5. `DECISIONS.md` when an architectural choice is involved

Do not begin by loading the entire repository into context unless the change genuinely crosses most of the architecture.

## Working-set principle

For a normal feature or bug fix, identify the smallest coherent working set:

- the owning Core/domain/service code;
- the adapter(s) affected by the feature;
- the closest tests;
- persistence/migration code only when state shape changes;
- editor code only when the external-editor contract changes.

The repository is the authoritative implementation. The documentation is a map.

## Feature workflow

### 1. Establish the behavior

Define what the player should observe, including timing, persistence, failure cases and interactions with existing systems.

### 2. Locate the existing mechanism

Search for the nearest existing feature with similar behavior. Reuse its service, lifecycle, formula, scheduler or persistence mechanism where appropriate.

### 3. Design the smallest coherent change

Keep gameplay rules in Core. Keep adapters thin. Avoid introducing a second mechanism for a problem the architecture already solves.

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

Prefer fake clocks and deterministic seams over real delays.

### 6. Review the architecture

Before considering the change complete, check for:

- duplicated gameplay rules;
- game-thread violations;
- blocking waits;
- direct mutable-state access from background work;
- duplicated path handling;
- ad-hoc persistence;
- unnecessary new lifecycle mechanisms.

### 7. Update documentation

Update `CODEBASE.md` when structure or entry points change.

Update `ARCHITECTURE.md` when boundaries or responsibilities change.

Update `DECISIONS.md` when a significant architectural choice is made.

Update `ROADMAP.md` when a milestone changes.

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

Do not put game rules in the editor protocol implementation.

### Persistence feature

Trace:

```text
GameState ↔ SaveGameMapper ↔ SaveGame ↔ serializer ↔ SaveManager ↔ store
```

If the saved representation changes, consider migration and golden-file coverage.

### Activity feature

Prefer the existing ActivityService/System lifecycle. Decide explicitly whether the activity supports focused mode, background mode or both.

## Testing principles

- Test deterministic outcomes, not wall-clock timing.
- Use `FakeGameClock` where timing matters.
- Test save migrations against representative old saves.
- Test scripting behavior at both analysis/compilation and runtime boundaries when applicable.
- Test editor behavior through the editor service/protocol boundary where useful.
- Keep unit tests close to the layer that owns the behavior.

## Things to avoid

- Implementing a gameplay rule in a UI command.
- Implementing a gameplay rule only in `NullByteApi`.
- Adding a separate editor-only implementation of a Core operation.
- Using `Thread.Sleep`, `.Wait()` or `.Result` on the game thread.
- Making tests depend on real elapsed time.
- Serializing live execution objects.
- Maintaining parallel file-system representations.
- Creating a new activity framework for one activity.
- Passing mutable GameState into background Roslyn/editor work.

## AI-assisted development workflow

The preferred workflow for work assisted by ChatGPT is:

1. Use the architecture documents to establish the relevant boundaries.
2. Use `CODEBASE.md` to identify likely files.
3. Inspect only those files and their tests from GitHub.
4. Discuss/confirm the design when the change affects architecture or player-visible behavior.
5. Implement the smallest coherent change.
6. Run/review relevant tests.
7. Inspect the resulting diff.
8. Update documentation if needed.

Avoid repeatedly attaching full Repomix exports. GitHub-backed targeted inspection keeps the context focused and makes the current repository state authoritative.

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
