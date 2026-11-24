# NullByte Current Status

Working snapshot of current development. Source is authoritative if this file becomes stale.

See `AI_CONTEXT.md`, `CODEBASE.md`, `ARCHITECTURE.md` and `DESIGN.md` for stable context.

## Current focus

**Milestone:** M11 - VS Code editor integration improvements.

Three M11 deliverables are complete:

- connected external-editor debug status
- run current script from VS Code
- query current script RAM from VS Code

Remaining M11 work:

1. launch VS Code from the game
2. full-range diagnostic underlines

No known implementation blocker is recorded for the next deliverable. Before starting it, inspect the current M11 implementation rather than assuming the earlier pre-merge APIs.

## Completed M11 work

### External editor status

`DebugCommands` now exposes `editor`. ConsoleUI receives the nullable `IExternalEditorStatus` abstraction rather than editor transport internals. It distinguishes integration disabled, no connected editors and active connections.

Relevant files:

```text
DebugCommands.cs
CommandSetup.cs
EditorServer.cs
IExternalEditorStatus.cs
ExternalEditorStatus.cs
ExternalEditorConnectionInfo.cs
```

### Run from VS Code

The editor stack can execute a `.cx` file through the existing scripting runtime. Editor-side execution reuses `IScriptService` rather than implementing a second execution path.

Relevant files:

```text
EditorFileService.cs
EditorProtocol.cs
IScriptService.cs
ScriptService.cs
commands.ts
runCodeLens.ts
package.json
```

### RAM from VS Code

The extension can query the current script's RAM using the existing script RAM/compilation behavior, preserving normal diagnostics and include semantics.

## Recently merged gameplay work

M10 activities now include crimes in addition to study/training and jobs.

Crime support includes:

- crime definitions/catalog data
- repeated timed crime attempts
- success chance and rewards
- focused/background activity modes
- activity UI/progress reporting
- persistence support

Relevant additions include `Crime.cs`, `CrimeActivity.cs`, `CrimeAttempt.cs` and `CrimeRewards.cs`.

Persistence is now at save format 4 with `V3ToV4Migration`, and activity persistence has dedicated tests.

## Established host work

The current codebase includes:

- closed `HostRequest` hierarchy for shutdown/load/reset
- atomic single-slot `HostRequestMailbox`
- true reset/session replacement
- autosave deletion during reset
- optional `settings.json`
- CLI overrides and centralized validation
- `--write-default-settings`

Configuration files live under `NullByte.ConsoleUI/Configuration/`.

## M11 remaining

### Launch VS Code from the game

**Status:** Planned

Keep external-process launching outside Core. Decide command name, executable discovery, workspace behavior and failure messages before implementation.

### Full-range diagnostics

**Status:** Planned

Diagnostics currently need start/end range support carried consistently through `ScriptDiagnostic`, language service, editor protocol and `diagnostics.ts`.

## Next milestone

**M12 - Sandbox and safety**

Planned areas:

- allow-listed references / banned API analysis
- blocking API prevention
- synchronous infinite-loop protection
- script failure containment

Do not fold M12 into M11 unless needed for correctness.

## Documentation rule

After meaningful work:

- `CURRENT_STATUS.md`: progress/current focus
- `CODEBASE.md`: important entry points/ownership only
- `ARCHITECTURE.md`: architectural changes only
- `DESIGN.md`: player-facing/product decisions only
- `AI_CONTEXT.md`: durable development invariants only
