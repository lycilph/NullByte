# Narrative Tutorial and Capability Map

## Purpose

This document describes the tutorial as it exists in the game: the current capability unlock sequence, the commands exposed at each point, and the practical guidance shown by `journal`.

It is an implementation reference, not a script for the entire story. The broader narrative direction and long-term story arc are in [NARRATIVE.md](NARRATIVE.md). The progression coordinator and persistence details are in [NARRATIVE_PROGRESSION_DESIGN.md](NARRATIVE_PROGRESSION_DESIGN.md).

## 1. New-game modes

A new game can start in one of two modes:

- **Tutorial** is the default. The player begins with a restricted command set and unlocks capabilities through successful actions.
- **Freeplay** starts with all capabilities available and skips tutorial initialization and unlock messages.

Existing saves created before narrative progression are migrated to Freeplay so established players are not unexpectedly restricted.

Freeplay is selected explicitly when starting a new game (for example, `--new --freeplay`) or confirming a reset with the corresponding option. The `--freeplay` option is not a standalone startup mode.

## 2. Tutorial sequence

The sequence below reflects the current `NarrativeCoordinator` implementation. Capability names are stable internal identifiers; command names are player-facing.

| Step | Trigger | Capabilities unlocked | Player-facing result |
|---|---|---|---|
| Initialization | New Tutorial session | `player.inspect` | SYSTEM introduces NB-0001 and explicitly tells the player to use `status`. |
| Runtime status | Successful `status` | `host.controls`, `network.scan`, `player.profile` | Host controls and network scanning become available. |
| First scan | Successful scan discovers at least one new host | `network.analyze` | SYSTEM reports discovered hosts and introduces `analyze <host>`. |
| First analysis | Successful analysis | `network.connect`, `network.inspect` | SYSTEM introduces `connect <host>` and `home`. |
| First connection | Successful connection to a host | `server.root-access` | SYSTEM introduces `nuke <host>`. |
| First root access | Root access is newly acquired | `server.operations`, `server.backdoor` | SYSTEM introduces `hack`, `grow`, `weaken` and `backdoor`. |
| First successful hack | A hack succeeds | `files.navigation`, `files.read`, `files.manage`, `scripts.execute` | SYSTEM introduces file operations and `run <file>`. |
| First script started | Script process starts successfully | `scripts.inspect`, `scripts.control`, botnet, cloud and activity capabilities | SYSTEM introduces process controls and the optional expansion systems. |

The first successful hack unlocks file operations and script execution. Root access does not unlock file operations. A failed hack does not advance this milestone.

The first script milestone currently unlocks these capability groups:

- `botnet.inspect`, `botnet.purchase`, `botnet.upgrade`
- `cloud.inspect`, `cloud.purchase`, `cloud.manage`
- `activities.learn`, `activities.work`, `activities.crime`, `activities.manage`

These unlocks expose existing systems; they do not make botnet, cloud or activities mandatory for the main narrative.

## 3. Command guidance

The `journal` command displays guidance derived from capabilities already unlocked. It is deliberately reconstructed from current state, so instructions remain available after save/load even though the full message history is not stored.

| Guidance section | Capability | Main commands introduced |
|---|---|---|
| Status | `player.inspect` | `status`, `money`, `skills` |
| Host controls | `host.controls` | `help`, `journal`, `save`, `load`, `saves`, `reset`, `clear`, `exit` |
| Network discovery | `network.scan` | `scan` |
| Network analysis | `network.analyze` | `analyze <host>` |
| Network connection | `network.connect` | `connect <host>`, `home` |
| Access | `server.root-access` | `nuke <host>` |
| Server operations | `server.operations` | `hack`, `grow`, `weaken` |
| Files | `files.navigation` | `ls`, `cd`, `pwd`, `cat`, `rm`, `mkdir`, `rmdir`, `mv`, `scp`, `edit` |
| Automation | `scripts.execute` | `run <file>`, `mem`, `free`, `ps`, `kill`, `killall` |
| Expansion | `botnet.inspect` | Botnet, cloud and activity commands |

The journal text is concise guidance rather than a complete reference. Use `help` for the currently available command list and command-specific usage.

## 4. Capability enforcement

Tutorial access is enforced, not merely presented as a sequence of messages.

- Locked commands are not exposed through normal command resolution, help or completion.
- A guessed locked command is treated like an unknown command.
- Relevant scripting API operations use the same capability state so scripts cannot bypass tutorial restrictions.
- Freeplay treats all capabilities as available, including capabilities added in later versions.

The command registry and scripting API are the implementation sources of truth for exact command/API mapping. When commands or API methods change, update this document alongside the corresponding tests.

## 5. Current scope and known limitations

The current implementation establishes the tutorial's opening and the main network-to-automation capability sequence. It does not yet implement the full five-act story.

Not yet implemented as narrative content:

- recovered files and records that reveal the creator/project mystery;
- longer conversations with SYSTEM;
- a persistent archive of all story messages;
- optional narrative discoveries for first botnet/cloud purchases or first activity use;
- later-act progression and story conclusions.

The `journal` is currently a capability-based tutorial guide, not a full narrative journal.