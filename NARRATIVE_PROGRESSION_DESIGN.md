# Narrative Progression Design

> **Implementation status (2026-11-27):** The Core coordinator, tutorial bootstrap, status milestone, and main network/root/hack/script-start triggers are implemented on `feature/narrative-design-docs`. Stage 4 capability checks now enforce the console and scripting API map in `NARRATIVE_TUTORIAL.md`. Optional botnet/cloud/activity narrative messages remain deferred; their access capabilities are unlocked after the first successful script start. The code and tests are the source of truth where this design draft differs from implementation.

## 1. Purpose and scope

This document records Stage 3: how actual gameplay outcomes should drive narrative milestones, capability unlocks and SYSTEM messages.

It is a design document, not an implementation. It deliberately maps to the current Core services and existing events. Proposed new events are identified as proposals; they are not described as existing APIs.

Stage 3 should introduce a small event-driven coordinator, not a general quest engine. Capability enforcement at every command and scripting API boundary remains Stage 4.

## 2. Confirmed progression modes

The persistent progression modes are:

- **Tutorial** — the default for a new game. Starts with no available commands and advances through explicit story milestones and capability unlocks.
- **Freeplay** — an intentional new-game option for players who want to start without the tutorial, as well as the migration target for saves created before narrative progression existed. All current and future capabilities are available; no tutorial messages or milestones are replayed.

Freeplay is therefore not merely a technical compatibility label. It is a supported way to restart the game while skipping the tutorial. New-game/reset flows must offer the choice outside the in-game command interface when necessary. The first implementation should not silently convert an existing Freeplay save into Tutorial mode.

## 3. Current implementation findings

### 3.1 Existing service-level event surface

The current code already has a few useful game-thread events:

| Existing source | Existing event | What it tells us |
|---|---|---|
| `ActivityService` | `ActivityEnded(ActivityEndInfo)` | An activity ended, with its concrete activity and reason |
| `ActivityService` | `JobPromoted(JobPromotion)` | The player advanced a job rank |
| `ActivityService` | `CrimeAttempted(CrimeAttempt)` | A crime attempt ended, with outcome and rewards |
| `ScriptService` | `ProcessCompleted(ScriptProcess)` | A script process completed normally |
| `ScriptService` | `ProcessFailed(ScriptProcess, Exception)` | A script process failed |

These events are raised from the game-thread-owned services. They are currently used primarily by presentation or script lifecycle observers, not by a narrative subsystem.

### 3.2 Important successful actions currently return results, not events

The following operations mutate game state and return result objects, but do not currently publish narrative-oriented events:

- `NetworkService.Scan` discovers neighboring servers by adding them to `Player.DiscoveredServerIds`.
- `NetworkService.Connect` changes the current server on success.
- `ServerActions.Nuke` grants root access on success.
- `ServerActions.HackAsync`, `GrowAsync`, `WeakenAsync` and `BackdoorAsync` return action results.
- `BotnetService.PurchaseNode` and `Upgrade` return purchase results.
- `CloudServerService.Purchase`, `Upgrade` and `Rename` return operation results.
- `ActivityService.StartCourse`, `StartWork` and `StartCrime` return start results.

The console commands and scripting APIs call these shared services. Narrative triggers should be attached at this shared Core boundary, not duplicated in command handlers, so manual play and script automation produce the same progression.

### 3.3 Script start is a distinct milestone

`ScriptService.Start` creates a process after validation, compilation, RAM allocation and API construction. `ProcessCompleted` only occurs when a process finishes. The narrative milestone "first autonomous process created" should therefore be tied to a successful process start, not process completion. A narrow `ProcessStarted` event is proposed.

## 4. Recommended architecture

### 4.1 Keep the coordinator small

Introduce a Core-owned `NarrativeCoordinator` (or equivalent) that receives typed gameplay events and applies one-time milestone rules to `GameState.Narrative`.

Responsibilities:

1. Observe meaningful gameplay events.
2. Check progression mode and completed milestone IDs.
3. Apply any capability unlocks.
4. Record milestone and message IDs before publishing presentation output.
5. Publish host-neutral narrative messages for the host to render.

It should not:
- own gameplay mechanics or duplicate service validation;
- inspect the command registry or parse console input;
- directly print terminal text;
- run timers or poll the game state;
- contain the full quest/dialogue framework;
- decide whether an operation succeeded independently of its owning service.

### 4.2 Event source and threading

Gameplay services already run on the game thread. New narrative-relevant events should be raised synchronously after the state mutation succeeds and on that same thread.

The coordinator should be wired once when `GameSession` is created. It must not be registered once per command invocation or per script process.

For the first implementation, prefer explicit typed service events over a generic event bus. There are few sources and the event payloads are domain-specific. A general event bus would add indirection without a current need.

### 4.3 Structured output

The coordinator should publish a small host-neutral record, for example:

~~~csharp
public sealed record NarrativeMessage(
    string Id,
    NarrativeMessageKind Kind,
    string Text);
~~~

The exact type may include a stable content key rather than rendered text if localization/content assets are introduced later. For the first console implementation, a stable ID plus text is sufficient.

The host subscribes to narrative output and renders it through the existing output abstraction. Core must not depend on ConsoleUI or terminal APIs.

Message delivery order:

1. Evaluate the event and milestone.
2. Apply capability unlocks and complete the milestone.
3. Mark the message ID as delivered in narrative state.
4. Publish the message to the host.

This ordering prevents a presentation failure or session reload from granting the same unlock twice or re-queuing the same one-time message. The host observer must not be allowed to break the gameplay action.

### 4.4 New-game bootstrap

The initialization sequence is not triggered by a player command. When a new Tutorial-mode session starts, the session bootstrap asks the coordinator to initialize the tutorial.

The bootstrap:
- emits the initialization SYSTEM message once;
- records the initialization milestone;
- unlocks `player.inspect`, exposing `status`;
- leaves all other commands, including host/session commands, unavailable.

After a successful `status` command, the command/session boundary notifies the coordinator of the successful command use. The coordinator unlocks `host.controls` and emits the instruction that normal host controls are now available.

Freeplay-mode sessions skip this bootstrap entirely.

## 5. Proposed trigger and milestone catalog

Stable IDs below are proposed names. They should be treated as content identifiers once accepted; changing them later requires migration or compatibility handling.

| Trigger / milestone ID | Actual source or proposed event | Condition | Narrative effect |
|---|---|---|---|
| `tutorial.initialization.complete` | New Tutorial-mode session bootstrap | First initialization of a new Tutorial save | Emit `system.initialization`; unlock `player.inspect` |
| `tutorial.status.used` | Successful command-dispatch completion | The resolved command is `status` and its handler completes successfully | Unlock `host.controls`; emit `system.host-controls` |
| `tutorial.network.first-scan` | Proposed `NetworkService.ServersDiscovered` | A scan discovers at least one previously undiscovered server | Emit `system.network-discovered`; unlock the next network capability/capabilities |
| `tutorial.network.first-connection` | Proposed `NetworkService.ServerConnected` | A connection to a different server succeeds | Emit `system.network-connected`; advance the network tutorial |
| `tutorial.network.first-root` | Proposed `ServerActions.RootAccessGained` | `Nuke` succeeds and root access is newly granted | Emit `system.root-access`; unlock authorized server operations |
| `tutorial.network.first-hack` | Proposed `ServerActions.HackCompleted` | A hack operation completes successfully; a failed attempt does not satisfy this milestone | Emit `system.first-external-action` |
| `tutorial.expansion.first-script` | Proposed `ScriptService.ProcessStarted` | A script has compiled, acquired RAM and its process has been created successfully | Emit `system.first-script`; unlock remaining script/process controls as designed |
| `tutorial.expansion.first-botnet-node` | Proposed `BotnetService.NodePurchased` | A botnet node purchase succeeds | Optional expansion message; do not make botnet ownership mandatory for the main story |
| `tutorial.expansion.first-cloud-server` | Proposed `CloudServerService.ServerPurchased` | A cloud-server purchase succeeds | Optional infrastructure message; do not make cloud ownership mandatory for the main story |
| `discovery.activities.first-study` | Proposed `ActivityService.ActivityStarted` | A university course starts successfully | Optional SYSTEM message introducing structured learning |
| `discovery.activities.first-training` | Proposed `ActivityService.ActivityStarted` | A gym course starts successfully | Optional SYSTEM message introducing physical-skill development |
| `discovery.activities.first-work` | Proposed `ActivityService.ActivityStarted` | A job starts successfully | Optional SYSTEM message introducing employment |
| `discovery.activities.first-crime` | Existing `ActivityService.CrimeAttempted` | First crime attempt actually resolves | Optional SYSTEM message introducing unauthorized activity |

### 5.1 Why activity introductions use different conditions

The current activity model does not have a general "course completed" event. Study, training and work are ongoing activities that may be stopped or run in the background. The first introduction should therefore trigger when the activity starts successfully, not claim that a course or work period has been completed.

Crime is different: `CrimeAttempted` is already emitted when an attempt resolves and contains the outcome. Use that event for the first crime introduction. Do not trigger on an unsuccessful `StartCrime` request.

### 5.2 First hack versus any server action

The initial story beat should be tied to the first successful hack, because it clearly represents NB-0001 extracting value from an external system. Grow, weaken and backdoor can be introduced as related operations after the player understands the basic action loop. Do not treat a failed hack as successful external resource acquisition.

### 5.3 Optional discoveries do not gate the main arc

Botnet, cloud, study, training, work and crime events may produce optional narrative messages. They must not be prerequisites for the main tutorial's chapter progression unless the tutorial design is explicitly revised and playtested.

## 6. Capability unlock sequencing

Stage 3 should establish the trigger semantics, but the complete command/API capability matrix remains in `NARRATIVE_TUTORIAL.md` and must be reconciled in Stage 4 before enforcement.

The current proposed main-path sequence is:

1. Bootstrap: unlock `player.inspect` (only `status`).
2. Successful `status`: unlock `host.controls` and `network.scan`.
3. First successful scan: unlock `network.analyze`.
4. First successful analysis: unlock `network.connect`, `network.inspect` and `player.profile`.
5. First successful connection: unlock `server.root-access`.
6. First root acquisition: unlock server operations, backdoor access and file navigation/read/management.
7. First successful hack: unlock `scripts.execute`.
8. First successful script creation: unlock script inspection/control and the optional botnet, cloud and activity capability groups.

Do not unlock every future command at once just because a milestone was reached. Keep capability groups narrow enough that each unlock is understandable and does not reveal later mechanics prematurely.

## 7. Out-of-order actions and idempotency

### 7.1 Out-of-order player actions

In Tutorial mode, the command and scripting API gates introduced in Stage 4 should prevent actions whose capabilities have not yet been unlocked. The narrative coordinator should nevertheless be robust to duplicate or already-satisfied events (for example, restored state or a future alternate entry point).

In Freeplay mode, all actions are available and the coordinator does not run tutorial progression. Optional narrative content may also be disabled initially in Freeplay to keep the promise of a tutorial-free restart. Do not accidentally deliver tutorial unlock instructions to Freeplay players.

### 7.2 Idempotency

For every one-time trigger:
- stable milestone ID is checked before any side effect;
- repeated event does not re-unlock or re-emit;
- unlock and milestone state are committed before output is published;
- state is saved through the existing save mechanism;
- loading a Tutorial save resumes from the recorded state;
- loading a Freeplay save never initializes the tutorial.

### 7.3 Event payloads

Events should contain only the data the narrative rules need. Prefer immutable records such as the server ID/hostname, activity kind, result outcome or process ID/filename. Avoid passing mutable service internals or entire service objects to the coordinator.

Do not put narrative message text in service event payloads. Gameplay services report what happened; narrative rules decide what it means and which message to emit.

## 8. Proposed Core event additions

The following are the narrow additions likely needed after confirming the exact implementation design:

- `NetworkService.ServersDiscovered` — only newly discovered servers from a successful scan.
- `NetworkService.ServerConnected` — a successful change to a different current server.
- `ServerActions.RootAccessGained` — emitted only when `Nuke` newly grants root access.
- `ServerActions.HackCompleted` — emitted after a hack resolves, with success/failure and target identity.
- `ScriptService.ProcessStarted` — emitted only after the process is created and execution has been scheduled successfully.
- `BotnetService.NodePurchased` — emitted after successful node purchase.
- `CloudServerService.ServerPurchased` — emitted after successful cloud-server purchase.
- `ActivityService.ActivityStarted` — emitted after a course or job has started successfully. Include the activity kind and immutable identifying details.

Existing `ActivityService.CrimeAttempted` can be reused. Existing `JobPromoted` and `ActivityEnded` can support later optional messages if needed, but neither should be mistaken for first employment or course completion.

Do not add events for every upgrade, every tick, every hack/grow/weaken repetition or every activity progress update. Narrative events should be sparse and milestone-oriented.

## 9. Testing strategy

### Coordinator/domain tests

- Tutorial bootstrap emits the initialization message once and unlocks only `player.inspect`.
- Freeplay bootstrap emits no tutorial message and unlocks no tutorial state.
- Successful `status` unlocks host controls; a failed/unknown command does not.
- First successful scan triggers once only when new servers are discovered.
- Repeated scan with no new discovery does not retrigger.
- Successful connection triggers once; failed or redundant connection does not.
- Root-access milestone triggers only when root access is newly granted.
- Failed hack does not satisfy the first-successful-hack milestone.
- Script compile/start failure does not satisfy first-script milestone.
- Successful process start satisfies it once, regardless of whether the process later completes or fails.
- Failed purchases and activity-start requests do not trigger milestones.
- Crime introduction uses the resolved attempt event and occurs once.
- Optional activity milestones do not change mandatory main-path progression.
- Event order is deterministic when multiple relevant conditions occur in one game-thread operation.

### Session/persistence integration tests

- New Tutorial session automatically initializes before accepting the first command.
- New Freeplay session starts with normal access and no tutorial output.
- Existing version 7 saves migrate to Freeplay and do not replay initialization.
- Saving/loading during the tutorial preserves milestone, unlock and delivered-message state.
- A message is not emitted twice after loading a save taken immediately after its trigger.
- Coordinator subscriptions are installed exactly once per session and do not survive session replacement.

### Service tests

Add tests beside the owning service for each new event:
- event emitted after successful state mutation;
- no event on failure/no-op;
- event payload describes the committed result;
- observers cannot make a successful gameplay action fail (follow the existing defensive observer pattern where appropriate).

## 10. Explicitly deferred

- Full implementation of the coordinator and events (this document defines the design).
- Capability checks in every console command and scripting API operation (Stage 4).
- Full tutorial dialogue beyond the initial network/automation milestones.
- Recovered files and creator/SYSTEM mystery content.
- Journal UI or replayable message-body history.
- Optional activity messages beyond the first-use introductions.
- Any new gameplay systems or mandatory participation in optional activities.

## 11. Stage 3 outcome

Stage 3 is ready for implementation when the event catalog, ownership, trigger conditions, Freeplay behavior and tests are accepted. Implementation should begin with the coordinator/bootstrap and the small set of main-path events, then add optional service events only when their narrative messages are ready.
