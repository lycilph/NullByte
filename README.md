# NullByte: Game Design Document

Version 0.1, first iteration (console), with a later WPF + AvalonEdit front end.

## 1. Vision

NullByte is a text-driven incremental game where the player grows a passive income stream (botnet) and then automates the rest of the economy by writing C# scripts against a small game API (the `ns` object), compiled at runtime with Roslyn. Manual commands and scripts use the same underlying actions, so everything a script can do the player can also do by hand, only slower.

The central fantasy is the same as Bitburner: you start by typing commands, get bored, write a script, then write a better one.

### Pillars

1. **Scripting is the game.** Progress comes from writing better programs, not clicking faster.
2. **Same rules for hand and script.** Hack, grow and weaken are one implementation, called from the console or from `nb`.
3. **Deterministic, single-threaded simulation.** All game state is touched from exactly one thread, driven by a custom synchronization context. No locks in game code.
4. **Start small, grow later.** The first iteration is deliberately narrow, but the architecture must not block factions, augmentations, a UI, saving and so on.

## 2. Scope of the first iteration

### In scope

- A player with money and a hacking level (with XP).
- Botnet: buy nodes, upgrade level, RAM and cores, generate passive income.
- A small hand-authored network of servers (home plus roughly 10 to 15), each with hacking requirement, security, money and growth.
- Server actions: `scan`, `connect`, `nuke` (gain root), `hack`, `grow`, `weaken`, with variable duration and thread count.
- Scripts: write, compile, run, kill and list scripts, with RAM cost, threads and arguments.
- Home RAM as the limiting resource for scripts.
- A console that blocks input while a manual hack/grow/weaken runs.
- Save and load (late milestone).

### Out of scope (for now)

Factions, augmentations, programs and port opening (we may simplify to "nuke needs hacking level only"), purchased servers (stretch goal), stock market, corporations, Bladeburner, Sleeves, Gangs, Stanek, Singularity API, offline progress, multiplayer, and a GUI.
