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
- launching VS Code from the game.

The remaining item in this area is full-range diagnostic underlining in the VS Code extension.

## M12 — Sandbox and safety

Planned work includes making player scripts safer and more contained:

- allow-listed references / banned API analysis;
- prevention of blocking APIs;
- protection against synchronous infinite loops;
- containment of script failures.

The goal is to make arbitrary player-authored C# useful without allowing it to compromise the game process.

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
