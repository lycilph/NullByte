# Narrative Implementation Plan

## 1. Purpose

This plan turns the narrative direction in NARRATIVE.md into a staged, testable implementation for NullByte.

The goal is to introduce a narrative tutorial that gives the player a reason to explore the existing game systems, while making capability discovery—especially hidden console commands—part of the fiction.

This plan deliberately starts with documentation and design. It does not assume that a quest engine, narrative state, capability gating or journal already exists.

## 2. Guiding constraints

- **Story before implementation:** agree on the player-visible sequence and unlock rules before changing gameplay code.
- **Start with nothing:** a new game begins with no player-accessible gameplay commands. SYSTEM delivers a short opening sequence and explicitly unlocks the first command.
- **Unlock capabilities, not command strings:** related console commands and scripting API operations must share the same access rule where they represent the same gameplay capability.
- **Hide unavailable capabilities:** locked commands must not appear in help, tab completion or other player-facing discovery surfaces. Guessing one should produce a generic unknown-command response.
- **Do not break existing progression:** established saves must not unexpectedly lose access to features they already use.
- **Keep the narrative optional where possible:** the main arc provides direction, while optional discoveries and different play styles remain viable.
- **Use existing gameplay:** do not add new police, faction, body/robot, corporate-politics or morality systems merely to support the story.
- **Follow the architecture:** gameplay and progression rules belong in Core; console and future UI hosts present them. Follow the game-thread and persistence conventions in ARCHITECTURE.md.
- **Avoid overengineering:** begin with milestones, capability availability and narrative messages. Do not build a general-purpose quest/dialogue framework unless real content later requires it.
- **No master commits without approval:** work on a feature branch. Ask before merging or committing anything to master.

## 3. Stages

### Stage 0 — Narrative design and repository alignment

**Status: Complete; access-scope decisions confirmed**

Deliverables:
- NARRATIVE.md — narrative premise, world, NB-0001, SYSTEM, five-act arc, command discovery, narrative delivery and boundaries.
- This plan — implementation stages, dependencies and acceptance criteria.
- [NARRATIVE_TUTORIAL.md](NARRATIVE_TUTORIAL.md) — current command/API capability map, proposed access model and first playable walkthrough.
- Verify the design against the current command registry, scripting API, game state and save model before implementation.

Acceptance:
- The story does not promise mechanics that are not implemented.
- Current commands and APIs can be mapped to capability groups without ambiguity.
- The opening sequence explains the first action and does not strand a new player.
- Existing-save behavior is explicitly designed.

### Stage 1 — Tutorial walkthrough and capability map

**Status: Complete; access-scope decisions confirmed**

**Goal:** define exactly what a new player experiences, before implementing the progression system.

Tasks:
1. Inspect the current console command registry, command groups, aliases, help output and tab completion.
2. Inspect the scripting API and identify operations that must be gated consistently with console capabilities.
3. Map each existing command/API operation to a named capability.
4. Identify prerequisites and dependencies between capabilities.
5. Write the opening tutorial as a step-by-step player walkthrough, including SYSTEM text, player input, expected output and unlock conditions.
6. Define main-path unlocks versus optional discoveries.
7. Decide how the narrative behaves when a player performs an action earlier than expected.
8. Decide the migration/compatibility policy for existing saves.

Deliverables:
- A capability matrix with stable capability IDs and the commands/API operations each capability exposes.
- A tutorial script for the initial playable arc.
- A list of gameplay events that drive narrative progression.

Acceptance:
- Every currently registered gameplay command and relevant scripting API operation has an explicit availability policy.
- No required tutorial step depends on an unavailable command.
- The player is never required to guess an undisclosed command.
- Optional play styles do not block the main story.

### Stage 2 — Narrative and capability state model

**Status: Drafted; awaiting review**

**Goal:** establish the smallest persistent representation of narrative progression and player access.

The proposed state ownership, Tutorial/Freeplay modes, capability/milestone/message IDs, new-game defaults, host-control hand-off, version 7-to-8 migration and Freeplay compatibility policy are documented in [NARRATIVE_STATE_DESIGN.md](NARRATIVE_STATE_DESIGN.md).

Deliverables:
- Core state ownership and invariants.
- Save model and V7-to-V8 migration design.
- Domain, persistence and integration test scenarios.

Acceptance:
- New Tutorial-mode games start with no capabilities and no commands.
- The automatic initialization sequence grants `player.inspect`, exposing `status` only.
- Successfully executing `status` grants `host.controls`.
- Unlocks, milestones and one-time message delivery are idempotent.
- Save/load preserves Tutorial and Freeplay state.
- Existing saves migrate to unrestricted Freeplay mode without replaying the tutorial.

### Stage 3 — Event-driven narrative progression

**Status: Design drafted; awaiting review**

**Goal:** translate gameplay milestones into narrative messages and capability unlocks.

The current service/event map, proposed Core event additions, trigger conditions, message delivery ordering, Freeplay behavior and test strategy are documented in [NARRATIVE_PROGRESSION_DESIGN.md](NARRATIVE_PROGRESSION_DESIGN.md).

Tasks:
1. Identify the owning Core events/services for the milestones in the tutorial.
2. Introduce a narrow narrative coordinator/service if needed; avoid embedding story logic in command handlers.
3. Connect explicit gameplay events to one-time narrative triggers.
4. Produce structured narrative output for the host to display.
5. Keep optional discoveries separate from mandatory chapter progression.
6. Ensure event handling is deterministic and safe across session replacement and save/load.

Deliverables:
- Core narrative progression behavior.
- Stable message and trigger identifiers.
- Tests for trigger conditions, duplicate delivery, ordering and optional events.

Acceptance:
- Relevant gameplay events produce the intended narrative result.
- Unrelated events do not advance the story.
- Replaying or reloading a session does not repeat one-time messages or unlocks.
- Narrative logic can be tested without terminal rendering.

### Stage 4 — Capability gating across player interfaces

**Goal:** ensure story unlocks control actual player access, not just what the console advertises.

Tasks:
1. Apply capability checks at the appropriate gameplay boundary.
2. Filter command registration/dispatch, help, aliases and tab completion based on acquired capabilities.
3. Ensure locked commands return the same generic response as unknown commands.
4. Gate corresponding scripting API operations so scripts cannot bypass restrictions.
5. Preserve consistent rules for console and script execution.
6. Review editor-facing functionality and any other interface that exposes or invokes gated operations.
7. Verify that infrastructure or activity commands with multiple subcommands are gated at the correct granularity.

Design caution:
- Do not rely solely on hiding command names in the console. If a capability is unavailable, the underlying gameplay operation must also reject access through any player-facing route.
- Conversely, do not gate low-level internal operations that are required by the game itself or by already-authorized systems.

Deliverables:
- Capability enforcement at the correct shared boundary.
- Command/help/completion integration.
- Scripting API enforcement where applicable.
- Tests covering direct invocation and bypass attempts.

Acceptance:
- A fresh game exposes only the first authorized capability after the opening sequence.
- Locked commands are absent from help and completion.
- Guessing a locked command does not disclose that it exists.
- Scripts cannot invoke a locked capability through the API.
- Unlocking a capability makes all intended entry points available together.
- Existing saves retain the access defined by the compatibility policy.

### Stage 5 — Initial tutorial content and presentation

**Goal:** deliver the first playable narrative arc through the existing host.

Tasks:
1. Implement the initialization sequence and the first command unlock.
2. Add concise SYSTEM messages for early network discovery and the first external action.
3. Introduce botnet and scripting as the AI discovers distributed resources and autonomous processes.
4. Add the first recovered records that establish the central mystery.
5. Ensure each unlock includes concise instructions.
6. Decide how the player can review missed instructions. A journal may be deferred if a simpler history/replay mechanism is sufficient.
7. Keep narrative presentation host-neutral so a future UI can reuse it.

Deliverables:
- A playable initialization and expansion tutorial.
- Narrative output through the current console host.
- A documented route for reviewing essential instructions.

Acceptance:
- A new player can complete the initial arc without external documentation.
- The narrative teaches the actual command behavior rather than replacing normal help.
- Story messages do not obscure important gameplay output.
- The tutorial does not force optional activities such as work or crime.

### Stage 6 — Extend through the existing activity systems

**Goal:** bring study, training, work and crime into the narrative without making them mandatory.

Tasks:
1. Add narrative introductions for study and training.
2. Add separate introductions for work and crime.
3. Connect first-use/completion events to optional or chapter-level discoveries as defined in Stage 1.
4. Review the writing against the actual activity model, durations, focused/background behavior and rewards.
5. Avoid implying new mechanics such as police response, heat, factions or physical avatars.

Acceptance:
- The narrative accurately describes the implemented activity behavior.
- Players can avoid any optional activity without becoming unable to continue the main arc.
- First-use messages do not repeat on every activity attempt.

### Stage 7 — Review, balance and documentation

Tasks:
1. Play through a fresh save from the zero-command start.
2. Test unusual orders: discovering capabilities early, repeating actions, skipping optional activities and loading during the tutorial.
3. Test an existing save using the agreed compatibility policy.
4. Review the experience for unclear instructions, excessive messages and dead ends.
5. Update NARRATIVE.md to reflect final implemented behavior.
6. Update ARCHITECTURE.md, CODEBASE.md, DECISIONS.md, ROADMAP.md and README.md only where the implementation changes their subject matter.
7. Keep the plan's status current.

Acceptance:
- A complete fresh-save walkthrough has no progression dead ends.
- Existing-save behavior is tested.
- One-time messages and unlocks remain stable through save/load.
- Documentation distinguishes implemented behavior from future ideas.

## 4. Proposed first playable scope

Keep the first implementation narrow.

**Include:**
- A new-game initialization sequence.
- Zero player-accessible commands at the initial state.
- A first explicit command unlock.
- A small number of subsequent command/capability unlocks that teach the existing network loop.
- The first successful external action as a narrative milestone.
- The first script as the transition into autonomous capability.
- Minimal persistent narrative state and save compatibility.

**Defer:**
- Full five-act dialogue and all optional narrative content.
- A general-purpose quest engine.
- Complex dialogue choices or branching morality.
- A fully featured journal, unless needed to make the tutorial usable.
- Factions, police/heat, corporate politics, robot bodies or other new gameplay systems.
- Any final reveal about SYSTEM or the creators.

## 5. Cross-cutting test checklist

- [ ] New game begins with no gameplay commands exposed.
- [ ] Opening sequence unlocks the first command without requiring the player to guess it.
- [ ] Help lists only acquired capabilities.
- [ ] Tab completion does not expose locked commands.
- [ ] A guessed locked command receives a generic unknown-command response.
- [ ] Aliases and subcommands follow the same capability policy.
- [ ] Script API cannot bypass capability restrictions.
- [ ] Unlock events are idempotent.
- [ ] One-time narrative messages are not duplicated.
- [ ] Save/load preserves unlocks and narrative progress.
- [ ] Existing-save migration/compatibility is covered.
- [ ] Optional activities are not required for main-path progression.
- [ ] Early or out-of-order player actions are handled without dead ends.
- [ ] Narrative output is deterministic and host-neutral.

## 6. Current status

| Stage | Status |
|---|---|
| 0 — Narrative design and repository alignment | Complete |
| 1 — Tutorial walkthrough and capability map | Complete (design draft) |
| 2 — Narrative and capability state model | Design drafted; awaiting review |
| 3 — Event-driven narrative progression | Design drafted; awaiting review |
| 4 — Capability gating across player interfaces | Not started |
| 5 — Initial tutorial content and presentation | Not started |
| 6 — Activity-system narrative | Not started |
| 7 — Review, balance and documentation | Not started |

Stages 0 and 1 are complete as design work. The access decisions are confirmed and recorded in [NARRATIVE_TUTORIAL.md](NARRATIVE_TUTORIAL.md). Stage 2's state and persistence design is in [NARRATIVE_STATE_DESIGN.md](NARRATIVE_STATE_DESIGN.md); Stage 3's event-driven progression design is in [NARRATIVE_PROGRESSION_DESIGN.md](NARRATIVE_PROGRESSION_DESIGN.md). Both are drafts awaiting review. No runtime implementation has begun.
