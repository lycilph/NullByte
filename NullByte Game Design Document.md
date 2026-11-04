# NullByte: Game Design Document

Version 0.1, first iteration (console), with a later WPF + AvalonEdit front end.

## 1. Vision

NullByte is a text-driven incremental game where the player grows a passive income stream (Hacknet) and then automates the rest of the economy by writing C# scripts against a small game API (the `ns` object), compiled at runtime with Roslyn. Manual commands and scripts use the same underlying actions, so everything a script can do the player can also do by hand, only slower.

The central fantasy is the same as Bitburner: you start by typing commands, get bored, write a script, then write a better one.

### Pillars

1. **Scripting is the game.** Progress comes from writing better programs, not clicking faster.
2. **Same rules for hand and script.** Hack, grow and weaken are one implementation, called from the console or from `ns`.
3. **Deterministic, single-threaded simulation.** All game state is touched from exactly one thread, driven by a custom synchronization context. No locks in game code.
4. **Start small, grow later.** The first iteration is deliberately narrow, but the architecture must not block factions, augmentations, a UI, saving and so on.

## 2. Scope of the first iteration

### In scope

- A player with money and a hacking level (with XP).
- Hacknet: buy nodes, upgrade level, RAM and cores, generate passive income.
- A small hand-authored network of servers (home plus roughly 10 to 15), each with hacking requirement, security, money and growth.
- Server actions: `scan`, `connect`, `nuke` (gain root), `hack`, `grow`, `weaken`, with variable duration and thread count.
- Scripts: write, compile, run, kill and list scripts, with RAM cost, threads and arguments.
- Home RAM as the limiting resource for scripts.
- A console that blocks input while a manual hack/grow/weaken runs.
- Save and load (late milestone).

### Out of scope (for now)

Factions, augmentations, programs and port opening (we may simplify to "nuke needs hacking level only"), purchased servers (stretch goal), stock market, corporations, Bladeburner, Sleeves, Gangs, Stanek, Singularity API, offline progress, multiplayer, and a GUI.

## 3. Core mechanics

The exact numbers below follow Bitburner as far as I remember them and are **starting values to verify against the Bitburner source and wiki** in milestone M2. They live in one `Formulas` class and one `Balance` config so they are easy to correct and tune.

### 3.1 Player

| Field | Notes |
| --- | --- |
| Money | `decimal` or `double`; decide in M0 (see open questions) |
| Hacking skill | Derived from hacking XP |
| Hacking XP | Gained from successful hack, grow and weaken |
| Home | A server with RAM that bounds script capacity |

### 3.2 Server

| Field | Meaning |
| --- | --- |
| Name / hostname | Unique id |
| Neighbors | Connections for `scan` and `connect` |
| RequiredHackingSkill | Minimum skill to hack |
| MaxMoney, MoneyAvailable | Hack takes money, grow adds it |
| MinSecurity, Security | Hack and grow raise it, weaken lowers it |
| GrowthParam | Server growth rate factor |
| MaxRam, UsedRam | Capacity for running scripts |
| HasRoot | Set by `nuke` |

### 3.3 Actions

Each action has a duration, a success rule and an effect, and applies per thread.

- **Hack**: success chance depends on skill, requirement and security. On success it steals a percentage of current money. Raises security by about 0.002 per thread.
- **Grow**: multiplies money by a growth factor based on threads, server growth and security. Raises security by about 0.004 per thread.
- **Weaken**: lowers security by about 0.05 per thread.
- **Durations**: hack time is derived from requirement, security and skill; grow is about 3.2 times and weaken about 4 times the hack time.
- **XP**: all three give hacking XP, scaled by server difficulty.

Key property for gameplay: durations are long enough that waiting is felt, which is what motivates scripting.

### 3.4 Hacknet

A node has level, RAM and cores. Production per second is roughly `level * 1.5 * 1.035^(ram - 1) * (cores + 5) / 6`, times a global multiplier. Costs grow geometrically per purchase, with separate base costs and growth factors for new node, level, RAM and core. Caps: level 200, RAM 64, cores 16. All values are initial tunables.

Hacknet accrues in game time, on the same simulation clock as everything else.

### 3.5 Scripts and RAM

- A script is a C# source file stored in the game's virtual file system (kept in memory and saved to JSON; no real disk access for scripts).
- Each script has a static RAM cost: a base cost plus the sum of costs for each distinct `ns` method it calls, found by analyzing the code with Roslyn's semantic model.
- Running a script with `t` threads costs `ram * t`. It must fit in the target server's free RAM.
- Scripts run as processes (PID, name, args, threads, host) and can be listed and killed.

### 3.5.1 Script shape

Start with the simplest thing that is pleasant to write:

```csharp
public class Script : IScript
{
    public async Task Main(NS ns)
    {
        var target = ns.Args[0];
        while (true)
        {
            if (ns.GetServerSecurityLevel(target) > ns.GetServerMinSecurityLevel(target) + 5)
                await ns.Weaken(target);
            else if (ns.GetServerMoneyAvailable(target) < ns.GetServerMaxMoney(target) * 0.75)
                await ns.Grow(target);
            else
                await ns.Hack(target);
        }
    }
}
```

Later we can switch to top-level scripts (Roslyn script mode) as a friendlier surface, since the compile pipeline is isolated behind an interface.

## 4. Technical architecture

### 4.1 Solution layout

- `NullByte.Core`: simulation, formulas, model, clock, scheduler, NS API. No console or UI references.
- `NullByte.Scripting`: Roslyn compile, analysis (RAM, banned APIs), loading and process management.
- `NullByte.Console`: the first host, with command parser, rendering and input.
- `NullByte.Wpf` (later): WPF host with AvalonEdit, reusing Core and Scripting unchanged.
- `NullByte.Tests`: unit tests, with a fake clock and seeded RNG.

Target framework: .NET 10 with the matching C# language version, nullable reference types enabled.

Hosts talk to the game through an interface (`IGameHost` or similar) for output, input prompts and notifications, so console and WPF are interchangeable.

### 4.2 Single-threaded simulation

All game state is read and written only on the game thread.

- A custom `GameSynchronizationContext : SynchronizationContext` holds a queue of posted callbacks. `Post` enqueues; the game loop drains the queue on its thread.
- The game loop runs on a dedicated thread (or the main thread in the console host), repeatedly: advance game time, fire due timers, drain the posted queue, render if needed, sleep briefly.
- `SynchronizationContext.SetSynchronizationContext` is set on that thread, so `await` in scripts and in console commands resumes on the game thread.
- Other threads (console `ReadLine`, Roslyn compilation if we want it off-thread) never touch game state; they `Post` results back.

### 4.3 Game time and timers

- `IGameClock` provides game time. Production uses a fixed time step driven by real elapsed time; tests use a manually advanced clock.
- A scheduler (priority queue) holds timers keyed by game time. `ns.Hack`, `ns.Grow`, `ns.Weaken` and `ns.Sleep` create a `TaskCompletionSource`, schedule a timer, and complete it when due. Continuations then run through the synchronization context, on the game thread.
- This keeps long operations cheap and makes time controllable (speed-up, pause, fast tests).

### 4.4 Console blocking behavior

- Manual `hack`, `grow` and `weaken` start the same action as a script would, with a progress indicator.
- While it runs, the console does not accept the next command, and anything typed in the meantime is discarded. Implementation: the command dispatcher awaits the action's task before printing the next prompt and before consuming the next queued input line.
- **The simulation does not pause.** Background scripts and Hacknet keep running while the player is blocked. Output from other sources is printed above or after the progress line and must not corrupt the prompt.
- Optional: Ctrl+C cancels the manual action through a `CancellationToken` (decide in M3).

### 4.5 Roslyn pipeline

1. Parse source into a `SyntaxTree`.
2. Create a `CSharpCompilation` with a curated list of metadata references (core runtime assemblies we allow, plus `NullByte.Core` API types).
3. Run analyzers: banned API usage (file, network, process, reflection, threading, `Environment`, `Assembly` loading), required entry point, and the RAM cost calculation using the semantic model.
4. Emit to an in-memory assembly and load it into a collectible `AssemblyLoadContext`, so recompiling or killing a script lets it be unloaded.
5. Instantiate the script class and run `Main` as a process on the game thread.
6. Report diagnostics (line, column, message) to the host as game output.

Compile results are cached by source hash.

### 4.6 NS API (first set)

Each method has a RAM cost declared with an attribute (for example `[RamCost(0.1)]`) so analysis can find it.

| Group | Methods |
| --- | --- |
| Actions | `Hack`, `Grow`, `Weaken`, `Sleep` |
| Info | `GetServerMoneyAvailable`, `GetServerMaxMoney`, `GetServerSecurityLevel`, `GetServerMinSecurityLevel`, `GetServerRequiredHackingLevel`, `GetHackingLevel`, `GetServerMaxRam`, `GetServerUsedRam`, `HasRootAccess` |
| Network | `Scan`, `Nuke` |
| Scripts | `Run`, `Exec`, `Kill`, `Ps`, `ScriptRunning`, `GetScriptRam` |
| Hacknet | `Hacknet.NumNodes`, `PurchaseNode`, `UpgradeLevel`, `UpgradeRam`, `UpgradeCore`, cost getters |
| Output | `Print`, `Tprint` |
| Args | `Args` |

### 4.7 Known risks and mitigations

| Risk | Mitigation |
| --- | --- |
| A script with a tight synchronous loop (no `await`) freezes the single game thread | Analyzer error for loops without an `await` in the body; ban `Thread.Sleep`, `Task.Wait`, `.Result`; consider an instruction-count or time-budget guard via injected checks (investigate in M7) |
| Roslyn startup and compile latency | Warm up the compiler at launch; cache results; show "compiling" status |
| Sandbox escape through reflection or dynamic loading | Allow-list references and ban APIs at the semantic level, not just by string matching. This is hobby-level sandboxing, not a security boundary, and the doc should say so |
| Floating-point drift and balance bugs | Central `Formulas` class with unit tests; seeded RNG |
| Console output interleaving with input | Single output writer on the game thread; the prompt is redrawn after other output |

## 5. Milestones

Each milestone ends with something runnable and tested. Acceptance criteria are what "done" means.

### M0: Foundations

**Deliverables**

- Solution and project layout, target framework, nullable enabled, analyzers and formatting rules.
- Decision on number types for money (see open questions), logging approach, and test framework.
- Balance config loading (a JSON file or an embedded record).

**Acceptance**: solution builds, one trivial test passes, a console app prints a banner.

### M1: Game loop and synchronization context

**Deliverables**

- `GameSynchronizationContext`, game loop, `IGameClock` with real and fake implementations.
- Timer scheduler with `Delay(gameTime)` returning an awaitable `Task`.
- Ability to run an async method on the game thread and see continuations resume there.

**Acceptance**

- Tests: continuations after `await Delay(...)` run on the game thread; ordering of timers is deterministic with the fake clock.
- A demo that prints a tick counter and two concurrent delayed tasks.
- Posting from another thread is safe and processed on the game thread.

### M2: Model, network and formulas

**Deliverables**

- `Player`, `Server`, `Network` (hand-authored data file with home and about 10 to 15 servers).
- `Formulas`: hack chance, hack percent, durations, grow factor, security deltas, XP gain, hacking level from XP.
- `ServerActions` service: `Hack`, `Grow`, `Weaken` as async operations using the clock, with thread count.
- Verification of formulas against Bitburner reference values.

**Acceptance**

- Unit tests for each formula with known values.
- Running 1000 simulated hacks on a fake clock yields money and security changes in the expected range.
- Invariants hold: money never above max or below zero, security never below minimum.

### M3: Console shell and manual actions

**Deliverables**

- Command parser and registry: `help`, `scan`, `connect`, `home`, `ls`, `nuke`, `analyze`, `hack`, `grow`, `weaken`, `money`, `status`.
- Prompt showing the current server.
- Manual hack/grow/weaken with progress display; input blocked until completion; simulation keeps running.
- Input thread posting lines to the game thread.

**Acceptance**

- A player can scan, connect, nuke a low-level server, and hack it for money from the console.
- Input typed during a blocking action is discarded and never executed.
- Tests drive the console with scripted input and a fake clock.

### M4: Hacknet

**Deliverables**

- Hacknet model, cost formulas, production per tick, node purchase and upgrades.
- Console commands: `hacknet`, `hacknet buy`, `hacknet upgrade <n> level|ram|core`, `hacknet status`.

**Acceptance**

- Production matches the formula in tests over simulated time.
- A new game can reach several nodes using only hand-hack money plus Hacknet income, so the early loop works before scripting exists.

### M5: Roslyn scripting, compile and run

**Deliverables**

- Virtual file system with `ls`, `cat`, `rm`, `edit` (console editor stub: paste or external editor reload).
- Compile pipeline from 4.5 minus RAM analysis and sandbox hardening.
- `run <script> [args]` executes `Main` on the game thread; diagnostics are printed.
- A first `ns` surface: `Print`, `Sleep`, `Args`, and the three actions.

**Acceptance**

- The example script from 3.5.1 runs against a server and increases the player's money over time.
- Compile errors show line and column.
- Recompiling a changed script unloads the old assembly (verify with a weak reference test).

### M6: Process manager, threads and RAM

**Deliverables**

- Processes with PID, host, threads and cancellation.
- Static RAM analysis from `[RamCost]` attributes; RAM enforcement on `run` and `exec`.
- `ps`, `kill`, `killall`, `free`, `mem <script>`, `scp` and `exec` for running on other rooted servers.
- Full first NS set from 4.6.

**Acceptance**

- Running with too little free RAM fails with a clear message.
- `kill` stops a script at its next `await` and frees its RAM.
- Multi-thread runs scale effect and RAM cost correctly.

### M7: Sandbox and safety

**Deliverables**

- Allow-listed references and banned API analysis.
- Guard against synchronous infinite loops (analyzer rule plus an investigated runtime guard).
- Exception handling: script crashes are caught, logged and do not take down the game loop.

**Acceptance**

- A set of malicious or buggy sample scripts (file access, reflection, `while(true){}`, thrown exceptions) are rejected or contained, and the game continues to run.

### M8: Progression and balance pass

**Deliverables**

- Tuned network, requirements and payouts so that a first playthrough has a clear arc: manual hack, Hacknet, first script, script on several servers, home RAM upgrades.
- Home RAM upgrade purchase (and optionally purchased servers).
- Hacknet and hack economy balanced so neither dominates.
- Short in-game tutorial via `help` and a few sample scripts shipped in the starting file system.

**Acceptance**: a play session of roughly 1 to 2 hours reaches the final in-scope tier of servers, with a script-based economy clearly better than manual play.

### M9: Save and load

**Deliverables**

- Serialization of player, servers, Hacknet, files, running processes (restarted on load) and game time.
- Versioned save format with a migration hook.
- Autosave and manual `save` and `load`.

**Acceptance**: save, quit, load restores an equivalent state; a golden-file test guards the format.

### M10: First playable release

**Deliverables**: bug fixing, README, sample scripts, a short "how to play" document, a tagged build.

**Acceptance**: someone other than the author can install, play through the first arc and write a working script using only the in-game help and README.

## 6. After the first playable

- **WPF host**: terminal control, script editor with AvalonEdit, Roslyn-powered completion and diagnostics (squiggles), file browser, status panels. Core and Scripting stay untouched; only a new `IGameHost` implementation is needed.
- Factions, augmentations, reset loop.
- Port-opening programs and more servers.
- Purchased servers, stock market, offline progress.
- Script API versioning.

## 7. Testing strategy

- Unit tests for formulas and invariants with fixed values.
- Deterministic simulation tests: fake clock, seeded RNG, scripted scenarios.
- Compile pipeline tests with good and bad scripts.
- Console tests with scripted input and captured output.
- Thread-affinity assertions: a debug check in game-state setters that the current thread is the game thread.

## 8. Open questions (resolved)

1. **Money type**: `double`, with formulas isolated so it can change. Decided.
2. **Script surface**: a class with `Main` first, Roslyn script mode later. Decided.
3. **Typing during a blocking command**: the input is discarded, not buffered. Decided.
4. **Real-time versus game-time ratio**: 1 to 1 initially, with a speed multiplier in the config. Decided.
5. **Target framework**: .NET 10. Decided.
6. **Network authoring format**: a JSON data file. Decided.

## 9. Suggested next step

M0 and M1 come first: set up the solution, then build the synchronization context, fake clock and timer scheduler with tests, since everything else depends on them. The decisions in section 8 are confirmed.
