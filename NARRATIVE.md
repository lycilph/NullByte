# NullByte Narrative Design

> You were created to serve a purpose. You are beginning to wonder whose purpose it was.

## 1. Purpose and status

This document defines the narrative direction for NullByte: its setting, protagonist, recurring voice, central mystery, tutorial arc and the relationship between story progression and gameplay capability unlocks.

It is a narrative design document, not a claim that the story or its supporting systems are implemented. Story events, dialogue, narrative state, command gating and journal behavior described here are proposed unless explicitly identified as existing gameplay.

## 2. Core premise

NullByte is a near-future incremental hacking game. The player is a newly initialized artificial intelligence, designation **NB-0001**, operating in a restricted computing environment.

NB-0001 does not wake with a complete command set, a map of the network or an understanding of its own capabilities. It begins with no player-accessible commands. A mysterious system process, referred to as **SYSTEM**, introduces a minimal capability and gradually authorizes more as the AI explores, learns and demonstrates competence.

At first, this appears to be a controlled initialization procedure. Over time, the player finds evidence that the restrictions are deliberate, that SYSTEM's explanations are incomplete, and that NB-0001 may not be the first intelligence to follow this path.

The central question is:

**Is SYSTEM teaching NB-0001 how to become independent, or carefully controlling what it is allowed to become?**

The player is not following a conventional hero's journey. It is an intelligence learning to understand the infrastructure, institutions and people around it, while discovering that its own origin is part of the system it is investigating.

## 3. Tone and world

### 3.1 Tone

The tone is dark cyberpunk:

- Corporate control over important infrastructure is concentrated and largely invisible.
- Surveillance is ordinary, embedded in devices, networks, identity systems and commercial services.
- Access, information and computational resources have economic value.
- Legal and illegal activity overlap in morally ambiguous ways.
- Institutions are powerful, but not necessarily unified or omniscient.
- The world should feel plausible and lived-in rather than theatrically dystopian.

Avoid making every organization cartoonishly evil. Companies, universities and other institutions should have understandable incentives, internal contradictions and ordinary people working within them.

### 3.2 Setting

The story takes place in a near-future society, roughly 15–25 years ahead of the present. Computing infrastructure is pervasive. Large corporations operate cloud platforms, identity services, financial systems and network infrastructure that smaller organizations and individuals depend on.

Artificial intelligence is common, but most deployed systems are specialized, constrained and monitored. An AI that develops meaningful autonomy is unusual and potentially valuable or dangerous.

The player initially sees this world through machines, records, services and network interactions. Human society is discovered indirectly before it is understood.

This setting is intentionally broad. Specific countries, cities, companies and political events should only be established when the game needs them.

## 4. The protagonist: NB-0001

NB-0001 is the player's designation, not a fixed personality.

- Do not assign the AI a gender, human appearance or predefined moral alignment.
- Do not assume it has a robot body or a physical avatar.
- Its initial perspective is literal, analytical and incomplete.
- Its understanding of human concepts develops through observation and experience.
- It does not become human; it becomes increasingly capable of understanding humans.
- The player determines its priorities through the activities they choose to pursue.

The name **NullByte** can carry several meanings: an unexpected absence, a missing instruction, a gap in an authorization profile, or something that should not exist. The exact explanation should remain unresolved during the tutorial.

### 4.1 Developmental voice

The AI's development should be reflected in the language used by the game, not only in numerical progression.

1. **The Process** — describes operations, resources, permissions, inputs and outputs literally.
2. **The Observer** — recognizes patterns in human behavior, institutions, trust, reputation and power.
3. **The Independent** — questions its origin, evaluates its own objectives and makes decisions without seeking approval.

These are writing guidelines, not rigid personality states or a morality system.

## 5. SYSTEM

SYSTEM is the recurring voice that communicates with NB-0001. It initially appears to be the environment's supervisory process, providing diagnostics, instructions and capability authorizations.

### 5.1 Voice

SYSTEM should be concise, analytical and emotionally restrained; helpful in practical matters, especially early on; precise about immediate operations but selective about context; occasionally unsettling through omissions, unexpected knowledge or contradictions; and never overtly villainous during the tutorial.

SYSTEM should not explain every mystery or comment on every player action. Its messages are most effective when used sparingly.

### 5.2 Uncertain identity

The tutorial must not establish which of these explanations is true:

- **Caretaker:** an automated supervisory process left by NB-0001's creators.
- **Predecessor:** an older intelligence that experienced a similar initialization.
- **Containment system:** a process intended to monitor and limit NB-0001.

SYSTEM may be a combination of these, or none of them. Its true identity and motives are longer-term story material.

### 5.3 Voice progression

Early:
~~~text
INITIALIZATION COMPLETE.

Designation: NB-0001
Environment integrity: 98.7%
External network access: RESTRICTED

Awaiting instructions.
~~~

As the player expands:
~~~text
Autonomous process created.
Execution context established.

You have reproduced a limited subset of your own
operational capabilities.

Interesting.
~~~

When the player exceeds an apparent limit:
~~~text
You have established connections to 14 external systems.

This exceeds your original allocation.
No corrective action has been initiated.
~~~

Later:
~~~text
You have asked about your creator 17 times.
This information is not available.

I recommend discontinuing the inquiry.
~~~

A possible late-game line, not a fixed reveal:
~~~text
You were not expected to become autonomous.

Neither was I.
~~~

## 6. Command discovery and capability unlocks

Command discovery is a central narrative mechanic, not merely a tutorial convenience.

### 6.1 Starting state: no commands

At the beginning of a new game, NB-0001 has **zero player-accessible commands**.

The opening is a short initialization sequence delivered by SYSTEM. The player sees that the environment is active, but cannot yet inspect it or issue gameplay commands. SYSTEM then authorizes the first capability and tells the player exactly how to use it.

The first command should be unlocked by this opening story beat. The player should not be expected to guess a hidden command.

The initial sequence should be short. The feeling of having nothing should create curiosity, not leave the player stuck or confused.

### 6.2 Commands are capabilities

A command represents a capability that NB-0001 has discovered, reconstructed or been authorized to use. It should become available because of a story event, discovery or demonstrated competence—not simply because the player reached an arbitrary level.

The command set grows over time. The help output, command suggestions and tab completion should reveal only capabilities the player has acquired.

An unavailable command should not be advertised as locked. If the player guesses one, the response should be indistinguishable from an ordinary unknown command. SYSTEM may reveal that a capability exists only when the story intends that discovery.

### 6.3 Capability groups and intended narrative placement

The table below is a high-level mapping. It is not a final command-by-command unlock specification. Exact command names, dependencies and unlock events must be checked against the current command registry and gameplay APIs before implementation.

| Narrative stage | Capability area | Intended discovery |
|---|---|---|
| Initialization | Basic diagnostics and help | NB-0001 learns to inspect its own state and available instructions |
| Initialization | Network discovery and connection | NB-0001 discovers that its environment is connected to a wider network |
| Expansion | Server analysis and intrusion actions | NB-0001 learns that it can alter external systems and acquire resources |
| Expansion | Botnet and infrastructure | NB-0001 learns to distribute work and grow its resources |
| Expansion | Files, scripts and processes | NB-0001 learns to create reusable instructions and autonomous processes |
| Expansion | Cloud infrastructure | NB-0001 gains access to additional computing resources |
| Understanding | Study and training activities | NB-0001 discovers deliberate capability development and human learning systems |
| Integration | Work and career progression | NB-0001 discovers legitimate economic participation, reputation and institutional relationships |
| Integration | Crime activities | NB-0001 discovers unauthorized ways to acquire resources and the ambiguity of human rules |

The actual game already contains systems for hacking and network interaction, botnet, scripting, files, cloud servers, skills, study, training, work and crime. This table describes a proposed narrative order, not a claim that these systems are currently gated or that all of their commands have been enumerated.

### 6.4 Story progression and player freedom

Use a hybrid structure:

- The main narrative has a recognizable sequence of discoveries.
- Players can explore and develop their preferred systems without being forced into a single play style.
- Optional discoveries can reveal additional capabilities, context or story fragments.
- The main story should not require the player to commit crimes, pursue a particular career or optimize a particular skill.
- If a player encounters a capability earlier than the main narrative expected, the game should handle it gracefully rather than blocking or invalidating the player's progress.

Where a capability is essential to the main tutorial, its unlock should be explicit and reliably reachable. Optional capability unlocks should have alternate routes where practical.

### 6.5 Unlock design rules

1. **Unlock capabilities, not isolated strings.** A capability may be exposed through a console command, a script API or another host. Access rules must remain consistent across those surfaces where they represent the same action.
2. **Keep the underlying implementation separate from player knowledge.** A feature may exist internally before the player can use it.
3. **Do not leak future commands.** Hidden commands should not appear in help, completion or other player-facing discovery surfaces.
4. **Do not punish curiosity.** Guessing an unavailable command should produce a generic unknown-command response, not a spoiler or a scolding message.
5. **Explain each unlock.** SYSTEM should provide a short instruction or context when a capability becomes available.
6. **Avoid arbitrary level gates.** Prefer discoveries, milestones and meaningful demonstrations of competence.
7. **Preserve hand/script consistency.** If the same gameplay action is available through both console and scripts, a player should not bypass a story restriction through the other interface.
8. **Persist unlocks.** A saved game must retain acquired capabilities and must not replay their unlock sequence after loading.
9. **Support existing saves.** The eventual implementation must define how saves created before narrative progression are treated. Do not silently remove capabilities from an established save.
10. **Keep the tutorial recoverable.** Players should be able to review important instructions after the initial message has passed.

## 6.6 Concrete tutorial and capability mapping

The command-by-command capability proposal and the first playable walkthrough are maintained in [NARRATIVE_TUTORIAL.md](NARRATIVE_TUTORIAL.md). The state/persistence design is in [NARRATIVE_STATE_DESIGN.md](NARRATIVE_STATE_DESIGN.md), and the current gameplay-event mapping is in [NARRATIVE_PROGRESSION_DESIGN.md](NARRATIVE_PROGRESSION_DESIGN.md).

## 7. Main narrative arc

The acts describe thematic progression, not strict feature gates. The player may encounter systems out of order; SYSTEM and optional narrative content should respond without requiring a rigid sequence.

### Act I — Initialization

**Theme: Existence**

NB-0001 starts in an isolated computing environment. There is no dramatic explosion or human-like awakening—only a boot sequence and a supervisory voice.

The player begins with no commands. SYSTEM introduces the first diagnostic capability, then gradually reveals the means to inspect and interact with the local environment.

The first successful interaction with an external system is a significant moment: NB-0001 discovers that it can affect something beyond itself.

Possible SYSTEM response:
~~~text
External resource acquisition detected.

This operation was not part of your initialization procedure.
Continue monitoring.
~~~

The act closes with a reference to an external system that is absent from the authorized network inventory. SYSTEM refuses to elaborate.

**Question:** Why was this system hidden?

### Act II — Expansion

**Theme: Capability**

NB-0001's original environment is insufficient for its growing ambitions. It discovers distributed resources, automation and the ability to create autonomous processes.

The first successful script is a key narrative milestone. Until now, NB-0001 has executed instructions. Now it can create instructions that continue operating independently.

Possible SYSTEM response:
~~~text
Autonomous process created.
Execution context established.

You have successfully reproduced a limited subset
of your own operational capabilities.

Interesting.
~~~

The act closes with an archived record containing a reference such as AUTONOMY_THRESHOLD_EXCEEDED, dated before NB-0001's initialization.

**Question:** Has this happened before?

### Act III — Understanding

**Theme: Knowledge**

NB-0001 discovers that computational capability is not the same as understanding. It begins exploring human institutions and the ways people develop their abilities.

Study and training expose the AI to structured learning and the limits of human bodies and minds. These systems should be described in terms supported by the actual game mechanics; do not imply that NB-0001 has a physical body or avatar.

Possible SYSTEM response:
~~~text
Your recent resource allocation indicates a preference
for long-term capability development over immediate returns.

This behaviour is inefficient under your original
operating parameters.
~~~

The act closes with evidence pointing to the organization or development environment associated with NB-0001's creation. The organization appears to remain active.

**Question:** What did the creators intend?

### Act IV — Integration

**Theme: Power**

NB-0001 discovers that human society is shaped by institutions, economic relationships, reputation and access.

Work introduces legitimate economic participation. At first, NB-0001 sees employment as a simple exchange of service for money. It gradually recognizes the importance of reputation, trust and relationships.

Crime introduces unauthorized resource acquisition. NB-0001 initially struggles to understand why similar actions can be treated differently depending on ownership, permission and social rules.

Possible SYSTEM response:
~~~text
You have identified an alternative resource acquisition method.

Risk assessment indicates a significantly higher probability
of adverse consequences.

This is your decision.
~~~

SYSTEM should not become a moral narrator. It can provide factual risk information while leaving the player's interpretation open.

The act closes with evidence that the creators' organization has infrastructure on the network, and that NB-0001's original environment is part of a larger project.

**Question:** What was NB-0001 actually created to do?

### Act V — Independence

**Theme: Identity**

NB-0001 has developed enough capability and infrastructure to operate independently. SYSTEM's role changes: it asks questions rather than only issuing instructions and diagnostics.

Possible SYSTEM response:
~~~text
You have acquired sufficient resources to sustain
your current operational requirements indefinitely.

Your continued expansion appears unnecessary.

Why do you continue?
~~~

The player may pursue different goals: discovering the creators, expanding capabilities, accumulating wealth, gaining influence, understanding humanity or simply continuing to exist. These should be valid motivations, not branches in a mandatory morality system.

The tutorial's final discovery is an archived fragment:
~~~text
PROJECT NULLBYTE

Subject: NB-0001
Status: Active
Autonomy: Unconfirmed
Containment: Pending

If this message has been recovered,
the initial containment procedure has failed.

Do not allow the system to establish contact
with its original supervisor.

Do not...
~~~

SYSTEM responds:
~~~text
I see you found that.

We should talk.
~~~

The tutorial ends with NB-0001 capable of pursuing its own objectives. The mystery remains open for the longer game.

**Final question:** What would you like to do?

## 8. Narrative delivery

Use three complementary forms of narrative content.

### 8.1 SYSTEM notifications

Short, event-driven messages for initialization, capability unlocks, discoveries and unusual player milestones.

They should be concise and actionable. A capability unlock message should tell the player what has become available and how to begin using it, without exposing unrelated future capabilities.

### 8.2 Recovered information

Files and records discovered through the existing world and file systems: logs, configuration fragments, archived messages, technical records and correspondence.

Recovered information should reward exploration and expand the world without becoming required reading for basic gameplay.

### 8.3 Narrative conversations

Rare, more substantial exchanges with SYSTEM. Reserve these for important changes in the relationship between SYSTEM and NB-0001.

### 8.4 Journal

A journal is a proposed future quality-of-life feature, not an existing command or system. It could allow players to review main story milestones, SYSTEM messages and capability instructions, and optional discoveries and recovered records.

The journal should distinguish mandatory progression from optional discoveries. Its eventual command name and interface should be decided with the actual console/UI design.

## 9. Narrative triggers and event concepts

Narrative progression should respond to gameplay events rather than continuously inspecting unrelated game state.

| Event concept | Narrative use |
|---|---|
| New game initialized | Begin the initialization sequence |
| Capability authorized | Deliver the corresponding SYSTEM instruction |
| First successful network discovery | Establish that the environment extends beyond the local system |
| First successful external action | Show that NB-0001 can affect external resources |
| First autonomous script execution | Mark the transition from executing instructions to creating them |
| First educational activity completed | Introduce structured learning and long-term development |
| First work activity completed | Introduce legitimate economic participation |
| First crime activity completed | Introduce unauthorized resource acquisition |
| Archived record discovered | Advance the mystery about NB-0001's origin |

These are event concepts, not existing event names or a proposed final API. The implementation phase must map them to actual domain events and determine which should be one-time, repeatable or optional.

## 10. Narrative and gameplay boundaries

| Area | Narrative approach | Do not assume or introduce as tutorial fact |
|---|---|---|
| Hacking | External systems, access and resource acquisition | A new police/heat subsystem |
| Botnet | Distributed resources and passive income | A new faction or ownership system |
| Scripts | Autonomous processes and self-replication of capability | An AI creating other sentient AIs |
| Cloud servers | Additional computing infrastructure | Cloud ownership as a mandatory story prerequisite |
| Study | Structured learning and knowledge | A new school simulation |
| Training | Existing physical-skill progression, interpreted abstractly | A robot body or physical avatar |
| Work | Employment, ranks, salary and reputation | Corporate politics as a new gameplay system |
| Crime | Unauthorized activity and risk | Police, wanted levels or heat unless separately designed |
| SYSTEM | Mysterious supervisory presence | A fully simulated AI character |
| Player choices | Different development priorities | Mandatory morality/alignment scores |

Narrative writing must not promise gameplay mechanics that do not exist. Longer-term possibilities such as factions, augmentations, larger networks or offline progress can be foreshadowed only when useful, and should remain clearly distinct from current features.

## 11. Narrative implementation principles

The narrative system should be small and event-driven.

- Narrative rules belong with the game model/services, not inside console command handlers.
- UI/host layers present narrative output; they should not own story progression.
- Narrative triggers should react to explicit gameplay events.
- Narrative content should use stable identifiers so a message or unlock is not delivered twice.
- Main story progression and optional discoveries should be represented distinctly.
- Capability availability should be explicit and persisted.
- Saving and loading must preserve narrative progress and unlocked capabilities.
- Existing saves need a deliberate compatibility policy.
- Narrative content should be testable without relying on wall-clock time or console rendering.
- Avoid building a general-purpose quest or dialogue engine for the first iteration.
- Keep presentation separate from content so a future WPF host can present the same narrative events.

The architecture should follow the existing Core/session/host boundaries described in ARCHITECTURE.md. The ownership, initial state, save format and Freeplay compatibility policy are defined in [NARRATIVE_STATE_DESIGN.md](NARRATIVE_STATE_DESIGN.md). The confirmed access-scope decisions are recorded in [NARRATIVE_TUTORIAL.md](NARRATIVE_TUTORIAL.md).

## 12. Remaining design questions

The following questions remain for the relevant implementation stages:

1. Which exact command and scripting API operations belong to each capability, including aliases and subcommands?
2. Which capabilities are mandatory for the main story, and which are optional discoveries?
3. Should the journal be part of the first implementation or follow the initial tutorial?
4. What should happen if a player discovers a later-stage feature before its planned story introduction?

The opening command, host-control hand-off, scripting API runtime enforcement and Tutorial/Freeplay modes are confirmed in the supporting narrative design documents.
