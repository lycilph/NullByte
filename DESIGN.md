# NullByte Design

Authoritative product and gameplay specification for NullByte.

See:

- `AI_CONTEXT.md` - development rules
- `ARCHITECTURE.md` - technical architecture
- `CODEBASE.md` - source/test navigation
- `CURRENT_STATUS.md` - implementation progress

Source code defines current implementation. This document defines intended behavior.

---

# 1. Vision

NullByte is a text-driven incremental hacking game inspired by Bitburner.

The player begins by manually exploring and hacking servers through a terminal. Repetition creates motivation to automate those actions with C# scripts.

The core progression loop is:

```text
Do manually
  -> understand
  -> automate
  -> scale
  -> optimize
  -> encounter harder problem
```

Core principle:

> Scripting is the game.

Programming should increasingly become how the player interacts with and optimizes the world.

---

# 2. Design principles

## Shared rules

Manual commands and scripts use the same gameplay mechanics.

Scripts gain power through automation, scale and coordination, not stronger versions of actions.

## Automation is earned

Players should normally encounter and understand a mechanic before automating it.

Repetition should naturally create the thought:

> I should write a script for this.

## Programs compose

Players should gradually build reusable tools and libraries rather than isolated scripts.

## Programming skill matters

Better:

- algorithms
- abstractions
- targeting
- scheduling
- thread allocation
- RAM usage
- infrastructure

should produce observable gameplay benefits.

---

# 3. Initial progression

The first playable experience should broadly progress through:

1. Learn terminal/network exploration.
2. Manually Hack/Grow/Weaken servers.
3. Establish passive Botnet income.
4. Write a first automation script.
5. Learn RAM, threads and processes.
6. Execute automation across multiple servers.
7. Build reusable libraries.
8. Optimize targeting and infrastructure.

By late early-game, the player's programs should drive substantially more progress than manual commands.

Target first-playable progression is approximately **1-2 hours** to the final initial server tier.

---

# 4. Network and servers

The first version uses a hand-authored network.

Servers may contain:

- hostname/connections
- hacking requirement
- money/max money
- security/min security
- growth
- RAM
- root status
- virtual files

Servers are discovered through exploration rather than all being visible initially.

`scan` reveals reachable servers.

`nuke` provides initial root-access progression. More advanced port-opening requirements are deferred.

---

# 5. Hack, Grow and Weaken

These are the fundamental early automation mechanics.

## Hack

Attempts to steal server money.

Affected by factors including:

- hacking skill
- server requirement/security
- thread count

Hack takes time, changes security and grants hacking experience.

## Grow

Restores/increases server money toward its maximum.

Effectiveness depends on server characteristics, security and threads.

Grow takes time, raises security and grants experience.

## Weaken

Reduces server security toward its minimum.

Effect scales with threads.

Weaken takes time and grants experience.

Together they produce the first meaningful scripting problem:

```text
security too high -> weaken
money too low     -> grow
otherwise         -> hack
```

Players should be free to improve substantially on this simple strategy.

---

# 6. Time and manual actions

Gameplay progresses in game time, initially approximately **1 game second per real second**.

Long manual operations temporarily block normal console input but do not stop simulation.

While waiting:

- scripts continue
- Botnet progresses
- activities progress
- scheduled operations continue

Automation therefore provides practical advantages without different game rules.

---

# 7. Botnet

Botnet provides passive income and an early incremental loop.

Nodes have upgradeable:

- level
- RAM
- cores

Players can buy nodes and upgrades.

Botnet should provide useful, reliable passive income but should not permanently dominate well-designed scripted hacking.

The two economies should complement each other.

---

# 8. Activities

Players can perform longer-running activities such as:

- jobs
- training
- studying

Activities may provide money and progression.

Jobs may involve companies, ranks, requirements, salaries and promotions.

Training/study can exchange time and potentially money for skill progression.

Activities should create meaningful alternatives rather than only idle waiting.

---

# 9. Virtual filesystem

Every server has a virtual filesystem.

Player files are part of game state, not the operating-system filesystem.

Paths use `/` and are case-sensitive.

Important rules:

- root-relative paths may optionally start with `/`
- maximum depth: 8
- maximum path length: 256 characters
- empty segments are invalid
- raw paths cannot contain unresolved `.` or `..`
- a path cannot simultaneously be a file and directory

Console commands support a current working directory and shell-style relative paths.

Scripts always use server-root-relative paths and do not inherit the console working directory.

---

# 10. Scripts

Player scripts use `.cx`.

They contain C# top-level statements and are compiled at runtime.

Main scripts implicitly receive:

- `nb` - NullByte API
- `args` - script arguments

Example:

```text
var target = args[0];

while (true)
{
    if (nb.GetServerSecurityLevel(target) >
        nb.GetServerMinSecurityLevel(target) + 5)
        await nb.Weaken(target);
    else
        await nb.Hack(target);
}
```

Players do not write an explicit runtime entry class/method.

---

# 11. Processes and RAM

Running scripts are game processes with concepts such as:

- PID
- host
- path
- arguments
- threads
- RAM
- state

Scripts have a static gameplay RAM cost based on API capabilities used.

Conceptually:

```text
required RAM = script RAM * threads
```

Execution fails clearly when the server lacks available RAM.

RAM should encourage meaningful choices between general-purpose and focused scripts.

---

# 12. Script API

The API exposed through `nb` includes capabilities covering:

- Hack/Grow/Weaken/Sleep
- server information
- player information
- scanning/root access
- script execution/process management
- files
- Botnet
- output

The API should expand according to gameplay needs rather than exposing internal game objects indiscriminately.

---

# 13. Libraries and includes

Reusable source uses:

`#include "path"`

Includes are server-root-relative.

Libraries may contain:

- using directives
- type declarations

They may not contain top-level executable statements.

Libraries do not implicitly receive `nb` or `args`; dependencies are passed explicitly.

Includes:

- may be recursive
- are included once
- are limited to depth 8
- may not form cycles
- fail clearly when missing/invalid

Included source contributes to compilation, diagnostics and RAM analysis.

Current RAM behavior charges API usage across all included source rather than runtime reachability.

Running processes use the source compiled when they started; later edits do not mutate them.

---

# 14. Include diagnostics

NullByte-specific include errors:

| ID | Meaning |
|---|---|
| NB1001 | Include not found |
| NB1002 | Include cycle |
| NB1003 | Depth > 8 |
| NB1004 | Invalid include path |
| NB1005 | Malformed include |
| NB1006 | Top-level statements in library |

Diagnostics should identify the player's actual file and source location.

---

# 15. Persistence and reset

Game progress can be manually saved/loaded and periodically autosaved.

The save format is versioned and supports migration.

Virtual files are persisted.

Running scripts are represented by game-level process information and reconstructed from saved source rather than serializing live CLR execution state.

Reset means:

> Start a genuinely new game.

It replaces the previous game state/session and removes the previous autosave.

Reset should not silently discard incompatible active foreground activity.

---

# 16. VS Code integration

VS Code is an optional richer script-development environment.

Players can:

- browse discovered servers/files
- create/edit/move/delete virtual files
- receive diagnostics
- use completion
- use hover/signature help
- navigate to definitions

Changes affect the actual in-game virtual filesystem.

The editor should understand the same script/include environment as runtime compilation.

Planned workflow improvements include:

- inspect connected editors
- full-range diagnostics
- run current script
- query RAM
- launch VS Code from the game

Current implementation progress belongs in `CURRENT_STATUS.md`.

---

# 17. Script safety

Player scripts should eventually be restricted from unsafe host capabilities such as:

- unrestricted filesystem access
- networking
- process launching
- reflection/assembly loading
- uncontrolled threading
- blocking waits

Synchronous infinite loops also require mitigation.

This is practical game sandboxing, not a hardened hostile-code security boundary.

---

# 18. First playable scope

Includes:

- terminal/network exploration
- Hack/Grow/Weaken
- player progression
- Botnet
- virtual filesystem
- C# scripting
- processes, threads and RAM
- reusable libraries
- activities
- persistence
- optional VS Code integration
- sandboxing
- balance/tutorial pass

Explicitly deferred include:

- factions
- augmentations
- stock market
- corporations
- gangs
- Sleeves
- multiplayer
- full graphical UI
- other large Bitburner-inspired systems

Purchased servers and port-opening progression may be introduced when they meaningfully improve initial progression.

---

# 19. Roadmap

Completed milestone implementation status belongs in `CURRENT_STATUS.md`; this roadmap defines intended scope.

## M0-M3 - Foundation and manual game

- project foundation
- simulation/threading
- network/model/formulas
- terminal and manual actions
- completion/history

## M4 - Botnet

Passive income and upgrade loop.

## M5-M6 - Scripting

Roslyn scripts, script API, processes, RAM, threads and remote execution.

## M7 - Persistence

Versioned saves, migration, manual saves and autosave.

## M8 - VS Code

Virtual filesystem and initial language-service integration.

## M9 - Includes

Reusable libraries with compilation, RAM and editor support.

## M10 - Activities

Jobs, training and study.

## M11 - VS Code improvements

- connected-editor visibility
- improved diagnostics
- run from editor
- RAM query
- launch VS Code

## M12 - Sandbox and safety

Restrict unsafe APIs and contain problematic scripts.

## M13 - Progression and balance

Tune network progression, economy, Botnet, hacking, RAM, tutorial/help and sample scripts.

Target roughly 1-2 hours for the initial progression arc.

## M14 - First playable

Stabilization, documentation, packaging and validation with another player.

---

# 20. Future possibilities

After the first playable:

- WPF host, potentially using AvalonEdit
- larger networks
- home RAM progression
- purchased servers
- port-opening programs
- factions
- augmentations/prestige
- additional economies
- offline progression
- script API versioning
- improved include dependency/reachability handling

New systems should strengthen rather than dilute the programming/automation fantasy.

---

# 21. Design test

Before adding a feature, ask:

1. Does it create an interesting problem?
2. Can the player understand the problem?
3. Can programming automate or improve it?
4. Can better programming improve it further?
5. Does it interact meaningfully with time, RAM, money or infrastructure?
6. Does it encourage reusable tools?
7. Are the rules understandable?

The deciding question is:

> Does this give the player a reason to write a better program?