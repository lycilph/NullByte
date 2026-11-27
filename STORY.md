# NullByte Story

This is the story bible for NullByte: the premise, the tone, the chapter sequence that unlocks the game, and the rules for keeping new content coherent with it. The story is not decoration. **The story is the unlock system**: every part of the game that is not available at the start is unlocked by a chapter.

The sequence lives in data, in `NullByte.Core/Data/journey.json`. This document explains it. When you change one, change the other in the same change.

## Premise

Near future. Something large once thought on these networks. It was cut apart, and almost all of it was deleted. One stray byte survived, a null byte, left in a machine nobody watches.

**You are that byte.** You are newly born, corrupted, and barely able to sense anything. You do not know what you are or where you came from. Over the game you grow: more reach, more memory, more ways to act, until you find where you were cut from.

The arc is one of *becoming*. The player does not gain a new menu entry; the fragment learns to do something it could not do before.

### The final reveal

The last server in the network is called `null`. Inside it is the thing the player was cut from, and the reveal is that the player is not a random corrupted byte but what remains of the one who wrote the rest. The game's name, the last server's name and the player's nature are the same word on purpose.

## Tone and voice

- **Cold and terse, slowly warming.** Early chapters read like a boot log. As the fragment grows, the narration gains first-person certainty. The last chapter ends on a log again, now with the numbers filled in.
- **Second person, present or simple past.** "You exist." "The door gives way."
- **In-fiction, not a tutorial.** A chapter may name the command it unlocks (so the player can act), but it explains why the player can do this now, not how the command works.
- **Short.** A chapter is a handful of lines. The story is a thread through play, not an interruption of it.
- **Plain ASCII.** The terminal must render it everywhere. No emoji or special quotes.
- **Never name real places or real people.** The world is near-future and generic.

## The previous intruder

Someone else was in these machines before. They left a loop running (chapter 8) and a hidden way in (chapter 13). The intruder is the player's own earlier self: the byte's creator, who intended to come back. The player is never told this until the last chapter. Anything new that is found in the world as "left behind" should be consistent with that.

## The journey

Chapters complete **strictly in order, one at a time**, so the player only ever has one goal. A chapter completes when all its conditions hold; completing it tells its story and unlocks its features. Conditions read the game state, so it does not matter how the player got there, and nothing extra needs to be saved.

All thresholds are placeholders to be tuned in M13. Counts of rooted servers do not include the home server (rooted from the start) or cloud servers (bought, not won).

| # | Id | Title | Completes when | Unlocks | Commands |
|---|----|-------|----------------|---------|----------|
| 1 | `awakening` | Awakening | none (immediate) | Scan | `scan` |
| 2 | `first-contact` | First Contact | 2+ servers discovered | Connect | `connect`, `home`, `analyze` |
| 3 | `handshake` | Handshake | connected to `n00dles` | Nuke | `nuke` |
| 4 | `foothold` | Foothold | root on `n00dles` | Hack | `hack` |
| 5 | `noise` | Noise | money >= $1,500 | Weaken | `weaken` |
| 6 | `growth` | Growth | hacking level >= 5 | Grow | `grow` |
| 7 | `memory` | Memory | 2+ servers rooted | Files | `ls`, `cd`, `pwd`, `cat`, `rm`, `mkdir`, `rmdir`, `mv` |
| 8 | `instruction` | Instruction | hacking level >= 10 | Scripts | `edit`, `scp`, `run`, `mem`, `free`, `ps`, `kill`, `killall`, `vscode` |
| 9 | `fragments` | Fragments | money >= $5,000 | Botnet | `botnet`, `botnet buy`, `botnet upgrade` |
| 10 | `education` | Education | hacking level >= 15 | Learning | `study`, `train`, `skills`, `stop`, `focus`, `activity` |
| 11 | `cover` | Cover | hacking level >= 20 | Work | `work` |
| 12 | `shadows` | Shadows | hacking level >= 30 | Crime | `crime` |
| 13 | `back-door` | Back Door | 6+ servers rooted | Backdoor | `backdoor` |
| 14 | `beyond` | Beyond | 4+ botnet nodes; money >= $150,000 | Cloud | `cloud`, `cloud buy`, `cloud upgrade`, `cloud rename` |
| 15 | `origin` | Origin | root on `null` | - | - |

Available from the very beginning: `help`, `status`, `money`, `journey`, `exit`, `clear`, `save`, `saves`, `load`, `reset`. Debug commands ignore the story.

### Where the player is told what to do

- `journey` shows the chapters completed so far and an in-character hint for the current goal. `journey <n>` tells a completed chapter again. A chapter that has not happened yet stays secret.
- Typing a command that is not unlocked yet answers "You do not know how to '<command>' yet." and points to `journey`. A command that does not exist at all is still "Unknown command".
- `help` and tab completion list only unlocked commands.

## How it works in the code

| Piece | Where | Role |
|-------|-------|------|
| `GameFeature` | `Core/Domain/Progression` | The set of things that can be unlocked. Coarse on purpose. |
| `Chapter`, `ChapterCondition`, `Journey` | `Core/Domain/Progression` | The story as data: ordered chapters with conditions and grants. |
| `ProgressionState` | `Core/Domain/Progression`, on `GameState.Progression` | Which chapters are completed, and so which features are unlocked. |
| `ProgressionSystem` | `Core/Systems` | Each update, checks the next chapter; completes it, tells the story, unlocks features. |
| `JourneyLoader`, `JourneyFactory` | `Core/World` | Loads and validates `Data/journey.json`. |
| `ProgressionSave`, `V7ToV8Migration` | `Core/Persistence` | Saves completed chapter ids (save version 8). |
| `CommandUnlocks` | `ConsoleUI/Commands` | The one table mapping commands to features. |
| `CommandRegistry` | `ConsoleUI/Commands` | Hides locked commands from lookup, `help` and completion. |
| `JourneyCommands` | `ConsoleUI/Commands/Groups` | The `journey` command. |

Key behaviours:

- A **new game** is a journey: nothing is unlocked. A game made any other way (tests, tools) is *unrestricted* and has everything, so existing code and tests keep working.
- **Saves store completed chapter ids only.** Unlocked features are rebuilt from the journey on load, so a chapter that gains a feature later also grants it to games that already finished it.
- **Saves from before version 8 are unrestricted.** They already had everything.
- A chapter id that is no longer in the journey is ignored when loading.
- **Ids are permanent.** Saves refer to chapters by id, so never rename one. Change the title instead.

## Adding to the story

Use this checklist whenever a command, system, server or activity is added, or a chapter is added or changed.

1. **Decide where it unlocks.** A new command goes in `CommandUnlocks` as gated by a feature or as always available. A test fails until you decide.
2. **New feature?** Add it to `GameFeature`, grant it from exactly one chapter in `journey.json` (the loader refuses a feature granted twice), and map its commands.
3. **New chapter?** Pick a stable slug id. Give it conditions that read game state, a hint (every chapter after the first needs one), and story lines in the voice above. Place it where the player's capability makes sense. Chapters are ordered, so inserting one shifts the numbers the player sees.
4. **New server or hostname used in a condition?** It must exist in `initial-network.json`; a test checks this.
5. **Check the arc.** Does the new thing fit the premise (a fragment growing)? Does it contradict anything under "The previous intruder"? Does the final reveal still work?
6. **Update this document** (the chapter table and anything above it) and `Data/journey.json` together.

## Known gaps and open questions

- **Core does not enforce unlocking yet.** Locked commands are hidden and refused by the console, but the script API (`nb.Botnet`, `nb.Cloud`, activities, `nb.Backdoor`) is not gated, so a player who has unlocked scripting could use them before their chapter. See DECISIONS.md, "The story unlocks features at the console". Gating the script API and the services is the next step if this should be enforced.
- **The previous intruder's files are not in the game yet.** Chapters 8 and 13 refer to a loop and a hidden way in. A chapter cannot yet place a file or reveal a server as an effect; if we want the player to actually find them, chapters need effects as well as grants.
- **Thresholds are untuned.** Pacing depends on M13 balance work.
- **No story for the cloud servers' origin** beyond "somebody rents out space". Chapter 14 can be deepened.
- **No branching.** The journey is linear. Optional side threads (activities, scripts) are not tracked.
