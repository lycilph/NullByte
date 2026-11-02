# NULLBYTE — C# Incremental Hacker Game

A Bitburner-inspired incremental/programming game in C#. Start tiny (a working
idle hacker in a console), then expand toward a scripting-driven game.

## 1. Design Pillars

- **Numbers go up, but choices matter.** Every purchase should be a tradeoff between money-per-second, upgrades, and unlocking new mechanics.
- **Automation is the content.** Early game is manual clicking; mid game is buying automation; late game is the player *programming* automation (the Bitburner hook).
- **Offline progress.** Incremental players expect progress while away — bake it in from the start.
- **Moddable content.** All game content (targets, upgrades, factions) should live in data files, not hardcoded, so adding content never requires touching the engine.

## 2. Core Loop (MVP)

1. Player runs **hacks** against a target → earns **money ($)** and **XP**.
2. Money buys **rig upgrades** (CPU, RAM, network) → faster hacks / passive income.
3. XP raises **skill level** → unlocks harder targets with better payouts.
4. Harder targets require better rig → feedback loop.

Currency list (keep it to one main currency for the MVP):
- **$ (money)** — universal.
- **XP/level** — progression gate.

Later currencies (Roadmap): **reputation** (factions), **research points** (tree), **crypto tokens** (prestige).

## 3. MVP Feature Set ("super simple")

| Feature | Description |
| --- | --- |
| Manual hack | Command/keypress that takes N seconds, yields $ based on target + rig |
| 5 targets | E.g. `webmail.zzz` → `bank-of-zzz`, escalating payout/requirements |
| Rig upgrades | 3–4 upgrade tracks (CPU: speed, RAM: concurrency, Net: offline efficiency) |
| Passive income | Hired bots / botnet that generate $/sec |
| Save/Load | JSON save file with offline progress on load |
| Console UI | Text commands + status bar; no graphics |

**Success criteria:** you can play 30 minutes, buy everything, and reset with offline earnings — all in a few hundred lines of C#.

## 4. Architecture

### Tech stack

| Layer | Choice | Why |
| --- | --- | --- |
| Language | C# / .NET 8 | As requested |
| Core engine | Plain class library (`Game.Core`) | No UI dependency; testable |
| MVP UI | Console (`Spectre.Console` optional) | Zero friction, iterate fast |
| Later UI | Blazor (web) or Avalonia (desktop) | Engine is UI-agnostic, so swap later |
| Scripting | Roslyn C# scripting (`Microsoft.CodeAnalysis.CSharp.Scripting`) — full C#, cooperative tick-aligned async/await model | The Bitburner-style hook; real language + IDE-grade editor (AvalonEdit + Roslyn classification) |
| Editor | AvalonEdit + Roslyn classification/IntelliSense | In-game script editor with real C# highlighting and completions |
| Save format | JSON via `System.Text.Json`, versioned | Easy debugging and migrations |
| Testing | xUnit on the engine | Incremental math is easy to unit test |

### Layer diagram

```mermaid
flowchart TD
  UI["Console / Web UI (thin)"] --> Game["GameSession (orchestrator)"]
  Game --> Loop["GameLoop (fixed tick, e.g. 10 Hz)"]
  Loop --> State["GameState (money, xp, upgrades, unlocks)"]
  Loop --> Systems["Systems"]
  Systems --> Eco["EconomySystem (production, costs)"]
  Systems --> Hack["HackSystem (active jobs, timers)"]
  Systems --> Prog["ProgressionSystem (levels, unlocks)"]
  Systems --> Script["ScriptingSystem (Roslyn: player C# scripts, tick-aligned)"]
  Systems --> Content["Content data (JSON: targets, upgrades, factions)"]
  Game --> Save["SaveSystem (JSON + offline catch-up)"]
```

### Key design rules

1. **Deterministic fixed-tick simulation.** All game logic advances in discrete ticks (e.g. 100 ms). The UI never mutates state; it sends *commands* (`BuyUpgrade`, `StartHack`). This makes offline progress trivial: on load, fast-forward `elapsedTicks = Δtime / tickDuration`.
   - Use `double` for currency (incremental numbers get big; switch to a custom BigNum if exponents exceed ~1e15).
2. **Command pattern.** UI → `GameSession.Execute(ICommand)` → engine applies and emits events. Keeps UI replaceable (console today, Blazor tomorrow).
3. **Content as data.** `Target`, `Upgrade`, and later `Faction`/`Augmentation` are records defined in JSON, loaded into registries. Adding content = editing JSON.
4. **Exponential cost curve.** `cost = baseCost * growthRate^owned` (growthRate ≈ 1.07–1.15 per level, like classic idlers). Production scales linearly per level → exponential cost creates the classic "wall then breakthrough" pacing.
5. **Save migrations.** Include a `schemaVersion` int in the save; write a migration step per version bump.
6. **Scripts save as source + explicit state.** Roslyn continuations cannot be serialized. Persist each script's source text + its `ns.Data` key-value store; on load, recompile and restart — scripts reconverge because they're loops. Durable facts go in `ns.Data`, ephemeral progress lives in locals and is accepted lost. Make spendy commands idempotent ("if already owned, no-op") so restarts can't double-buy.

### Suggested project layout

```
Nullbyte.sln
├─ src/Nullbyte.Core/           # engine, no I/O except SaveSystem
│  ├─ GameSession.cs            # command entry point
│  ├─ GameLoop.cs               # fixed tick loop
│  ├─ GameState.cs              # pure state record
│  ├─ Systems/
│  │  ├─ EconomySystem.cs
│  │  ├─ HackSystem.cs
│  │  └─ ProgressionSystem.cs
│  ├─ Content/
│  │  ├─ ContentLoader.cs
│  │  ├─ Target.cs / Upgrade.cs
│  │  └─ data/*.json
│  ├─ Commands/                 # ICommand implementations
│  └─ Save/SaveSystem.cs
├─ src/Nullbyte.Console/        # MVP frontend
├─ src/Nullbyte.Scripting/      # (Phase 4) Roslyn runtime: ns API, tick barrier, quota, lockdown
├─ src/Nullbyte.Editor/         # (Phase 4) AvalonEdit + Roslyn classification frontend
└─ tests/Nullbyte.Core.Tests/
```

## 5. Roadmap

### Phase 1 — Playable core (1–2 weeks of spare time)
- Fixed-tick engine, manual hack with timer, money + XP
- 5 targets, 3 upgrade tracks with exponential costs
- Passive botnet income
- JSON save/load + offline catch-up
- Console UI: `hack <target>`, `buy <upgrade>`, `status`, `save`
- *Exit test:* a full playthrough loop feels satisfying for 30 min.

### Phase 2 — Progression & content depth
- 15–20 targets with "security levels" (some require meeting rig thresholds)
- Skill tree: spend XP on perks (hack speed, batch hacking, offline %)
- Event log / terminal flavor text (hacker theme sells the fantasy)
- Achievements and stats page
- Missions/contracts (timed objectives with rewards) — first "content drip"
- *Exit test:* returning after a day feels rewarding.

### Phase 3 — prestige
- Prestige/reset layer ("burn your identity"): reset for permanent multipliers
- Second currency, exponential-tier prestige upgrades
- More content data + a content validator tool so balance mistakes surface early
- *Exit test:* a first reset is an exciting choice, not a chore.

### Phase 4 — The programming hook (Bitburner moment)
- **Roslyn C# scripting as the player language** (full C#, async/await, LINQ) — validated by spike: `ns` API object, tick barrier (`TaskCompletionSource` released once per tick), per-script CPU quota, compile-time lockdown (whitelisted namespaces/usings, banned patterns fail compilation)
- Execution model: scripts are `async Task` coroutines; every `await` on an `ns` promise is tick-aligned — scripts cannot proceed between ticks
- CPU economy: time-based quota per script per tick, scaled by rig CPU/RAM → rig upgrades make scripts "faster" (API-call metering for cost balance)
- Save model: persist script source + `ns.Data` key-value state; recompile and restart on load; durable facts in `ns.Data`, locals accepted lost; spendy commands made idempotent
- In-game editor: AvalonEdit + Roslyn classification → real C# highlighting, IntelliSense, compile-error squiggles
- Script marketplace (share/import scripts as JSON strings)
- Runtime errors halt one script, never the game; cache compiled scripts per save (Roslyn warm-up ~1–2 s once)
- *Exit test:* a player script can play the early game unattended; save/load round-trips and the script reconverges.

### Phase 4.5 — Hack overhaul: mark heat & drain (Bitburner security)

Bitburner's core insight: **a server is a resource, not a vending machine.** NULLBYTE
targets currently pay a flat reward forever; grinding one mark is optimal and boring.
This phase gives every target live state — a drainable account and a heat gauge —
so the 18-mark roster becomes a farm you rotate and manage.

**Per-target live state** (persisted in `GameState`, wiped on prestige burn):

```csharp
TargetState { double CurrentMoney; double Heat; }   // keyed by target id
```

**Hack (steal) — revised:**
- Completion pays `StealBase (12%) × CurrentMoney` (× ransom perk, notoriety implant),
  then subtracts from the target — so rewards decline as the mark drains.
- `MaxMoney` per target is tuned so the *first* hack pays ≈ the old flat `MoneyReward`
  (early game feels identical, the decline curve does the new work).
- Heat: **+6** per completed hack, **+10** per *failed* hack.
- **Failure risk at high heat** (user decision): `failChance = clamp((Heat − 40) / 120, 0, 0.5)`
  — zero below 40 heat, ~25% at 70, 50% at 100. Failed hack = no reward + extra heat.
  Checked at completion, announced with trace-flavor text. This roll is the  
**documented engine-randomness exception #2** (contract offers being #1).
- Duration multiplier `1 + Heat/50` — hot marks are also slower to crack.
- At < 20% of `MaxMoney`: "tapped out" flavor + scraps (steal % of a small pool).

**Grow (new action)** — active counter-play, like Bitburner's `grow`:
- `grow <target>`: occupies a hack slot for a grow job (~1.5× hack duration);
  on completion `CurrentMoney += 25% of MaxMoney` (capped at max), +2 heat.
- Small XP (≈25% of the target's XpReward). No money reward.
- **No passive money regen** (user decision) — the pool only recovers via grow,
  which is exactly the scriptable resource-management gameplay we want.

**Scrub (new action)** — heat counter-play (active op, hence the verb):
- `scrub <target>`: occupies a hack slot; on completion `Heat −= 35` (floor 0).
  Flavor: scrub your traces off the mark's logs.
- Passive heat decay still exists (−1/sec, only while the target has no active job).
- No XP. Pure heat tool.

**Emergent loop:** drain a mark ~6 hacks (pool ~45%, heat ~40, fail risk begins) →
rotate to a cold mark → later return with a `grow`+`scrub` rotation → repeat.
Best done by a script: the ideal `grinder.cs` becomes a heat/money-aware rotation
engine — which is the actual Bitburner hook landing.

**Ripple effects (handled in the same pass):**
- Commands: `GrowTarget`, `ScrubTarget` records via GameSession; console `grow`/`scrub`.
- Scripting API: `await ns.Grow(id)`, `await ns.Scrub(id)` actions +
  `ns.TargetMoney(id)`, `ns.TargetHeat(id)`, `ns.TargetMax(id)` sync queries.
- `targets` command shows live money/heat columns; `status` shows active job types.
- Prestige burn resets all `TargetState` (new identity → cold marks).
- Offline: heat decay lives in `Tick()`, so `FastForward` makes away-time
  recovery time — marks are cool and you *haven't* grown them; grow is a
  deliberate player/script action, and that's the intended asymmetry.
- Save: `Targets` dict in `SaveData` (tolerant of missing = fresh state);
  `MaxMoney` added to content JSON per target (default 8× `MoneyReward` if absent).
- Contracts counting "complete N hacks" become subtly rotation-aware.
- Tests: drain math, fail-risk gate (inject fixed heat), grow cap, scrub floor,
  heat decay gating, save round-trip with target state, prestige wipe.
- *Exit test:* camping one target is clearly worse than rotating; a rotation
  script measurably out-performs a naive `while(true) hack webmail` loop.

### Phase 5 - Factions 
- Factions with reputation grind → unique upgrades (Bitburner staple)
- *Exit test:* Exploring factions is rewarding game play wise

### Phase 6 — World & late game
- Persistent "network map" world (nodes to conquer, turf mechanics)
- WPF UI with a real terminal window and live graphs
- Move constants to the content to enable easier balancing
- Balancing pass, difficulty curve tuning from telemetry-style save data

## 6. First Week Checklist

- [x] `dotnet new` solution with Core + Console projects
- [x] GameState record + fixed-tick GameLoop running in a test
- [x] Manual hack command with income math + exponential upgrade costs
- [x] JSON content file for targets/upgrades + loader
- [x] Save/load with `schemaVersion` and offline catch-up
- [x] Minimal console commands: `hack`, `buy`, `status`
- [x] Unit tests: cost curve, offline catch-up, save round-trip

## 7. Pitfalls to avoid

- **No fixed tick / state mutated from UI threads** → nondeterministic offline progress and unfixable bugs later.
- **Hardcoded content** → every balance tweak is a recompile.
- `float`/`decimal` money → precision issues; use `double` then BigNum.
- **Building the scripting layer too early** → nail the idle core first; scripting needs solid, tick-based systems underneath it.
- **Trusting Roslyn as a sandbox** → .NET has no true sandbox; enforce at compile time (reference/using whitelist, banned-pattern analyzer) and treat scripts as player-self-inflicted risk, like Bitburner does. Real isolation would require an out-of-process host.
- **Expecting to serialize running scripts** → Roslyn continuations can't be saved; design `api` APIs and player conventions around source-restart + `api.Data` from day one.