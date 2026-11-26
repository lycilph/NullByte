# NullByte Roadmap

This roadmap records the current development direction. Milestones are intended to represent coherent player-visible capabilities rather than arbitrary amounts of code.

## Completed

### M0–M3 — Foundation

Core game model, initial world/network, simulation infrastructure, console shell and foundational gameplay were established.

### M4 — Botnet

Botnet concepts, state, services and simulation were introduced.

### M5–M6 — C# scripting

The C# scripting pipeline was established around Roslyn, including compilation, runtime execution, RAM analysis and the in-game script API.

### M7 — Persistence and lifecycle

Save/load, autosave, versioned save data and migration infrastructure were established. Runtime execution is reconstructed rather than serialized as live objects.

### M8 — VS Code integration

The external editor workflow was introduced, including the VS Code extension, virtual filesystem and language-service/editor features.

### M9 — Includes

Script includes and their source-resolution pipeline were added.

### M10 — Activities

Study/training, jobs and crimes were introduced using a shared activity lifecycle with focused/background modes and persistence.

### M11 — Editor workflow

The editor workflow has been expanded with:

- connected-editor status;
- run-current-script from VS Code;
- script RAM queries;
- launching VS Code from the game;
- full-range diagnostic underlining in the VS Code extension, including source-mapped spans for included files.

## M12 — Sandbox and safety

**Status: Completed**

Player scripts now have a practical, compile-time safety layer:

- allow-listed .NET metadata references, with semantic analysis for banned host APIs;
- diagnostics for common synchronous blocking APIs;
- diagnostics for loops that do not contain an asynchronous yield;
- containment of script exceptions, including process failure state, RAM release, collectible load-context cleanup and isolation of lifecycle observers.

The intended security level is a practical hobby-game sandbox: protection against accidental mistakes and common attempts to reach the host environment, not a hardened boundary against a determined attacker. The loop check is a syntactic safeguard rather than a formal proof of cooperation, and script cancellation remains cooperative at asynchronous yield points. This does not provide process isolation or a hard execution-time limit.

## M13 — Progression and balance

Planned work around progression, economy and balancing the available activities and hacking systems into a coherent player progression.

The exact feature set should be shaped by playtesting rather than prematurely locking the implementation.

## M14 — First playable release

Bring the currently implemented systems together into a coherent first release with enough content and balance for a complete gameplay loop.

This milestone should prioritize player experience and stability over adding every planned system.

## Longer-term possibilities

These are ideas rather than committed milestones:

- WPF/AvalonEdit in-game editor;
- larger and more varied networks;
- purchased servers and additional infrastructure;
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
