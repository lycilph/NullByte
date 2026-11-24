# NullByte Design

Authoritative product/gameplay specification. Source defines current implementation; this file defines intended behavior.

## 1. Vision

NullByte is a text-driven incremental hacking game inspired by Bitburner. The player starts with manual terminal work, then increasingly automates gameplay with C# scripts.

```text
manual -> understand -> automate -> scale -> optimize -> harder problem
```

**Scripting is the game.** Manual commands and scripts use the same rules; scripts gain power through automation, scale and coordination.

## 2. Progression principles

- Players should encounter a mechanic before automating it.
- Repetition should naturally motivate scripting.
- Reusable programs/libraries should become a personal toolset.
- Better algorithms, targeting, timing, RAM use and infrastructure should matter.

Initial arc: learn terminal/network -> Hack/Grow/Weaken manually -> Botnet -> first script -> RAM/threads/processes -> multi-server automation -> reusable libraries -> optimization.

Target first-playable progression: roughly **1-2 hours** to the final initial server tier.

## 3. Network and hacking

The first version uses a hand-authored network. Servers have topology, hacking requirement, money/max money, security/min security, growth, RAM, root state and virtual files. `scan` reveals reachable servers; `nuke` provides simplified initial root progression.

Hack, Grow and Weaken take game time, scale with appropriate factors/threads, affect money/security and grant hacking XP. Together they create the first automation problem:

```text
security high -> weaken
money low     -> grow
otherwise     -> hack
```

Manual long actions block normal console input but not simulation; scripts, Botnet and activities continue.

## 4. Botnet

Botnet is the early passive-income loop. Nodes have level, RAM and cores and can be purchased/upgraded. It should bootstrap progress without permanently dominating effective scripted hacking.

## 5. Activities

The player can perform longer-running activities in **focused** or **background** mode. Background activity leaves the console free but reduces rewards; focused activity owns the console until ended/backgrounded.

Current activity types:

- study/training for skill XP
- jobs for money, reputation and career progression
- crimes for risk/reward progression

Crimes repeat timed attempts until stopped/background rules end them. Each attempt has a success chance; success/failure determines money/XP rewards. Crime progress, attempt counts and success counts are player-visible. Activities share one lifecycle and persist through saves where defined.

## 6. Virtual filesystem

Each server has an in-game filesystem, independent of the host OS. Paths use `/`, are case-sensitive, max 8 levels and 256 characters, and cannot contain empty/unresolved `.`/`..` segments or conflicting file/folder identities.

Console commands support current-directory/relative-path behavior. Scripts and editor API paths are server-root-relative.

## 7. Scripts, processes and RAM

Scripts are `.cx` C# top-level source compiled at runtime. Main scripts implicitly receive `nb` and `args`.

Running scripts are game processes with PID, host, path, arguments, threads, RAM and state.

Script RAM is a static gameplay cost based semantically on API capabilities used:

```text
required RAM = script RAM * threads
```

Insufficient server RAM rejects execution clearly.

The `nb` API covers game actions, server/player information, network/rooting, scripts/processes, files, Botnet and output. It should expand from gameplay needs rather than expose internal game objects.

## 8. Includes/libraries

Reusable source uses `#include "path"`. Includes are server-root-relative and resolved on the execution server.

Libraries may contain usings/types but no top-level executable statements. They do not implicitly receive `nb`/`args`; dependencies are explicit.

Rules:

- recursive includes allowed
- each file included once
- max depth 8
- cycles/missing/invalid includes are errors
- `scp` does not automatically copy dependencies
- all included source participates in diagnostics/RAM
- running processes keep the code compiled at start

Diagnostics: NB1001 missing, NB1002 cycle, NB1003 depth, NB1004 invalid path, NB1005 malformed directive, NB1006 top-level library statements.

## 9. Persistence and reset

Saves are versioned and migrated. Persistent state includes the gameplay state required to resume, including virtual files/folders and restorable process/activity state. Live CLR execution is not serialized; scripts are reconstructed from saved source.

Reset means a genuinely new game/session. Confirmed reset removes progression and autosave/backup while preserving named saves. If autosave deletion fails, reset aborts and the existing session remains active. Successful load/reset notifies editor integrations that the session changed.

## 10. Configuration

Application settings are not game state. Optional `settings.json` beside the executable is merged with CLI overrides and validated at startup.

Current host settings include save directory, editor enable/port/connection file and development game-speed multiplier. `--new` and `--write-default-settings` are launch-only instructions. Environment variables and live reload are out of scope.

## 11. VS Code integration

VS Code is optional and exposes discovered servers/files as the actual NullByte virtual filesystem. It supports file/folder editing plus Roslyn-backed diagnostics, completion, hover, signature help and definitions that match runtime script semantics.

M11 workflow improvements include:

- connected-editor status from the game - **done**
- run current script from VS Code - **done**
- query current script RAM from VS Code - **done**
- launch VS Code from the game - remaining
- full-range diagnostic underlines - remaining

Editor Run/RAM must use the same runtime behavior as normal game commands, not editor-specific game rules.

## 12. Script safety

M12 will strengthen restrictions around host filesystem/network/process access, reflection/assembly loading, uncontrolled threading, blocking waits and synchronous infinite loops. This is practical hobby-game sandboxing, not a hostile-code security boundary.

## 13. First-playable scope

Includes terminal/network exploration, hacking, Botnet, virtual filesystem, C# scripting, processes/threads/RAM, libraries, activities, persistence, optional VS Code integration, sandboxing and a balance/tutorial pass.

Deferred: factions, augmentations, stock market, corporations, gangs, Sleeves, multiplayer, offline progression and full GUI. Purchased servers/port-opening progression remain candidates where they improve the initial arc.

## 14. Roadmap

- **M0-M3:** foundation, simulation, network/formulas, console/manual actions and QoL.
- **M4:** Botnet.
- **M5-M6:** Roslyn scripting, processes, threads, RAM and remote execution.
- **M7:** persistence, host lifecycle and true reset.
- **M8:** initial VS Code integration.
- **M9:** includes/libraries.
- **M10:** activities including jobs, study/training and crimes.
- **M11:** VS Code workflow improvements; three of five deliverables complete.
- **M12:** sandbox and safety.
- **M13:** progression/balance, tutorial/help, home RAM upgrades; target 1-2 hour initial arc.
- **M14:** stabilization, docs, packaging and first playable.

Current implementation progress belongs in `CURRENT_STATUS.md`.

## 15. Future possibilities

WPF/AvalonEdit host, larger networks, home RAM/purchased-server progression, port-opening programs, factions/augmentations/prestige, additional economies, offline progression, API versioning and improved include dependency/RAM analysis.

## 16. Design test

Before adding a feature, ask:

1. Does it create an understandable problem?
2. Can programming automate/improve it?
3. Can better programming improve it further?
4. Does it interact with time, RAM, money, risk or infrastructure?
5. Does it encourage reusable tools?
6. Are the rules understandable?

> Does this give the player a reason to write a better program?
