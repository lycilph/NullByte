# Narrative Progression: Implementation Reference

## Purpose

This document records the implemented narrative progression architecture and the remaining work needed to grow it into a fuller story. It replaces the earlier staged implementation plan and separate state-design draft, which described work that has since been implemented.

The narrative's intended tone and long-term story are defined in [NARRATIVE.md](NARRATIVE.md). The player-facing tutorial sequence and command map are in [NARRATIVE_TUTORIAL.md](NARRATIVE_TUTORIAL.md).

## 1. Architectural responsibilities

Narrative progression is owned by Core and is driven by successful gameplay events.

- `GameState` owns the durable `NarrativeState`.
- `NarrativeCoordinator` translates relevant gameplay events into milestones, capability unlocks and host-neutral `NarrativeMessage` instances.
- `NarrativeContentCatalog` owns immutable authored entries, stable entry IDs, act metadata and milestone associations. The coordinator refers to entries by ID and supplies runtime values such as hostnames.
- `NarrativeEntry` renders simple `{placeholder}` values; it does not contain branching or conditional logic.
- `NarrativeAct` identifies the five high-level story acts. Act metadata is content organization, not a separately persisted progression counter.
- Gameplay services report what happened; they do not contain story text.
- The host subscribes to published messages and decides how to present them.
- Console command and scripting API boundaries consult the same capability state.
- The journal derives its tutorial guidance from unlocked capabilities rather than relying on terminal history.

The coordinator is session-scoped and unsubscribes from service events when disposed. Narrative state changes and subscribed event handling are expected to run on the game thread.

## 2. Durable state and persistence

The current save format is version 9. Version 9 adds persisted discovered-discovery IDs; version 8 introduced narrative progression. The narrative save object contains:

| Field | Meaning |
|---|---|
| `Mode` | `Tutorial` or `Freeplay` |
| `UnlockedCapabilities` | Stable capability IDs explicitly unlocked in Tutorial mode |
| `CompletedMilestones` | Stable IDs for one-time progression triggers |
| `DeliveredMessages` | Stable IDs preventing one-time SYSTEM messages from repeating |
| `DiscoveredDiscoveries` | Stable IDs for optional or required narrative discoveries the player has read (added in version 9) |

Capability, milestone and message IDs are stable namespaced identifiers, not command strings, display text, enum ordinals or array positions. Renaming an ID requires an explicit migration or compatibility handling.

Tutorial mode checks the explicit capability set. Freeplay treats every capability as available, including future capabilities, so old saves do not require a grant-list migration each time the game adds a feature.

Saves created before narrative gating are migrated by the version 7-to-8 migration to Freeplay with empty explicit collections. This prevents existing players from being forced through the tutorial.

The save mapper captures and restores narrative state alongside gameplay state. A restored Tutorial save resumes from its recorded milestones and unlocks. A Freeplay save does not initialize tutorial progression.

A separate chapter number is not persisted. Chapter/act progress can be derived from milestone history when richer story content requires it.

## 3. Current event-driven progression

The coordinator currently subscribes to:

| Event | Trigger condition | Current result |
|---|---|---|
| `NetworkService.ServersDiscovered` | At least one new host is discovered | First-scan milestone; unlock analysis |
| `NetworkService.ServerAnalyzed` | A host is successfully analyzed | First-analysis milestone; unlock connection and network inspection |
| `NetworkService.ServerConnected` | A connection succeeds | First-connection milestone; unlock root access |
| `ServerActions.RootAccessGained` | Root access is newly granted | First-root milestone; unlock server operations and backdoor |
| `ServerActions.HackCompleted` | A hack succeeds | First-hack milestone; unlock file operations and script execution |

The coordinator also exposes explicit entry points used by the host/session:

- `Initialize()` begins the Tutorial opening and unlocks `player.inspect`.
- `StatusUsed()` unlocks host controls and network scanning after a successful `status`.
- `ScriptStarted()` unlocks script inspection/control and the botnet, cloud and activity capability groups after a script process starts successfully.
- `GetTutorialGuide()` returns the capability-derived guidance displayed by `journal`.

Freeplay bypasses tutorial initialization and all tutorial unlock messages.

The coordinator's current scope is intentionally narrow. It does not yet subscribe to botnet purchases, cloud purchases or activity events to produce optional story messages. The authored catalog currently contains the initial Act I milestones and the first script milestone in Act II; later acts are placeholders in the act taxonomy, not implemented content.

### File-based discoveries

Optional story material is registered separately from tutorial milestones in `NarrativeDiscoveryCatalog`. Each discovery has a stable ID, a server hostname and normalized virtual-file path, plus an `Optional` or `Required` classification. The catalog is content metadata; it does not own gameplay state.

A successful player-facing `cat` read is reported to `NarrativeCoordinator.FileRead`. A matching discovery ID is recorded once in `NarrativeState.DiscoveredDiscoveries`. Reading the same file again does not repeat the discovery, and the file remains in the virtual filesystem so the player can revisit its contents at any time. Editor previews and internal file reads do not count as player discoveries.

Optional and required are narrative-content classifications only. Neither type unlocks or gates gameplay capabilities. Freeplay skips tutorial progression but still records discoveries. The default discovery catalog is currently empty; N4 will add the first recovered files and their registrations.

## 4. Idempotency and message delivery

Each one-time progression trigger has a stable milestone ID and message ID.

The coordinator:
1. ignores tutorial triggers in Freeplay;
2. checks whether the milestone has already been completed;
3. unlocks the associated capabilities;
4. records the milestone;
5. records the message as delivered before publishing it;
6. publishes a host-neutral message to observers.

Repeated events therefore do not repeat unlocks or messages. Observer failures are isolated so a presentation subscriber cannot cause a successful gameplay action to fail.

The message body itself is not currently stored as a replayable history. Only delivered-message IDs are persisted. The tutorial journal reconstructs essential guidance from capability state.

## 5. Capability enforcement

Capability access must be consistent across player-facing surfaces.

- Command definitions are associated with stable capability IDs.
- Command resolution, aliases, help, subcommands and completion respect the capability state.
- Scripting API operations enforce the corresponding capability at runtime.
- Internal game operations remain usable by trusted game code; restrictions belong at player-facing boundaries.
- Freeplay grants access without maintaining an explicit list of every known capability.

The full current player-facing mapping is maintained in [NARRATIVE_TUTORIAL.md](NARRATIVE_TUTORIAL.md).

## 6. Testing and invariants

The implementation should preserve these invariants:

- A new Tutorial session initializes once and makes only `status` available initially.
- A successful `status` unlocks host controls and scanning; failed or unknown commands do not.
- Only successful discoveries, analysis, connections, root acquisition, hacks and script starts advance their corresponding milestones.
- Failed actions and no-op repeats do not satisfy first-success milestones.
- Repeated events and save/load do not duplicate unlocks or one-time messages.
- Freeplay starts unrestricted and never emits tutorial unlock messages.
- Existing pre-narrative saves migrate to Freeplay.
- Capability restrictions cannot be bypassed through the scripting API.
- Session replacement disposes old coordinator subscriptions.
- Narrative output is host-neutral and does not own gameplay state.
- The default content catalog has unique entry and milestone IDs, and every implemented coordinator trigger resolves to a catalog entry.
- Runtime placeholder values are rendered without mutating the authored catalog.

Tests belong at the owning layer: domain/coordinator tests for progression, service tests for event semantics, command/API tests for enforcement, and persistence/session tests for save/load behavior.

## 7. Remaining narrative work

The current implementation is a tutorial progression foundation, not the complete story described in [NARRATIVE.md](NARRATIVE.md).

N1 (content model) and N2 (initial Act I content) are implemented. The current catalog deliberately keeps the opening practical. Its first anomaly is a low-key inherited audit-retention note after the first successful external operation; the first-script message adds a slightly more unusual environment-profile observation.

Next content work should be incremental:

1. N3 provides the persisted file-discovery foundation: stable IDs, optional/required classification and one-time registration on player reads.
2. N4 adds the first recovered files and uses them to hint at an earlier project or unexplained network entry without immediately explaining it.
3. Expand SYSTEM's voice gradually while keeping most messages practical.
4. Decide how recovered information is represented and discovered using the existing file/world model before adding new mechanics.
5. Extend the journal only when there is actual story content to index; preserve its current role as a reliable tutorial guide.
6. Add later-act progression only when there is enough playable content to support it.

Do not add a generic quest engine, branching dialogue framework or new gameplay mechanics merely to deliver the next few narrative fragments.