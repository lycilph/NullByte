# NullByte Current Status

Working snapshot of current NullByte development.

Update after completing a deliverable or changing development focus.

See:

- `AI_CONTEXT.md` - development rules
- `CODEBASE.md` - source/test navigation
- `ARCHITECTURE.md` - system structure
- `DESIGN.md` - intended behavior and roadmap

Current source is authoritative if this file becomes stale.

---

# Current focus

**Milestone:** M11 - VS Code editor integration improvements

**Current deliverable:** M11.1 - Debug command for connected external editors

Goal:

Allow the in-game debug command system to show all currently connected external editor clients.

Expected behavior:

- list every active editor connection
- report clearly when none are connected
- obtain live state from the editor subsystem
- avoid maintaining a duplicate connection registry

---

# Current issue

While integrating editor connection information into `DebugCommands`, the supplied delegate/provider was observed as `null` during command registration/composition.

Before continuing, inspect the current versions of:

```text
NullByte.ConsoleUI/Commands/Groups/DebugCommands.cs
NullByte.ConsoleUI/Commands/CommandSetup.cs
NullByte.ConsoleUI/GameHost.cs
NullByte.Editor/EditorServer.cs
NullByte.Editor/EditorConnection.cs
```

Do not continue from an assumed API.

Trace the current composition path first and resolve the lifetime/wiring issue rather than introducing global/static connection state.

The final abstraction should expose only the read-only connection information required by the command.

---

# M11 roadmap

## M11.1 - Connected editor debug command

**Status:** In progress

Current task described above.

## M11.2 - Full-range diagnostics

**Status:** Planned

Improve VS Code diagnostics so the relevant source range is underlined rather than only a partial position.

Likely areas:

```text
ScriptDiagnostic.cs
ScriptCompiler.cs
ScriptLanguageService.cs
EditorProtocol.cs
diagnostics.ts
```

## M11.3 - Run current script from VS Code

**Status:** Planned

Add editor protocol/VS Code commands for executing the currently open `.cx` file through the existing `ScriptService`.

Do not create an editor-specific execution path.

## M11.4 - Get RAM from VS Code

**Status:** Planned

Expose existing script RAM measurement through the editor protocol and VS Code.

Reuse `ScriptService`/existing RAM analysis and preserve include behavior and diagnostics.

## M11.5 - Launch VS Code from the game

**Status:** Planned

Add a host-level command for launching VS Code into the NullByte editing workflow.

Keep external-process launching outside Core.

---

# Recently completed

## Reset / new game

Implemented as a true replacement of game/session state rather than field-by-field clearing.

The completed design includes:

- new game state/session
- autosave deletion
- lifecycle cleanup
- editor notification
- restrictions around incompatible active activity

Verify exact APIs against current source before modifying this flow.

## Host request/mailbox architecture

Host-directed lifecycle requests were consolidated around a common mailbox/request concept instead of independent polling mechanisms.

Use the current implementation for operations such as load, reset and shutdown.

## Application settings

Completed settings architecture includes:

- command-line parsing
- `settings.json`
- settings beside the executable
- CLI overrides
- centralized validation
- settings-file writing
- refactored startup
- unit tests

Application settings remain separate from gameplay balance.

## Script includes

Implemented including:

- recursive includes
- cycle/depth validation
- compilation
- RAM analysis
- language-service support
- tests

---

# Established foundation

The following major capabilities are already present and should normally be treated as existing systems rather than redesigned during unrelated work:

- single-threaded simulation
- Hack/Grow/Weaken
- console command system
- virtual filesystem
- Botnet
- Roslyn `.cx` scripting
- script processes/threads/RAM
- persistence and autosave
- VS Code virtual filesystem
- VS Code language services
- script includes/libraries
- activities/jobs/training

See `CODEBASE.md` for their implementation locations.

---

# Next milestone

**M12 - Sandbox and safety**

Not started.

Expected areas include:

- restricted references/APIs
- semantic analyzers
- blocking-operation prevention
- synchronous infinite-loop protection
- script failure containment

Do not start M12 implicitly while completing M11 unless required for correctness.

---

# Immediate next steps

1. Inspect current M11.1 implementation files.
2. Trace `DebugCommands` creation and registration.
3. Determine why the connection-information provider becomes `null`.
4. Settle the smallest correct read-only boundary.
5. Implement connection listing.
6. Verify zero, one and multiple connections.
7. Add tests where practical.
8. Mark M11.1 complete here.
9. Proceed to M11.2.

---

# Documentation update rule

After completing meaningful work:

- update this file for progress/blockers
- update `CODEBASE.md` only if important entry points move
- update `ARCHITECTURE.md` only for architectural changes
- update `DESIGN.md` only for product/game-design decisions
- update `AI_CONTEXT.md` only when development invariants change

---

# Last updated

2026-11-24

**Active:** M11.1 - connected external editor debug command.