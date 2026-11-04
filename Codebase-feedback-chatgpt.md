# NullByte – Architecture and codebase review

I have started with `GameDesign.md`, as requested, and will use it as the reference point for evaluating the implementation.

My initial assessment is that NullByte has a sound architectural foundation, and its most interesting feature is also its biggest design opportunity: player-programmable automation.

I would not approach this primarily as a conventional incremental game with hacking-themed mechanics. The design describes something closer to a combination of:

- An incremental game, with exponential progression and prestige.
- A programming sandbox, where writing automation is part of the gameplay.
- A simulation engine, where targets have resources, heat and operational constraints.
- A persistent world, where automation and offline progress continue to matter.

That distinction is important. It affects how I would evaluate the architecture, scripting API, progression systems and future UI.

I also want to distinguish between two types of feedback:

1. Engineering feedback: correctness, maintainability, architecture, performance and extensibility.
2. Game-design feedback: whether the implementation supports the intended player experience and whether the mechanics create interesting decisions.

A technically elegant implementation is not necessarily a good incremental game. Both aspects need to work together.

## 1. Game design: what the implementation should optimize for

The design document has four pillars:

| Pillar                            | What it means for the implementation                                                |
| --------------------------------- | ----------------------------------------------------------------------------------- |
| Numbers go up, but choices matter | Multiple viable strategies, meaningful upgrade trade-offs and resource management   |
| Automation is the content         | The scripting system must become a central gameplay feature, not just a convenience |
| Offline progress                  | The simulation must support reliable progression without continuous execution       |
| Moddable content                  | Content and balance should be extensible without changes to engine code             |

There is also an important evolution in the design: Phase 4.5 introduces drainable target accounts and heat. This is a substantial improvement over the original flat-reward model because it creates a reason to rotate between targets and eventually automate that rotation.

I think this is the right direction.

### My main design observation

The strongest potential gameplay loop is:

1. Discover targets and understand their characteristics.
2. Choose which targets to exploit.
3. Manage their money pools and heat.
4. Upgrade the rig to increase throughput.
5. Write scripts to optimize target selection and job scheduling.
6. Unlock more difficult targets and mechanics.
7. Eventually prestige and repeat with a stronger starting position.

The important part is step 5.

If the optimal way to play eventually becomes writing a better algorithm, you have a much more distinctive game than an incremental game where players simply buy increasingly expensive multipliers.

I would use this as the main criterion for future feature decisions.

### One potential design conflict

Your original design describes three stages:

- Early: manual hacking.
- Mid: purchased automation.
- Late: player-programmed automation.

The current implementation has passive botnet income and player scripts, but I don't see a substantial intermediate automation layer.

Botnet income is an additional source of money, but it doesn't automate the actual hacking process.

I would consider introducing some form of purchased automation before making scripting the dominant mechanic. For example:

- Basic auto-hack: repeatedly attacks one selected target.
- Target rotation: automatically switches when a target is depleted.
- Advanced automation: configurable conditions and priorities.
- Player scripts: unrestricted strategic control.

These should not simply be increasingly powerful versions of the same feature. They should provide a progression from using automation to understanding automation to programming it.

There is a design decision worth making here: Should players be able to write C# scripts from the very beginning, or should scripting itself be unlocked through progression?

Both approaches are viable, but they create different games.

## 2. Architecture: overall assessment

Sound foundation

Your separation between `NullByte.Core`, `NullByte.Scripting` and the UI projects is a good architectural decision.

The most important architectural choice is that the game engine is independent of the presentation layer.

Current architectural model

Console UI / Future WPF UI

Player commands and presentation

GameSession

Command queue, simulation ticks and state access

Game systems

Economy, hacking, progression, prestige, contracts

Scripting system

Roslyn, player scripts, API and execution

GameState + GameContent

Persistent simulation state and data-driven definitions

SaveSystem

Persistence and offline catch-up

This is a sensible structure for a game that will eventually have a much richer UI.

I particularly like the command pattern. The same `IGameCommand` implementations are used by both the console and the scripting API. This means that scripts cannot bypass normal gameplay rules simply by interacting with the engine differently.

That is exactly the kind of consistency you want.

However, I see several improvements worth making before expanding the feature set.

### 2.1 GameSession: fix the synchronization boundary

Priority: High

In `GameSession.cs`, you have:

```
private static readonly Lock gate = new();
```

This is a static lock shared by every `GameSession` instance.

I would change this to:

```
private readonly Lock gate = new();
```

A static lock means that two independent game sessions would unnecessarily block each other.

More importantly, `GameSession.State` is publicly exposed:

```
public GameState State { get; }
```

Although the scripting API uses `session.Read()` for synchronized access, other consumers can access the mutable collections directly.

For example:

```
session.State.UpgradeLevels
session.State.ActiveJobs
session.State.Targets
```

The internal setters provide some protection, but the dictionaries and lists themselves remain mutable.

This weakens one of your most important architectural guarantees:

> The game state should only be modified inside the simulation tick.

I would recommend:

- Make `State` internal or expose a read-only snapshot.
- Keep `Read()` as the controlled access mechanism.
- Introduce immutable snapshot objects for UI consumption.
- Ensure all mutations go through commands and systems.

This will become increasingly important when you introduce a WPF UI with live updates.

### 2.2 Separate simulation time from wall-clock time

Priority: Medium

Your fixed-tick implementation is a good starting point:

```
session.Tick();
scripting.Tick();
await Task.Delay(GameSession.TickMs, cts.Token);
```

However, `Task.Delay(100)` does not guarantee a 100 ms simulation interval.

The loop will accumulate execution overhead and scheduling delays.

For an incremental game, this isn't necessarily a problem, provided the game design consistently treats ticks as the authoritative unit of simulation time.

I would eventually introduce a dedicated game-loop abstraction that handles:

- Fixed simulation ticks.
- Real-time pacing.
- Catch-up when the application is temporarily delayed.
- Controlled shutdown.
- Optional pause and speed controls.

Keep the simulation itself independent of real time.

That will also make testing and offline simulation easier.

### 2.3 Consider making simulation randomness injectable

Priority: Medium

You have several static random generators:

- `HackSystem.Rng`
- `ContractsSystem.rng`
- `AtmosphereSystem.rng`
- `Flavor.rng`

The design explicitly identifies the first two as gameplay randomness exceptions.

I would introduce an injectable random source for the gameplay systems.

Why?

Because it gives you:

- Repeatable test scenarios.
- Reproducible bug reports.
- Deterministic simulation replays.
- Better balance testing.
- More reliable offline simulation.

For example, a test could reproduce exactly the same sequence of failed hacks every time.

You don't necessarily need to implement a full deterministic event-replay system now. Just avoid making randomness inaccessible to tests.

## 3. Hacking mechanics: a good evolution, with some balance questions

Strong design direction

The transition from flat rewards to drainable targets is one of the better design decisions in the project.

The implementation is reasonably clean:

- `TargetState` holds the live money pool and heat.
- `HackSystem` owns the hacking mechanics.
- `ActiveJob` represents work in progress.
- `Hack`, `Grow` and `Scrub` share the same job infrastructure.

This is a good example of a feature being implemented at the correct architectural level.

### 3.1 The target economy needs a closer balance pass

Your current values are:

| Mechanic               | Value               |
| ---------------------- | ------------------- |
| Hack steal             | 12% of current pool |
| Heat per hack          | +6                  |
| Heat per failed hack   | +10                 |
| Failure threshold      | 40                  |
| Maximum failure chance | 50%                 |
| Grow refill            | 25% of maximum pool |
| Grow heat              | +2                  |
| Scrub reduction        | -35                 |
| Passive heat decay     | -0.1/tick           |

The mechanics create an interesting relationship between risk, reward and time.

However, there is something I would investigate.

The hack duration increases with heat:

```
1 + Heat / 50
```

At heat 40, this makes the job 1.8 times slower.

At heat 70, it is 2.4 times slower.

At heat 100, it is 3 times slower.

Combined with the failure probability, the cost of continuing to hack a hot target becomes substantial.

That might be desirable, but it could also make the correct strategy too obvious: leave a target as soon as it gets warm.

I would measure the actual income per minute for different strategies rather than balancing each individual number in isolation.

### 3.2 Parallel jobs deserve particular attention

One thing I like is that you calculate the stolen amount when a job completes rather than when it starts.

This prevents concurrent jobs from reserving the same money.

However, multiple jobs on one target also interact with heat and failure probability at completion.

The completion order currently follows the reverse order of the active-job list.

This is deterministic, but it is an implicit gameplay rule.

I would consider explicitly defining how simultaneous completions are resolved.

For example:

- All jobs completing on a tick evaluate against the same initial heat.
- Jobs resolve sequentially, with each affecting the next.
- Jobs are resolved in submission order.

Any of these can work. The important thing is that the behavior is deliberate and tested.

### 3.3 Grow and scrub should be strategically interesting

There is an interesting opportunity here.

Currently, grow and scrub are separate actions with fixed effects.

I would consider whether their effectiveness should also be influenced by:

- Target characteristics.
- CPU or other hardware.
- Specific perks.
- Prestige upgrades.
- Later research or faction bonuses.

Not every mechanic needs to be upgradeable, but this would create additional strategic possibilities.

One thing I would avoid is adding too many multipliers directly to the existing `HackSystem`. As the mechanics expand, you will benefit from having the calculations and modifier rules centralized.

## 4. Scripting system: the most important part of the project



Strong concept

Needs hardening



This is where I think the project has the most potential, but also where I see some of the most important engineering risks.

Your implementation has several good decisions:

- Roslyn provides actual C# rather than a custom scripting language.
- Scripts interact through `NullByteApi`.
- Actions submit the same commands as the console.
- Actions await tick boundaries.
- Scripts have their own output and error state.
- Scripts persist their source and `ScriptData`.
- Libraries can be included using `// uses:`.

I particularly like the library mechanism. It is a relatively lightweight way to give players reusable building blocks without introducing a separate module system.

However, I would address the following issues.

### 4.1 Your CPU quota isn't actually a CPU quota

Priority: Critical

Currently, you have:

```
public const int MaxApiCallsPerTick = 50;
```

This limits API calls, not CPU consumption.

Consider this script:

```
while (true)
{
    var x = Math.Sqrt(12345);
}
```

It never calls the game API and never reaches a tick barrier.

Consequently:

- It never consumes API quota.
- It never yields to the game loop.
- `Stop()` cannot interrupt it.
- Its execution task never completes.

Your script runs through `Task.Run`, so this doesn't necessarily freeze the simulation immediately, but it can consume a thread-pool worker indefinitely.

This is particularly important because your design explicitly identifies CPU as a resource that should influence scripting performance.

My recommendation:

For the current single-player implementation, I would avoid trying to build a perfect sandbox.

Instead, establish two distinct concepts:

1. API quota: limits how frequently scripts can interact with the game.
2. Execution budget: limits how much computation a script can perform between cooperative yield points.

The second is harder to implement reliably with unrestricted C#.

A practical solution would be to introduce an execution model where scripts must cooperate with the scheduler, with explicit yield points and cancellation checks.

If you eventually want genuinely untrusted scripts, you will need process isolation rather than relying on syntax checks.

This is consistent with the security caveat already documented in your GameDesign.

### 4.2 ScriptingSystem has concurrency risks

Priority: High

Your `ScriptingSystem` maintains:

```
private readonly List<ScriptInstance> scripts = [];
```

The list is accessed from several places:

- `StartAsync()`
- `Stop()`
- `Find()`
- `Tick()`
- The console UI when displaying scripts.

In particular, `StartAsync()` performs compilation asynchronously and adds the script to the collection afterward.

Meanwhile, the game loop can enumerate or modify that same collection.

`List<T>` is not thread-safe.

This can produce intermittent collection-modified exceptions or inconsistent script listings.

I would introduce a synchronization strategy for the script registry.

For example:

- Protect collection modifications with a dedicated lock.
- Return snapshots when exposing the script list.
- Never hold the registry lock while compiling or executing scripts.
- Consider using a `ConcurrentDictionary<string, ScriptInstance>` keyed by script name.

I would also make script names unique at the `ScriptingSystem` level rather than relying on the console UI to prevent duplicates.

### 4.3 The tick barrier is a good abstraction, but needs stronger lifecycle handling

Your `TaskCompletionSource` barrier is a reasonable solution for synchronizing scripts with simulation ticks.

I like the fact that you use:

```
TaskCreationOptions.RunContinuationsAsynchronously
```

This avoids executing arbitrary script continuations directly inside the game loop's tick method.

There are, however, some edge cases worth testing:

- A script stopped while waiting at a barrier.
- A script stopped while executing a long-running operation.
- Several API calls started without awaiting them.
- Several scripts submitting commands simultaneously.
- A script failing while other scripts continue.
- Starting and stopping scripts while the game is saving.

I also noticed that `ApiCallsThisTick` is incremented without synchronization. Since your tests explicitly allow scripts to fire multiple unawaited API calls, concurrent increments can become problematic.

I would make the API quota accounting thread-safe or enforce sequential execution of API operations per script.

### 4.4 Script persistence needs an explicit execution contract

Your source-and-data persistence model is sensible.

You correctly recognize that Roslyn execution state cannot simply be serialized.

However, restarting a script from the beginning introduces an important semantic question:

What exactly does it mean for a script to be running when a game is saved?

Suppose a script does this:

```
await nb.Hack("webmail");

nb.Data.SetNum("completed", 1);
```

If the game is shut down between those operations, the script may restart and execute the hack again.

For non-idempotent actions, that can matter.

I would document the persistence model as an explicit contract:

- Scripts restart from their entry point.
- Local variables are lost.
- `nb.Data` is the durable state.
- Pending operations are not guaranteed to be exactly-once.
- Scripts should be designed to recover safely.

Longer term, I would consider adding a command/job identifier or an operation ledger for scripts that need reliable recovery.

You don't need distributed-systems-level transaction handling for this game, but the semantics should be predictable.

### 4.5 The scripting API could become much more expressive

At present, the API exposes useful information such as:

- Money, XP and level.
- CPU and RAM levels.
- Target scanning.
- Target money and heat.
- Hack, grow and scrub actions.
- Delays and persistent data.

I would gradually expand it with read-only information about the game world.

For example:

| API capability                | Why it matters                        |
| ----------------------------- | ------------------------------------- |
| Target hack duration          | Allows efficiency calculations        |
| Target XP reward              | Allows XP-focused strategies          |
| Target maximum pool           | Allows refill planning                |
| Upgrade definitions and costs | Allows purchase optimization          |
| Current upgrade levels        | Allows upgrade planning               |
| Active job details            | Allows scheduling                     |
| Available contracts           | Enables objective-driven automation   |
| Prestige information          | Enables scripts to prepare for resets |

Some of these capabilities already exist in your implementation; the point is to make the API comprehensive and consistent.

I would also introduce a clear distinction between:

- Queries: synchronous, read-only snapshots.
- Actions: asynchronous, tick-aligned operations.

This is a very good foundation for a future scripting IDE.

## 5. Progression and economy

This is the area where I would invest more game-design work before adding many more mechanics.

### 5.1 Your upgrade system is simple and understandable

The current upgrade tracks are:

| Upgrade | Effect               |
| ------- | -------------------- |
| CPU     | Faster hacking       |
| RAM     | More concurrent jobs |
| Botnet  | Passive income       |

These are good foundational mechanics because they represent different ways of improving the operation.

However, I see a few opportunities.

CPU is straightforward but potentially too dominant.

Reducing job duration improves throughput, XP generation and money generation. It therefore indirectly improves almost everything.

RAM increases throughput by allowing more concurrent work, but its usefulness depends on target availability and the efficiency of the automation.

Botnet provides a different income stream, which is good, but it doesn't directly improve the hacking operation.

I would make sure all three have meaningful investment windows rather than allowing one upgrade to dominate the others.

### 5.2 Your skill tree isn't quite a tree yet

Currently, perks are independent upgrades with maximum levels.

There are no prerequisites or branches.

That's perfectly fine for an initial implementation, but I would eventually consider making it a genuine progression tree.

For example:

Possible perk structure

Intrusion techniques

Overclock

Hack speed

Data mining

XP generation

Multitasking

Concurrency

Ransomware

Hack revenue

Illustrative structure, not a proposed final balance.

I would also consider making the choice of perks more consequential.

If players can eventually max every perk without meaningful restrictions, the skill system becomes another checklist rather than a strategic decision.

### 5.3 Prestige is promising

I like the identity-burning concept.

The manual reset and scaling reward create a push-your-luck decision, which is more interesting than automatically resetting at a fixed threshold.

I would pay particular attention to three things:

1. How long it takes to reach the first prestige.
2. How much faster the second run becomes.
3. Whether players have a reason to delay prestige rather than always resetting immediately.

The permanent implants are a good mechanism for this.

One improvement would be to make the prestige screen show a comparison:

- Tokens earned if resetting now.
- Tokens potentially earned at the next meaningful threshold.
- Current run bonuses that will be lost.
- What the next implant level would provide.

That makes the decision understandable without removing the player's freedom.

### 5.4 Build a balance simulator

Priority: High, before substantial new content

I would build a separate balance-testing tool.

It doesn't need a UI.

Give it several strategies:

- Manual hacking of one target.
- Naive automated hacking.
- Target rotation.
- Heat-aware rotation.
- Money-pool-aware rotation.
- Optimized script-driven operation.

Then measure:

- Money per minute.
- XP per minute.
- Time to next level.
- Time to next upgrade.
- Time to prestige.
- Efficiency of CPU versus RAM investment.
- Performance of different scripts.

This is particularly important because the game's intended differentiator is automation.

You need to know whether better scripts actually outperform simpler ones.

Otherwise, you risk building an elaborate scripting environment without giving players a compelling reason to use it.

## 6. Persistence and content architecture

There are some relatively concrete improvements here.

### 6.1 Handle missing target state when loading older saves

Priority: High

Your `GameDesign.md` explicitly anticipates content evolving over time.

However, `GameSession` initializes target state only when creating a new game:

```
if (loadedState == null)
{
    State = new GameState();
    HackSystem.InitializeTargets(State, content);
}
else
{
    State = loadedState;
}
```

Imagine a player has a save containing 18 targets, and you introduce a nineteenth target in a content update.

The loaded state will not contain that target.

`HackSystem.StateFor()` throws an exception when it cannot find the corresponding state.

This means adding content can break existing saves.

I would introduce a reconciliation step during loading:

```
HackSystem.EnsureTargetStates(State, content);
```

It should:

- Initialize missing targets.
- Preserve existing target state.
- Handle removed targets deliberately.
- Avoid silently resetting existing progress.

This is a small change with significant long-term value.

### 6.2 Corrupt saves should not silently become new games

Priority: High

Currently, `SaveSystem.Load()` returns `null` when JSON deserialization fails or the schema version is unsupported.

The console application interprets this as a new game.

That creates a potentially serious user-experience problem: a damaged save can appear to be a missing save, and the next save can overwrite the previous file.

I would distinguish between:

- No save exists.
- Save loaded successfully.
- Save is corrupt.
- Save version is unsupported.

For corrupt saves, preserve the original file and provide a recovery option.

I would also keep a backup of the previous valid save.

### 6.3 Your content validation needs to become more comprehensive

`GameContent.Validate()` is a good start.

It checks duplicate IDs in several content categories and validates some numerical properties.

But it doesn't currently validate everything that can cause problems.

I would add checks for:

- Achievement IDs (they are currently missing from duplicate checking).
- Empty IDs and names.
- Valid target requirements.
- Positive and finite maximum money values.
- Valid contract goals.
- Valid achievement and contract stat keys.
- References to nonexistent targets or other content.
- Values that could produce invalid cost or reward calculations.

An especially important example is the stat key.

`AchievementsSystem.StatValue()` returns zero for unknown keys.

A typo in a JSON achievement definition could therefore leave an achievement permanently unobtainable without producing a loading error.

Content validation should catch this before the game starts.

### 6.4 Introduce explicit save migrations

You have a schema version, which is good.

But currently, only version 1 is accepted.

As the game develops, I would introduce a migration pipeline:

```
Save v1
   |
   v
Migrate to v2
   |
   v
Migrate to v3
   |
   v
Current save format
```

Also consider separating save compatibility from content compatibility.

A save format migration and a content update are related but not the same problem.

### 6.5 Consider the offline progression design

Your offline cap is currently eight hours.

The design document describes returning after a day as an important progression experience.

Those two choices aren't necessarily compatible.

I would decide whether you want:

- A hard eight-hour cap.
- A full 24-hour cap.
- Diminishing returns after a certain period.
- Different offline efficiency depending on upgrades.

I would lean towards diminishing returns or a configurable cap, rather than permanently discarding most of a player's absence.

There is another detail worth making explicit: `ContractsSystem.Tick()` is skipped during fast-forward. Consequently, active contract deadlines don't expire while the player is offline, even though other progression continues.

That might be intentional, but it should be a deliberate game rule.

## 7. Project structure and future UI

You currently have five main projects:

- `NullByte.Core`
- `NullByte.Scripting`
- `NullByte.ConsoleUI`
- `NullByte.Core.Tests`
- `NullByte.Scripting.Tests`

There is also a WPF project, `NullByte`, which currently contains little beyond the application and an empty main window.

I would keep this structure for now.

I would not introduce additional projects just for the sake of architectural purity.

However, I would eventually make a distinction between the following responsibilities:

| Component    | Responsibility                           |
| ------------ | ---------------------------------------- |
| Core         | Game rules, state and simulation         |
| Scripting    | Script compilation, execution and API    |
| Application  | Composition, lifecycle and orchestration |
| Presentation | Console or WPF interface                 |
| Content      | Definitions and game configuration       |

You already have most of these responsibilities represented.

### One suggestion for the future WPF implementation

Don't make the WPF application responsible for running the simulation.

Instead, let it consume the same `GameSession` abstraction as the console application.

I would also introduce a presentation-facing snapshot or view-model layer.

For example, the UI should be able to display:

- Money and income.
- XP and level progress.
- Active jobs.
- Target money and heat.
- Script execution status.
- Script output.
- Available upgrades.

It should not need to know how the hacking calculations work.

This will make the transition from console to WPF much easier.

I would also avoid updating the UI on every simulation tick unnecessarily. At 10 Hz, you can update the presentation at a lower frequency while keeping the simulation at its fixed rate.

## 8. Code quality and maintainability

A few additional observations.

### Things I particularly like

1\. The systems are reasonably well separated.

`HackSystem`, `EconomySystem`, `ProgressionSystem`, `PrestigeSystem` and `ContractsSystem` have distinct responsibilities.

This is much better than putting all game logic into `GameSession`.

2\. Commands are small and understandable.

The command implementations are straightforward and make it easy to see how player actions enter the engine.

3\. You are using records for content definitions.

This makes the content model relatively lightweight.

4\. The scripting tests are more thoughtful than average.

I like that you explicitly account for asynchronous scheduling and avoid brittle assertions about exact tick counts.

5\. The DevJournal is useful.

It provides context for why certain decisions were made. I would continue maintaining it, particularly for decisions that affect game mechanics.

### Things I would clean up

- Naming conventions are inconsistent. For example, there is a mixture of `snake_case` and PascalCase/camelCase in the C# implementation.
- Several systems use static random generators, which makes testing harder.
- Some systems depend directly on string identifiers for gameplay effects, such as `"cpu"`, `"botnet"` and `"ransom"`.
- Some functionality is implemented using repeated dictionary lookups and hardcoded effect calculations.
- The console application's `Program.cs` is becoming quite large and handles several responsibilities.

None of these are immediate architectural problems, but I would address them incrementally.

In particular, I would consider a central upgrade-effect/modifier mechanism as the number of upgrades, perks and implants grows.

For example, instead of having every system independently know about specific perks, you could have a mechanism that calculates the effective hacking speed, money multiplier, XP multiplier and so on.

I would avoid building an elaborate modifier framework prematurely, though.

## 9. My recommended priorities

If I were taking over development of NullByte, this is the order in which I would work.

## Priority 1 – Stabilize the foundations

Highest priority

These are things I would address before expanding the game substantially.

- Make GameSession synchronization instance-specific.
- Stop exposing mutable GameState collections.
- Fix ScriptingSystem collection concurrency.
- Improve script cancellation and execution budgeting.
- Handle missing target states when loading saves.
- Protect against corrupt-save data loss.
- Expand content validation.

## Priority 2 – Make automation the core gameplay

Highest design priority

- Define the progression from manual to purchased to programmed automation.
- Expand the scripting API.
- Establish clear script persistence semantics.
- Build a balance simulator for competing strategies.
- Ensure sophisticated scripts outperform simplistic automation.

## Priority 3 – Improve progression

- Evaluate CPU/RAM/Botnet balance.
- Introduce meaningful perk choices and dependencies.
- Evaluate the first and subsequent prestige cycles.
- Make target rotation strategically interesting.
- Review offline progression and contract deadlines.

## Priority 4 – Expand the game

- Network discovery and node relationships.
- Factions and reputation.
- More sophisticated automation capabilities.
- Research and additional progression systems.
- WPF interface and script editor.

## 10. What I would deliberately NOT do yet

There are several tempting improvements that I would postpone.

Don't build the complete WPF interface yet.

The console is perfectly adequate for developing and testing the game mechanics.

Don't introduce more currencies yet.

You already have money, XP, skill points and 0-days. There is plenty of progression complexity to explore.

Don't introduce a complex plugin/mod architecture.

Your content definitions are already reasonably data-driven. Improve validation and extensibility before adding another abstraction.

Don't add dozens of targets just to increase content volume.

Make the existing targets interesting first.

Don't overengineer the simulation.

The fixed-tick model is already a good foundation. You don't need an ECS, a distributed event system or a complicated dependency-injection framework.

## 11. Overall assessment

# My assessment of NullByte

# Good foundation

Qualitative assessment based on static code review, not a test or benchmark run.

| Area                   | Assessment                 |
| ---------------------- | -------------------------- |
| Overall architecture   | Strong                     |
| Separation of concerns | Good                       |
| Game simulation        | Good foundation            |
| Hacking mechanics      | Promising                  |
| Progression design     | Needs balancing            |
| Scripting architecture | Promising, needs hardening |
| Persistence            | Needs improvement          |
| Content extensibility  | Good foundation            |
| Automated testing      | Good start                 |
| Presentation layer     | Early stage                |

My main conclusion is that you are building the right technical foundations for the game you describe, but the next challenge is making the scripting system and the incremental mechanics reinforce each other.

I would resist the temptation to add more conventional incremental-game features just because they are easy to implement.

The real differentiator is the player's ability to develop increasingly sophisticated solutions to the game's problems.

A player should ideally progress from:

- "I need more money."
- "I need to hack faster."
- "I need to manage several targets."
- "I need a better algorithm."
- "I need to write a better program."

That is the progression I would protect.

One final qualification: this review is based on the supplied merged source snapshot. I have performed a static architectural and code review, not compiled the solution or executed its test suites. The findings above should therefore be treated as code-review observations rather than verified runtime defects.

My suggested next step: I would tackle the scripting architecture first, particularly execution budgeting, cancellation and concurrency. It is the most technically distinctive part of NullByte, and getting it right will influence almost every major feature you add afterwards.