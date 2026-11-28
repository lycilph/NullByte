# NullByte Roadmap

This roadmap records the current development direction. Milestones are intended to represent coherent player-visible capabilities rather than arbitrary amounts of code.

## M0–M3 — Foundation
**Status: Completed**

Core game model, initial world/network, simulation infrastructure, console shell and foundational gameplay were established.

## M4 — Botnet
**Status: Completed**

Botnet concepts, state, services and simulation were introduced.

## M5–M6 — C# scripting
**Status: Completed**

The C# scripting pipeline was established around Roslyn, including compilation, runtime execution, RAM analysis and the in-game script API.

## M7 — Persistence and lifecycle
**Status: Completed**

Save/load, autosave, versioned save data and migration infrastructure were established. Runtime execution is reconstructed rather than serialized as live objects.

## M8 — VS Code integration
**Status: Completed**

The external editor workflow was introduced, including the VS Code extension, virtual filesystem and language-service/editor features.

## M9 — Includes
**Status: Completed**

Script includes and their source-resolution pipeline were added.

## M10 — Activities
**Status: Completed**

Study/training, jobs and crimes were introduced using a shared activity lifecycle with focused/background modes and persistence.

## M11 — Editor workflow
**Status: Completed**

The editor workflow has been expanded with:

- connected-editor status;
- run-current-script from VS Code;
- script RAM queries;
- launching VS Code from the game;
- full-range diagnostic underlining in the VS Code extension, including source-mapped spans for included files.

## M12 — Sandbox and safety
**Status: Completed**

- Allow-listed .NET references and semantic analysis of banned host APIs.
- Diagnostics for common synchronous blocking APIs and non-cooperative loops.
- Containment of script failures, including process state, RAM cleanup, load-context unloading and lifecycle observer isolation.

See [Script sandbox and safety](ARCHITECTURE.md#script-sandbox-and-safety) for the security model and its limitations.

## M13 — Narrative progression and tutorial
**Status: Completed**

The first player-facing narrative progression system was introduced:

- New Tutorial and Freeplay progression modes.
- Tutorial capabilities are unlocked gradually through successful gameplay events.
- Commands and scripting APIs are runtime-gated by narrative capabilities.
- Narrative messages and the journal explain newly available capabilities.
- Narrative progression is persisted and migrated safely for existing saves.
- Tutorial presentation is visually distinguished from normal terminal output.
- Existing players and players who prefer to skip the tutorial can use Freeplay mode.
- Reset and new-game startup can explicitly choose Freeplay with `--freeplay`; the flag requires an explicit new-game request.

The implementation also establishes the narrative coordinator and event-driven progression infrastructure that later story content can build on.

## M14 — Progression and balance
**Status: Planned**

Planned work around progression, economy and balancing the available activities and hacking systems into a coherent player progression.

The exact feature set should be shaped by playtesting rather than prematurely locking the implementation.

## M15 — First playable release
**Status: Planned**

Bring the currently implemented systems together into a coherent first release with enough content and balance for a complete gameplay loop.

This milestone should prioritize player experience and stability over adding every planned system.

## Longer-term possibilities

These are ideas rather than committed milestones:

- WPF/AvalonEdit in-game editor;
- larger and more varied networks;
- additional infrastructure beyond the implemented cloud servers;
- port-opening/network-access mechanics;
- home infrastructure;
- factions;
- augmentations;
- prestige;
- stock market;
- corporations;
- gangs;
- Sleeves;
- offline progression;
- richer script API/versioning;
- multiplayer;
- additional world and progression systems.

## Roadmap rule

A milestone should describe a meaningful player-facing capability or a clear architectural enabler. Avoid turning the roadmap into a list of individual classes or implementation tasks.
