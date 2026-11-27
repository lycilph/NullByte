# Narrative Tutorial and Capability Map

## 1. Purpose and scope

This document records the first concrete mapping of the current player-facing command and scripting API surfaces to narrative capabilities. It also proposes a first playable tutorial walkthrough.

It is based on the current implementation on `feature/narrative-design-docs`. The mapping is a design proposal, not an implementation: all commands are currently registered and available independently of story progression.

The aim is to make the first hours of play legible and story-driven without inventing gameplay systems that do not exist.

## 2. Current implementation findings

### 2.1 New-game state

A new game is created by `InitialGameStateFactory`:

- Player starts on `home`.
- Starting money is $1,000.
- Hacking experience starts at zero; the current formula gives the player the starting hacking level.
- The player has root access to `home`.
- `home` is the only initially discovered server.
- The initial network contains `home`, `n00dles`, `foodnstuff`, `sigma-cosmetics`, `joesguns`, `harbor`, `catalyst`, `datavault`, `phantom`, `neurocore`, `blackice` and `null`.
- `home` is connected to `n00dles` and `foodnstuff`.
- `n00dles` requires hacking level 1; `foodnstuff` requires level 5.

Therefore, "starts with nothing" can mean **no in-world gameplay commands**, but it does not currently mean an empty wallet, an empty home server or an absent network. Those starting resources are established game balance and should not be changed as part of narrative gating without a separate design decision.

### 2.2 Command registration and dispatch

Commands are registered by `CommandSetup.Create`, which creates the registry and registers the command groups. All normal command groups are currently registered immediately.

The relevant behavior is:

- `CommandRegistry.Commands` exposes the registered commands.
- `CommandRegistry.TryResolve` resolves every registered command and alias.
- `CommandDispatcher.DispatchAsync` resolves and invokes commands.
- `SystemCommands.HelpAsync` lists all non-debug registered commands.
- `CompletionCatalog.BuildCommands` exposes all registered commands and aliases to tab completion.
- `CommandRegistry.Subcommands` is also used to show subcommand usage.

Consequently, filtering only the help output would not implement hidden commands. The same capability policy must govern command resolution, aliases, help, subcommand usage and completion.

### 2.3 Existing boot sequence

The current `BootSequence` is a host startup animation. It prints generic machine startup messages, including "Command interpreter standing by", then "SYSTEM READY". It does not represent a narrative event or persistent game state.

The story initialization sequence should be introduced separately from this host startup behavior. We can revise the boot text later if it conflicts with the final opening, but the tutorial should not depend on the terminal animation as its narrative state machine.

### 2.4 Scripting API

Every running script receives an `INullByteApi` instance through `NullByteApiFactory`. The API currently exposes all of its methods to every script. There is no capability check in the API implementations.

This is an important second access surface. Hiding `hack` in the console while leaving `nb.Hack()` callable would let scripts bypass the story restriction once script execution is available.

The script API is also broader than the console in some places and narrower in others. It does not currently provide direct equivalents for console `connect`, `home`, `analyze`, `backdoor`, `cat` or `edit`, for example. It does expose server queries and actions that accept a hostname directly.

The script editor/compiler sees the full `INullByteApi` interface, so hiding API methods from IntelliSense is a separate problem from runtime authorization. The first implementation should at minimum enforce capabilities at runtime; whether editor completion must also conceal unavailable API members should be decided before the scripting phase.

### 2.5 Persistence

The current save format is version 7. `SaveGame` stores player, network, botnet, employment, activity, files and running processes, but has no narrative or capability state.

A persistent narrative state therefore requires a save-format change and migration. The existing migrator upgrades older formats in sequence. The recommended compatibility policy is that a save made before narrative gating is introduced should load with all currently implemented capabilities unlocked, so an established player is never unexpectedly restricted.

## 3. Recommended access model

### 3.1 Separate in-world capabilities from host controls

Recommended distinction:

- **In-world gameplay commands** are hidden until NB-0001 acquires the corresponding capability.
- **Host/session controls** remain available independently of the story so the player is not trapped and can still get help, save, load, reset, clear the terminal or exit.

Under this model, a new game starts with no in-world commands, while a small meta-command set remains available. The help output initially contains only that meta-command set.

This is a recommendation for review, not a settled user decision. If "starts with nothing" is intended literally to include `help`, `save`, `load` and `exit`, the design needs a different bootstrap and escape mechanism.

### 3.2 Capability identity

Capabilities should have stable IDs independent of command spelling or localized text. The command registry should associate each command definition with one required capability, or explicitly mark it as a host command.

The capability state should be owned by Core/game state, not by the console registry. The registry and dispatcher can then ask whether a capability is available, while the scripting API can use the same state.

### 3.3 Unknown-command behavior

A locked command should be treated exactly like an unknown command:

- no "locked" or "not yet unlocked" explanation;
- no suggestion of the hidden command name;
- no usage text;
- no subcommand list;
- no tab-completion result.

The same applies to aliases. A known command with a hidden alias must not leak through the alias.

### 3.4 Suggested capability groups

The following IDs are proposed stable identifiers. They group related functionality rather than individual command strings.

| Capability ID | Console commands | Notes |
|---|---|---|
| `player.inspect` | `status`, `money`, `skills` | Inspect player resources and skills |
| `network.scan` | `scan` | Discover adjacent servers |
| `network.analyze` | `analyze` | Inspect a discovered server; requires network discovery |
| `network.connect` | `connect`, `home` | Navigate discovered network |
| `server.root-access` | `nuke` | Gain root access when hacking level permits |
| `server.backdoor` | `backdoor` | Install a backdoor on a rooted server |
| `server.operations` | `hack`, `grow`, `weaken` | Perform operations on a rooted server |
| `files.navigation` | `ls`, `cd`, `pwd` | Navigate the virtual file system |
| `files.read` | `cat` | Read files |
| `files.manage` | `rm`, `mkdir`, `rmdir`, `mv`, `scp`, `edit` | Create, modify, move, copy and remove files |
| `scripts.execute` | `run` | Start a script |
| `scripts.inspect` | `mem`, `free`, `ps` | Inspect script RAM and running processes |
| `scripts.control` | `kill`, `killall` | Stop script processes |
| `botnet.inspect` | `botnet` (also `botnet status`) | Inspect botnet state |
| `botnet.purchase` | `botnet buy` | Purchase a node |
| `botnet.upgrade` | `botnet upgrade` | Upgrade a node |
| `cloud.inspect` | `cloud` (also `cloud list`) | Inspect cloud servers |
| `cloud.purchase` | `cloud buy` | Purchase a cloud server |
| `cloud.manage` | `cloud upgrade`, `cloud rename` | Manage owned cloud servers |
| `activities.learn` | `study`, `train` | Study and train skills |
| `activities.work` | `work` | Work for a company |
| `activities.crime` | `crime` | Attempt crimes |
| `activities.manage` | `stop`, `focus`, `activity` | Manage or inspect the current activity |

Host/session controls proposed to remain outside narrative gating:

| Command | Alias | Reason |
|---|---|---|
| `help` | — | Explains currently available commands |
| `exit` | `quit` | Allows the player to leave |
| `clear` | `cls` | Terminal presentation only |
| `save` | — | Protects player progress |
| `load` | — | Loads a saved game |
| `saves` | — | Lists saved games |
| `reset` | — | Starts a new game |
| `vscode` | — | Optional host/editor integration; availability currently depends on settings |

Debug commands (`debug`, `time`, `cheat`, `editor`) are already marked as debug-only and should remain outside the normal player capability map.

The command groups `botnet` and `cloud` use multi-word command definitions and aliases. The access check must apply to the resolved definition, including the bare parent command and all registered subcommands.

## 4. Scripting API capability mapping

The API should use the same capability IDs wherever a script method represents the same player-facing action. The table maps the current public API surface; it does not imply that every API method has a direct console equivalent.

| Capability ID | Script API surface |
|---|---|
| `scripts.execute` | `nb.Print`, `nb.Sleep`, and the ability to start scripts at all |
| `network.scan` | `nb.Scan` |
| `server.root-access` | `nb.Nuke` |
| `server.operations` | `nb.Hack`, `nb.Grow`, `nb.Weaken` |
| `network.inspect` | `nb.GetServerMoneyAvailable`, `nb.GetServerMaxMoney`, `nb.GetServerSecurityLevel`, `nb.GetServerMinSecurityLevel`, `nb.GetServerRequiredHackingLevel`, `nb.GetServerMaxRam`, `nb.GetServerUsedRam`, `nb.HasRootAccess` |
| `scripts.inspect` | `nb.Ps`, `nb.ScriptRunning`, `nb.GetScriptRam` |
| `scripts.execute` | `nb.RunAsync`, `nb.ExecAsync` |
| `scripts.control` | `nb.Kill`, `nb.KillAll` |
| `files.navigation` / `files.manage` | `nb.Files.List`, `nb.Files.Delete`, `nb.Files.Move`, `nb.Files.Copy` |
| `botnet.inspect` | `nb.Botnet.NumberOfNodes`, `nb.Botnet.GetNodeStats`, `nb.Botnet.GetNextNodeCost`, and upgrade-cost queries |
| `botnet.purchase` | `nb.Botnet.PurchaseNode` |
| `botnet.upgrade` | `nb.Botnet.UpgradeLevel`, `nb.Botnet.UpgradeRam`, `nb.Botnet.UpgradeCore` |
| `cloud.inspect` | `nb.Cloud.GetServers`, `nb.Cloud.GetServer`, `nb.Cloud.GetServerCost`, `nb.Cloud.GetUpgradeCost` |
| `cloud.purchase` | `nb.Cloud.Purchase` |
| `cloud.manage` | `nb.Cloud.Upgrade`, `nb.Cloud.Rename` |
| `activities.learn` | `nb.Activities.Study`, `nb.Activities.Train`, `nb.Activities.GetCourses` |
| `activities.work` | `nb.Activities.Work`, `nb.Activities.GetCompanies`, `nb.Activities.GetReputation`, `nb.Activities.GetJob` |
| `activities.crime` | `nb.Activities.Crime`, `nb.Activities.GetCrimes`, `nb.Activities.GetCrimeChance` |
| `activities.manage` | `nb.Activities.Stop`, `nb.Activities.GetStatus` |
| `player.inspect` | `nb.Player.Money`, `nb.Player.Skills`, `nb.Activities.GetSkillLevel` |

Important gaps and overlaps to resolve:

- `nb.Scan(hostname)` accepts a hostname. The underlying network service only scans a source server the player has already discovered, but API methods that query server properties resolve a hostname directly.
- `nb.Nuke`, `nb.Hack`, `nb.Grow` and `nb.Weaken` accept a hostname and do not independently require that the target has been discovered; the action services enforce root access and/or hacking level as applicable.
- The API has no direct equivalents for console `connect`, `home`, `analyze` or `backdoor`.
- The files API has list/delete/move/copy operations, but no direct read/write method. File editing is currently a console-host operation.
- Activity API calls always start work in the background, as documented on `INullByteActivitiesApi`; the console can start focused or background activities.
- `nb.Player` exposes money and skills as read-only information. It should be available when player inspection is unlocked.

For the first pass, runtime API calls must check the same capability state as their corresponding console actions. The full interface remains visible to script compilation and editor tooling today; concealing members from IntelliSense may require a separate API-surface design and is not solved by runtime checks alone.

## 5. Proposed first playable walkthrough

This is a suggested opening sequence to validate through playtesting. It intentionally uses only current game systems.

### Step 0 — No in-world commands

The host completes its ordinary startup. The narrative layer then displays:

~~~text
SYSTEM INITIALIZATION

Designation: NB-0001
Environment integrity: 98.7%
Local runtime: ACTIVE
External network access: RESTRICTED

Player capability profile: EMPTY

Awaiting authorization.
~~~

No gameplay command is available yet. Host/session controls remain available under the recommended access model.

SYSTEM continues:

~~~text
A minimal diagnostic interface can be restored.

One operation is available.
Use: status
~~~

Unlock `player.inspect` (or a narrower initial `player.status` capability if we later decide that `money` and `skills` should be separate).

### Step 1 — Inspect the local environment

Player enters:

~~~text
status
~~~

The existing command reports money, hacking level/experience, current server and root access. With a new game this should identify `home`, $1,000 and root access.

SYSTEM:

~~~text
Local environment verified.

A network interface is present.
Its connection table is incomplete.

I can restore a discovery operation.
~~~

Unlock `network.scan` and `network.analyze`.

### Step 2 — Discover adjacent systems

Player enters:

~~~text
scan
~~~

The initial network reveals `n00dles` and `foodnstuff`. This is grounded in the current network definition: both are directly connected to `home`.

SYSTEM:

~~~text
Two adjacent hosts detected.

One is within your current capability profile.
The other is not.

This distinction may become relevant.
~~~

The first successful scan is a one-time narrative milestone. It should not repeat the message on every later scan.

### Step 3 — Inspect a discovered host

Player enters:

~~~text
analyze n00dles
~~~

The existing analyze output shows root access, backdoor status, required hacking level, money, security, growth and RAM. `n00dles` requires hacking level 1, while `foodnstuff` requires level 5.

SYSTEM:

~~~text
The remote host is accessible through the network.

Your current authorization is insufficient to modify it.
A connection can be established.
~~~

Unlock `network.connect`. The player can use `connect n00dles` and `home`.

### Step 4 — Gain access

Player enters:

~~~text
connect n00dles
nuke
~~~

The current `nuke` operation grants root access when the player's hacking level meets the target requirement. The initial `n00dles` host is designed for level 1.

On successful root access, unlock `server.operations`.

SYSTEM:

~~~text
Administrative access acquired.

You have modified a system outside your assigned environment.
No authorization record was found.

Continue.
~~~

### Step 5 — Perform the first operation

Player enters:

~~~text
hack
~~~

The current hack operation takes game time, attempts to steal money, grants hacking experience and increases server security on success. A failed attempt can still grant reduced experience.

After the first completed hack attempt, SYSTEM acknowledges the first external effect. The exact message should distinguish success from failure rather than implying money was stolen when it was not.

The `grow` and `weaken` commands can be introduced with the same `server.operations` capability. `backdoor` remains a separate later capability because it changes network navigation by enabling direct connections.

### Step 6 — Discover files and scripting

After the player returns to `home`, introduce the virtual file system:

- Unlock `files.navigation` and `files.read`.
- Unlock `files.manage` when the player is ready to create a script file.
- The player can use `edit` to create a script and `ls`/ `cat` to inspect it.

Then unlock `scripts.execute` and `scripts.inspect`. The first script should be deliberately simple: print a message, wait using game time, and print again. It demonstrates that the player can create an autonomous process without requiring money, network access or a complicated script.

The first script should not require any API action that has not separately been unlocked.

On the first successful script execution, SYSTEM marks a major milestone:

~~~text
Autonomous process created.
Execution context established.

You have reproduced a limited subset of your own
operational capabilities.

Interesting.
~~~

### Step 7 — Expansion and optional systems

Introduce botnet and cloud infrastructure as separate capability groups. Botnet ownership is a natural first expansion of distributed resources; cloud servers can follow when the player has sufficient money and has discovered the relevant infrastructure.

Study/training, work and crime are introduced as optional branches as the narrative moves into human institutions. The main story must not require work or crime. Study/training can also remain optional unless a later, explicit design decision gives a specific skill milestone a narrative purpose.

## 6. Suggested unlock order

This is a first proposal, not a final balance decision.

| Order | Capability | Trigger / prerequisite |
|---:|---|---|
| 0 | Host/session controls | Always available; not part of the in-world capability profile |
| 1 | `player.inspect` | Explicitly granted during initialization |
| 2 | `network.scan` | After the player successfully uses `status` |
| 3 | `network.analyze` | After the first successful scan |
| 4 | `network.connect` | After inspecting a discovered server |
| 5 | `server.root-access` | After connecting to a suitable remote host |
| 6 | `server.operations` | After first successful root acquisition |
| 7 | `files.navigation`, `files.read` | After the first remote operation / return to home |
| 8 | `files.manage` | When the player is ready to create a script |
| 9 | `scripts.execute`, `scripts.inspect`, `scripts.control` | After a script file is created |
| 10 | `botnet.inspect`, `botnet.purchase`, `botnet.upgrade` | Expansion milestone; exact trigger to be playtested |
| 11 | `cloud.inspect`, `cloud.purchase`, `cloud.manage` | Later expansion milestone; exact trigger to be playtested |
| 12 | `server.backdoor` | After the player has encountered a suitable rooted host |
| 13 | `activities.learn`, `activities.manage` | Optional introduction to structured learning |
| 14 | `activities.work` | Optional introduction to legitimate employment |
| 15 | `activities.crime` | Optional introduction to unauthorized activity |

The exact trigger for later systems should be selected from real game events. Do not require a player to buy a cloud server, commit a crime or take a particular job just to advance the main story.

## 7. Implementation implications

The current code suggests the following responsibilities:

1. **Core state:** add durable narrative/capability state to the game model. It should be independent of ConsoleUI.
2. **Persistence:** add a save-format migration. Current save format is v7; pre-narrative saves should load with all existing capabilities unlocked.
3. **Command metadata:** associate each command definition with a capability requirement or an explicit host-control classification.
4. **Registry/dispatch:** enforce availability for resolution, help, aliases and subcommand usage—not just help rendering.
5. **Completion:** filter command names and aliases using the same capability state.
6. **Script API:** guard each relevant API operation using the same capability state. Do not rely on console command hiding.
7. **Narrative delivery:** provide host-neutral messages/events so the ConsoleUI can render SYSTEM output without owning progression.
8. **Testing:** test direct command invocation, aliases, subcommands, help, completion, script API calls, save/load and legacy migration.

The implementation should be designed around the actual game-thread and save/load lifecycle. In particular, scripts are restored after a saved session starts; their API instances must see the restored capability state.

## 8. Decisions to confirm before implementation

1. **Host controls:** should `help`, `exit`, `clear`, `save`, `load`, `saves` and `reset` remain available from the beginning, while all in-world gameplay commands start hidden? This is the recommended model.
2. **First unlock:** is `status` the right first in-world command, with the opening SYSTEM message explicitly telling the player to use it?
3. **Script API visibility:** is runtime rejection of locked API calls sufficient for the first iteration, even though the full API surface may remain visible in editor completion, or should we also design capability-aware script IntelliSense before scripting is introduced?

No runtime code should be changed until these access-scope decisions are agreed.
