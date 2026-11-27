# Narrative State and Persistence Design

## 1. Purpose

This document records the Stage 2 design for persistent narrative progression and command capability access.

It defines the proposed Core state, save representation, migration policy and invariants before runtime implementation. It does not implement these concepts.

The design follows the existing architecture: mutable gameplay state belongs to Core and is owned by the game thread; persistence uses a separate versioned save contract and explicit migrations; hosts present narrative output but do not own progression rules.

## 2. Confirmed design decisions

The following decisions were made for the tutorial:

1. **Literal zero-command start.** At the beginning of a new game, no console commands are available—not even `help`, `save`, `load`, `exit`, `clear` or `reset`.
2. **First command is `status`.** SYSTEM automatically presents the initialization sequence and explicitly tells the player when `status` becomes available. The player never has to guess it.
3. **Runtime API enforcement.** Script API calls will be checked at runtime against the same capability state as console commands. The API interface and IntelliSense may still expose unavailable members in the first implementation.
4. **Freeplay-save compatibility.** Saves created before narrative gating must retain access to all existing and future capabilities without replaying the new-game tutorial or being forced through the new unlock sequence.

## 3. State ownership

### 3.1 Core owns the authoritative state

Narrative and capability progression belongs in the Core game model, not in ConsoleUI, Shell or the command registry.

The proposed ownership is:

- `GameState` owns a `NarrativeState`.
- Core narrative logic applies milestones, unlocks and message-delivery records.
- ConsoleUI subscribes to or receives host-neutral narrative output and renders SYSTEM messages.
- Command definitions declare their required capability; command resolution checks the current `GameState`.
- Script API implementations check the same capability state before delegating to gameplay services.
- Save mapping captures and restores narrative state alongside the rest of `GameState`.

This keeps the command registry and scripting API as adapters over one authoritative access policy.

### 3.2 Proposed state shape

The initial state should remain deliberately small:

| Field | Type / shape | Purpose |
|---|---|---|
| Progression mode | Stable enum: `Tutorial` or `Freeplay` | Distinguishes a new narrative-gated game from a pre-narrative save |
| Unlocked capability IDs | Set of stable strings | Records capabilities explicitly acquired in tutorial mode |
| Completed milestone IDs | Set of stable strings | Prevents milestone triggers from being applied more than once |
| Delivered message IDs | Set of stable strings | Prevents one-time SYSTEM messages from being delivered more than once |

Suggested domain shape (illustrative, not final code):

~~~csharp
public sealed class NarrativeState
{
    public NarrativeProgressionMode Mode { get; }
    public IReadOnlySet<string> UnlockedCapabilities { get; }
    public IReadOnlySet<string> CompletedMilestones { get; }
    public IReadOnlySet<string> DeliveredMessages { get; }
}
~~~

The actual API should preserve the project's normal domain encapsulation and validation style; callers should use methods such as `HasCapability`, `UnlockCapability`, `CompleteMilestone` and `MarkMessageDelivered`, rather than mutating collections directly.

### 3.3 Why use a Freeplay mode?

A freeplay save should not be migrated by writing a list of every capability known today. That would create a maintenance trap: every future capability would need another migration or an explicit grant to avoid restricting old players.

Instead:

- `Tutorial` mode uses the explicit unlocked-capability set.
- `Freeplay` mode treats every capability as available.
- A freeplay save therefore remains compatible when new capabilities are added in later versions.
- The freeplay mode is permanent for that save unless a deliberate conversion feature is designed later.

Freeplay mode also marks the tutorial as bypassed. It must not replay initialization messages or trigger first-time tutorial milestones merely because the older save did not record them.

### 3.4 Stable identifiers

Capability, milestone and message IDs are stable, namespaced identifiers—not command text, display text, enum ordinals or array positions.

Examples:

- `player.inspect`
- `network.scan`
- `server.root-access`
- `tutorial.initialization.complete`
- `tutorial.network.first-scan`
- `system.initialization`
- `system.capability.status`

IDs must not change when a command is renamed, its description is edited or the narrative text is rewritten. Renaming a capability ID requires an explicit migration or compatibility alias.

Collections should be serialized in ordinal sorted order to preserve deterministic save output. Duplicate IDs should be rejected or normalized at the domain boundary; they must never result in duplicate unlocks or messages.

## 4. Initial state and opening sequence

### 4.1 New game

A new game starts in `Tutorial` mode with:

- no unlocked gameplay capabilities;
- no completed narrative milestones;
- no delivered narrative-message IDs.

There is no command allow-list for host/session controls at this point. The command resolver exposes nothing.

### 4.2 Automatic initialization

After a new session is created and the host is ready to accept input, the narrative bootstrap emits the initialization text without requiring a command. It then completes the initialization milestone and unlocks `player.inspect`, which makes `status` available.

The intended player-visible order is:

1. SYSTEM initialization text is displayed.
2. SYSTEM says the diagnostic interface can be restored.
3. SYSTEM explicitly tells the player to use `status`.
4. The `player.inspect` capability is granted.
5. The input prompt becomes active with `status` as the only available command.

The zero-command state is real, but brief and automatically resolved by the opening sequence. The player is not expected to type an unknown command to start the game.

### 4.3 Host controls after the first command

The literal decision applies to the initial state. Host/session commands must not be available before `status` is used.

After the player successfully runs `status`, the tutorial should unlock a separate `host.controls` capability, exposing the normal host commands such as `help`, `save`, `load`, `saves`, `reset`, `clear` and `exit` (plus optional integrations such as `vscode` when configured).

These commands are not part of NB-0001's in-world capability profile and should not be framed as abilities discovered in the fiction. They are simply withheld during the short initialization hand-off.

This is the proposed resolution for preserving the literal opening while avoiding a permanently trapped player. The host itself may still be closed using ordinary operating-system/window controls; those are not game commands.

## 5. Milestones, unlocks and messages

### 5.1 Keep the concepts distinct

- **Milestone:** a durable fact that a narrative-relevant gameplay event has occurred.
- **Capability:** permission to use a gameplay operation.
- **Message:** player-facing text emitted by SYSTEM.

They often happen together, but they are not interchangeable. For example, the first successful scan can complete a milestone, unlock analysis and deliver a SYSTEM message. The milestone prevents the trigger from repeating; the capability controls access; the message ID prevents the text from being delivered twice.

### 5.2 Idempotency

Applying the same milestone more than once must have no additional effect:

- a capability remains unlocked;
- a milestone remains completed;
- a one-time message remains delivered once;
- no duplicate notification is emitted.

The narrative coordinator should apply the state change on the game thread. The ordering between state mutation and presentation must be explicit so a session reload cannot replay an already-recorded one-time message.

### 5.3 Message delivery and journal scope

A full journal is deferred. For the first implementation, delivered-message IDs are retained only to guarantee one-time delivery and future review capability; this design does not require storing every rendered message body in the save.

If later requirements include a replayable journal, the save model can be extended with stable content references or message records. Do not store localized/rendered text as the identity of a message.

### 5.4 Chapter state is derived

Do not persist a separate current chapter number in the first version. Chapter/act progress can be derived from completed milestone IDs. This avoids redundant state that can disagree with the actual unlock and discovery history.

If later content needs explicit branching or chapter checkpoints, introduce that only when a concrete use case requires it.

## 6. Capability access invariants

1. A new Tutorial-mode game begins with an empty capability set.
2. `status` is the first gameplay command unlocked by the initialization sequence.
3. Host/session commands remain unavailable until the player successfully executes `status`.
4. A command is available only if its declared capability is unlocked, unless it is explicitly part of the post-initialization host-control set.
5. A locked command resolves exactly like an unknown command: no help entry, completion result, alias, usage or subcommand disclosure.
6. Runtime script API calls require the same capability as the corresponding console operation.
7. A script cannot gain access by calling a lower-level API route that bypasses the intended capability boundary.
8. Freeplay mode grants all capabilities, including capabilities introduced by future updates.
9. Unlocking a capability is idempotent and persisted.
10. Narrative progression is deterministic and mutated only on the game thread.

The first iteration enforces scripting restrictions at runtime. Editor completion and compile-time hiding of API members are explicitly out of scope.

## 7. Persistence design

### 7.1 Save contract

The current save format is version 7. The narrative state should be added as a required top-level `Narrative` save object in the next format version.

New-game creation should offer two progression modes: `Tutorial` (the default, with the zero-command opening) and `Freeplay` (starts with all capabilities available and skips the tutorial). The existing restart/new-game flow should expose this choice without requiring a story command.

Proposed shape:

~~~json
{
  "narrative": {
    "mode": "tutorial",
    "unlockedCapabilities": [
      "player.inspect"
    ],
    "completedMilestones": [
      "tutorial.initialization.complete"
    ],
    "deliveredMessages": [
      "system.initialization",
      "system.capability.status"
    ]
  }
}
~~~

The exact property names should follow the project's current camel-case JSON convention. Arrays are serialized in ordinal order.

The domain model and save DTO remain separate. Do not serialize the live domain object directly.

### 7.2 Version 7 to version 8 migration

Add a `V7ToV8Migration` and register it in `SaveMigrator`. The migration should add the narrative object with:

- `mode = freeplay`;
- empty unlocked-capability, milestone and delivered-message arrays.

The empty capability list is intentional: Freeplay mode means all capabilities are available. This avoids hardcoding today's capability inventory into the migration and keeps old saves compatible with future capabilities.

The migration must be deterministic and safe to run only once in the normal sequential migration pipeline. The typed save contract must require the narrative object after migration.

### 7.3 Capture and restore

- `SaveGameMapper.Capture` copies the narrative mode and sorted ID collections from `GameState.Narrative`.
- `SaveGameMapper.Restore` validates the mode and IDs, creates the domain `NarrativeState`, and attaches it to the restored `GameState`.
- A restored Tutorial-mode save resumes exactly where it left off.
- A restored Freeplay-mode save remains unrestricted and does not replay the tutorial.
- Running scripts are restarted only after the restored state is available; their API instances must consult the restored narrative state.

### 7.4 Validation

Malformed narrative data should fail save restoration with the existing save-format error path. Validate:

- known progression mode;
- non-null collections;
- non-empty, well-formed stable IDs;
- no duplicate IDs;
- no invalid capability grants or milestone/message records where the implementation has a registry to validate against.

Unknown future IDs should not be silently converted into known IDs. Whether unknown IDs should be ignored with a warning or rejected should follow the project's existing compatibility conventions; the first implementation should prefer strict validation for malformed data and should not silently grant access.

## 8. Test design

Stage 2 implementation should include tests at the owning layer.

### Domain state

- New Tutorial-mode state has no capabilities, milestones or delivered messages.
- Freeplay-mode state reports every capability as available.
- Unlocking a capability twice has one effect.
- Completing a milestone twice has one effect.
- Marking a message delivered twice has one effect.
- Invalid IDs and invalid mode values are rejected.

### Persistence

- Capture/restore preserves Tutorial-mode capability, milestone and message state.
- Capture/restore preserves Freeplay mode.
- Serialized collections are deterministic and ordinally sorted.
- A version 7 save migrates to version 8 Freeplay mode with empty explicit collections.
- Saves from earlier supported versions still migrate through version 8.
- The existing golden-save fixture is updated deliberately for the new version and shape.
- A malformed narrative object fails with the normal save-format error.

### Integration invariants

- A fresh session performs automatic initialization and unlocks only `player.inspect`.
- Before `status` is successfully executed, no command—including host controls—is available.
- After `status`, host controls become available.
- Loading a freeplay save does not run the new-game initialization.
- A restored Tutorial-mode save does not replay already-delivered one-time messages.
- Script API instances created after load observe the restored capability state.

## 9. Explicitly deferred

- Runtime implementation of narrative state and capability checks.
- Full tutorial content and later act triggers.
- A journal or persistent message-body history.
- Branching dialogue, choices and morality/alignment.
- Capability-aware IntelliSense/editor completion.
- A general quest/dialogue framework.
- Conversion of a Freeplay save into Tutorial mode.
- Any changes to starting money, home server, network topology or existing gameplay balance.

## 10. Stage 2 outcome

Stage 2 is complete when this state/persistence design is accepted and the implementation can proceed without unresolved questions about state ownership, new-game defaults, old-save compatibility or idempotency.

The next implementation stage is Stage 3: event-driven narrative progression. The current service/event map and proposed trigger contracts are documented in [NARRATIVE_PROGRESSION_DESIGN.md](NARRATIVE_PROGRESSION_DESIGN.md).
