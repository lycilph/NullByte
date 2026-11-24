# NullByte Design

This document is the authoritative product and game-design specification for NullByte.

It defines:

- the game's vision
- gameplay pillars
- player-facing mechanics
- scripting behavior
- intended progression
- milestone scope and acceptance criteria
- future gameplay direction

Technical implementation details belong primarily in `ARCHITECTURE.md`.

For other perspectives:

- `AI_CONTEXT.md` defines important development rules and invariants.
- `ARCHITECTURE.md` describes the technical system and component relationships.
- `CODEBASE.md` maps concepts to source files.
- `CURRENT_STATUS.md` records what is currently implemented and being worked on.

Where implementation and this document disagree, treat the discrepancy explicitly rather than silently changing either the design or the implementation.

---

# 1. Vision

NullByte is a text-driven incremental hacking game in which the player gradually replaces manual work with automation.

The player begins by typing commands directly into a terminal:

- scan networks
- connect to servers
- gain root access
- hack targets
- manage files
- earn money

Eventually, doing this manually becomes repetitive.

The solution is not faster clicking.

The solution is programming.

The player writes C# scripts against the NullByte API and lets those scripts perform the same actions automatically.

As the player's infrastructure and programs improve, more of the game becomes automated.

The intended progression is:

```text
Do something manually
        |
        v
Understand the problem
        |
        v
Automate it with a script
        |
        v
Scale the automation
        |
        v
Encounter a larger problem
        |
        v
Write better automation
```

The core fantasy is inspired by Bitburner:

> Start by typing commands. Get bored. Write a script. Then write a better script.

---

# 2. Design pillars

## 2.1 Scripting is the game

Programming is not an optional side system.

Writing increasingly capable scripts is the primary progression mechanism.

The game should regularly present the player with situations where automation feels like the obvious improvement.

---

## 2.2 Manual and scripted actions follow the same rules

A manual Hack and a scripted Hack are the same gameplay action.

The same applies to:

- Grow
- Weaken
- network interaction
- file operations
- Botnet management
- other automatable systems

Scripts gain power from automation, repetition and scale, not from secretly stronger versions of game mechanics.

---

## 2.3 Automation should feel earned

The player should first understand a mechanic manually.

Automation then provides relief from repetition.

A mechanic that is automated before the player understands it loses much of its programming-game value.

---

## 2.4 Waiting creates motivation to automate

Actions intentionally take time.

Hack, Grow and Weaken should be long enough that manual repetition becomes undesirable.

The player should naturally think:

> I could write something to do this for me.

---

## 2.5 Programs should compose

The player should gradually build a personal toolbox.

Scripts can therefore use reusable libraries through `#include`.

The intended evolution is from individual scripts toward reusable player-written infrastructure.

---

## 2.6 Start small and expand

The first playable version deliberately focuses on a narrow hacking/automation loop.

The design should leave room for later systems such as:

- factions
- augmentations
- programs
- larger networks
- purchased servers
- additional economies
- graphical interfaces

These should expand the core automation fantasy rather than obscure it.

---

# 3. First playable scope

The initial major game arc includes:

- player money
- hacking experience and skill
- Botnet passive income
- a hand-authored server network
- server discovery
- connecting between servers
- gaining root access
- Hack
- Grow
- Weaken
- virtual files
- executable scripts
- script arguments
- script processes
- script threads
- RAM limitations
- reusable script libraries
- jobs and training/study activities
- save/load
- VS Code integration

---

# 4. Explicitly deferred scope

The following are not required for the first playable release:

- factions
- augmentations
- port-opening programs
- purchased servers
- stock market
- corporations
- Bladeburner-style systems
- Sleeves
- gangs
- Singularity-style API
- offline progression
- multiplayer
- full graphical UI

Some may be introduced later.

---

# 5. Player

The player owns or tracks:

- money
- hacking experience
- derived hacking skill
- current server
- discovered servers
- current console directory
- Botnet
- current activity

Home is a server and acts as the player's initial computing environment.

---

# 6. Skills and experience

Hacking skill is derived from hacking experience.

Experience is earned through hacking-related activity such as:

- Hack
- Grow
- Weaken

Other skills may be introduced as activities and progression expand.

The exact numerical formulas are balance concerns and should remain tunable and testable.

---

# 7. Network

The game world contains a network of connected servers.

The first version uses a hand-authored network rather than procedural generation.

A server may contain:

- hostname
- neighbors
- hacking requirement
- money
- maximum money
- security
- minimum security
- growth characteristics
- RAM
- used RAM
- root-access state
- virtual files

The initial network should contain roughly enough servers to provide a meaningful progression from trivial early targets to targets requiring substantially more capability.

---

# 8. Server discovery

The player does not automatically see the entire network.

Servers become known through exploration.

`scan` reveals reachable servers.

Once discovered, servers may become visible through other interfaces, including the VS Code virtual filesystem.

Unknown servers should remain hidden.

---

# 9. Connecting

The player operates interactively from a current server.

Connecting changes the current interactive server.

Changing server also resets the interactive working directory to that server's root.

Script execution does not depend on the interactive shell's current directory.

---

# 10. Root access

Some actions require root access.

The early design uses `nuke` as the primary root-access operation.

More elaborate port-opening requirements are intentionally deferred.

This can later become a progression gate involving player-created/acquired programs.

---

# 11. Hack

Hack attempts to steal money from a server.

Its behavior depends on factors such as:

- player hacking skill
- server requirement
- server security
- thread count

Hack:

- has a success probability
- takes game time
- steals money on success
- increases security
- grants hacking experience

Hack duration should be significant enough that manual repeated hacking strongly motivates automation.

---

# 12. Grow

Grow increases the target server's available money.

Its effectiveness depends on:

- thread count
- server growth characteristics
- security

Grow:

- takes game time
- increases server money
- raises server security
- grants hacking experience

Money must remain bounded by the server's maximum money.

---

# 13. Weaken

Weaken reduces server security.

It:

- takes game time
- scales with thread count
- cannot reduce security below the server's minimum
- grants hacking experience

Together, Hack, Grow and Weaken produce the fundamental optimization problem around which early player automation is built.

---

# 14. The first automation problem

A useful early script should naturally emerge from gameplay.

Conceptually:

```text
if security is high
    weaken
else if money is low
    grow
else
    hack
```

The player should be able to discover this pattern without being forced into exactly one implementation.

More sophisticated players can improve:

- thresholds
- thread counts
- timing
- targeting
- parallelism
- RAM usage
- coordination across servers

This creates room for programming skill to matter.

---

# 15. Botnet

Botnet provides passive income before and alongside scripting.

A Botnet consists of nodes.

Each node has:

- level
- RAM
- cores

The player can:

- buy nodes
- upgrade level
- upgrade RAM
- upgrade cores

Production grows as nodes improve.

Costs grow as infrastructure becomes stronger.

Botnet provides an early incremental-game loop and prevents hacking from being the player's only source of progress.

---

# 16. Botnet balance

Botnet and scripting should complement rather than replace each other.

Botnet should:

- provide reliable passive income
- help bootstrap the early game
- reward investment

But a well-designed script-based hacking economy should eventually become a major source of progression.

Neither economy should permanently make the other irrelevant.

This balance receives a dedicated pass before the first playable release.

---

# 17. Activities

The player can perform longer-running activities outside direct server hacking.

Examples include:

- jobs
- training
- studying

Activities can provide:

- money
- experience
- progression

Only one foreground player activity should normally occupy the corresponding player state at a time.

Activities continue according to game time.

---

# 18. Jobs

Jobs allow the player to exchange time for income.

The job system may include:

- companies
- positions
- ranks
- requirements
- promotion
- salary

Jobs provide an alternative early progression route and a context in which skills other than pure hacking can eventually matter.

---

# 19. Training and study

Courses/training allow the player to exchange time and potentially money for skill progression.

This gives the player another route through progression bottlenecks.

The system should create meaningful choices rather than merely requiring idle waiting.

---

# 20. Game time

Gameplay runs according to game time.

The normal initial relationship is approximately:

**1 second real time = 1 second game time**

A development/configuration multiplier may accelerate simulation.

Game time drives systems such as:

- hacking operations
- Botnet production
- activities
- script sleep

The game may continue progressing while the interactive console waits for a manual action.

---

# 21. Manual blocking actions

When the player manually executes a long action such as Hack, Grow or Weaken, the console waits for that operation to finish before accepting another normal command.

This does **not** pause the simulation.

While the player waits:

- scripts continue
- Botnet continues
- activities continue where applicable
- scheduled gameplay continues

This deliberately makes automation more powerful than manual interaction without giving scripts different rules.

---

# 22. Virtual filesystem

Each server has its own virtual filesystem.

Player scripts never use the real operating-system filesystem.

Game files include:

- scripts
- libraries
- other player-created files

Files are stored as part of game state and persist through saves.

---

# 23. Virtual paths

A typical path looks like:

`tools/net/scan.cx`

Paths are relative to a server root.

A leading `/` is also accepted:

`/tools/net/scan.cx`

Both identify the same file.

Important rules:

- `/` is the separator.
- Names are case-sensitive.
- Paths are limited to 8 levels.
- Paths are limited to 256 characters.
- A path cannot simultaneously represent a file and folder.
- Empty path segments are invalid.
- Raw filesystem paths cannot contain unresolved `.` or `..`.
- Backslashes and control characters are invalid.

Folders may exist even when they contain no files.

---

# 24. Console working directory

The interactive console has a current working directory.

Commands such as:

- `cd`
- `pwd`
- `ls`
- `cat`
- `edit`
- `rm`
- `mkdir`
- `rmdir`
- `mv`

operate with familiar shell-like relative path behavior.

For console commands:

- `/foo` starts from server root.
- `foo` starts from the current directory.
- `.` refers to the current directory.
- `..` moves toward root.

The prompt reflects the current location.

Example:

`root@home:/tools $`

---

# 25. Script path behavior

Scripts do **not** inherit the console working directory.

Script-facing paths always resolve from the server root.

This ensures a script behaves identically regardless of where the player happens to be in the interactive shell.

This applies to operations such as:

- file API calls
- `Run`
- `Exec`

---

# 26. File operations

The game supports file operations such as:

- list
- read
- write/edit
- delete
- create directory
- remove directory
- move
- copy

Compound operations should behave atomically from the player's perspective.

A failed move or copy should not leave a mysterious partially completed filesystem state.

---

# 27. Script files

Player scripts conventionally use the `.cx` extension.

They contain ordinary C#-style top-level statements.

Example:

```text
var target = args[0];

while (true)
{
    if (nb.GetServerSecurityLevel(target) >
        nb.GetServerMinSecurityLevel(target) + 5)
    {
        await nb.Weaken(target);
    }
    else if (nb.GetServerMoneyAvailable(target) <
             nb.GetServerMaxMoney(target) * 0.75)
    {
        await nb.Grow(target);
    }
    else
    {
        await nb.Hack(target);
    }
}
```

The player does not write an explicit entry class or entry method.

---

# 28. Implicit script context

Main scripts receive two implicit concepts:

`nb`

The NullByte scripting API.

`args`

The script's arguments.

This keeps simple automation scripts concise.

---

# 29. Script execution

Scripts are compiled before execution.

The player can:

- run a script
- execute scripts on appropriate remote servers
- supply arguments
- select thread count
- inspect running processes
- kill processes

A running script has a process identity.

Important process information includes:

- PID
- filename/path
- host
- arguments
- thread count
- RAM usage
- state

---

# 30. Script RAM

RAM is a gameplay resource.

Every script has a static RAM cost determined by the API capabilities it uses.

Conceptually:

```text
base script RAM
+
cost of distinct API methods used
=
script RAM
```

Running with multiple threads multiplies the required RAM:

```text
required RAM =
script RAM * thread count
```

Execution fails clearly when the target server does not have enough free RAM.

---

# 31. RAM and programming decisions

RAM should create interesting programming decisions.

The player may choose between:

- one large general-purpose script
- several focused scripts
- small reusable libraries
- running many lightweight workers
- fewer feature-heavy workers

The RAM system should reward understandable optimization rather than obscure compiler tricks.

---

# 32. Reusable libraries

Scripts can reuse other source files with:

`#include "path"`

Example:

```text
#include "lib/net.cx"

var target = args[0];
await Net.WeakenTo(nb, target, 5);
```

A library might conceptually contain:

```text
using NullByte.Core.Scripting;

public static class Net
{
    public static async Task WeakenTo(
        INullByteApi nb,
        string target,
        double margin)
    {
        while (nb.GetServerSecurityLevel(target) >
               nb.GetServerMinSecurityLevel(target) + margin)
        {
            await nb.Weaken(target);
        }
    }
}
```

---

# 33. Library rules

Included libraries:

- may contain using directives
- may contain type declarations
- may not contain top-level executable statements
- do not receive implicit `nb`
- do not receive implicit `args`

Library functions receive dependencies such as `INullByteApi` explicitly.

This encourages ordinary reusable programming patterns.

---

# 34. Include path behavior

Includes are resolved relative to the server root.

These refer to the same file:

```text
#include "lib/net.cx"
#include "/lib/net.cx"
```

Libraries are read from the server where the script is compiled/run.

Copying a script to another server does not automatically copy its libraries.

A missing library causes compilation to fail clearly.

---

# 35. Include graph rules

Includes can be nested.

Rules:

- each distinct file is included once
- cycles are errors
- nesting is limited to 8 levels
- invalid paths are errors
- missing files are errors
- malformed directives are errors

The player should receive diagnostics that identify the relevant file and source location.

---

# 36. Include diagnostics

NullByte-specific include errors use:

| ID | Meaning |
|---|---|
| NB1001 | Include not found |
| NB1002 | Include cycle |
| NB1003 | Include nesting deeper than 8 |
| NB1004 | Invalid include path |
| NB1005 | Malformed `#include` |
| NB1006 | Top-level statements in a library |

These errors should behave like first-class compiler diagnostics.

---

# 37. Includes and RAM

Included libraries contribute to the parent script's RAM cost.

Current rule:

- base cost is counted once
- each distinct charged API method is counted once across all included source
- library source counts even when a particular library function is not reached at runtime

This deliberately favors small focused libraries.

A more sophisticated reachability model may be considered later if whole-file charging proves too coarse.

---

# 38. Includes and running processes

A running script represents code compiled when that process started.

Changing:

`lib/net.cx`

does not mutate existing running processes using that library.

The new source affects subsequent compilations.

This provides predictable runtime behavior.

---

# 39. NullByte API

The script API is exposed through `nb`.

The initial API is grouped conceptually into:

## Actions

- Hack
- Grow
- Weaken
- Sleep

## Server information

- available money
- maximum money
- security
- minimum security
- required hacking level
- RAM information
- root-access information

## Player information

- hacking level

## Network

- Scan
- Nuke

## Scripts/processes

- Run
- Exec
- Kill
- Ps
- Scp
- ScriptRunning
- GetScriptRam

## Files

- List
- Delete
- Move
- Copy

## Botnet

- node information
- purchase node
- upgrade level
- upgrade RAM
- upgrade cores
- upgrade-cost queries

## Output

- Print
- Tprint

The API should grow according to gameplay needs rather than exposing the entire internal game model.

---

# 40. Script diagnostics

Compilation diagnostics should be understandable in terms of the files the player wrote.

They should provide:

- file
- line
- column/range
- severity
- diagnostic identifier where applicable
- message

Generated implementation details should not leak unnecessarily into normal player diagnostics.

---

# 41. Script safety

Player scripts eventually require deliberate restrictions.

The intended model allows useful ordinary programming while preventing APIs that can interfere with the game or host machine.

Capabilities to restrict include:

- host filesystem access
- arbitrary network access
- process launching
- reflection
- arbitrary assembly loading
- uncontrolled threading
- blocking waits

Synchronous infinite loops also require mitigation because scripts execute within the simulation model.

This is intended as practical sandboxing for a local hobby game, not as a hardened boundary against hostile native-level code.

---

# 42. Save and load

Game state can be persisted.

The save contains the state required to continue the game, including appropriate portions of:

- player state
- network/server state
- Botnet
- virtual files
- directories
- file metadata
- game progression
- script process information needed to restart processes

Save formats are versioned.

Older supported formats migrate forward.

Damaged persisted state should fail clearly instead of silently creating invalid game behavior.

---

# 43. Running scripts after load

Executable CLR state is not serialized.

Instead, script processes are represented by game-level information sufficient to reconstruct them.

On load, scripts are compiled again from the files in the restored game state.

This means restored execution reflects the saved source state.

---

# 44. Autosave

The game autosaves periodically.

Manual save/load is also available.

Autosave should protect player progress without becoming intrusive.

A true new-game/reset operation removes the previous autosave so it cannot accidentally resurrect the previous game.

---

# 45. Reset / new game

Reset means:

> Start a genuinely new game.

It is not merely a command that zeroes selected properties of the existing state.

A reset creates fresh game state and discards the previous active game.

Where active foreground behavior makes reset unsafe or confusing, the player must first stop/background that activity according to the current rules.

Reset should be explicit because it destroys progress.

---

# 46. VS Code integration

VS Code can optionally be used as a richer editor for NullByte scripts.

The intended player experience is:

```text
Run NullByte with editor integration
        |
        v
Open the NullByte workspace in VS Code
        |
        v
Browse discovered servers
        |
        v
Edit .cx files
        |
        v
Receive C#-aware assistance
        |
        v
Save changes directly into the game
```

The integration is optional.

Running NullByte without VS Code should remain a complete supported experience.

---

# 47. VS Code filesystem experience

VS Code presents discovered NullByte servers and their files as a virtual workspace.

The developer/player can perform ordinary editor operations such as:

- browse folders
- open files
- edit scripts
- create files
- create folders
- rename
- move
- delete

Changes affect the actual in-game virtual filesystem.

Changes made from inside the game should appear in VS Code without requiring manual refresh.

---

# 48. VS Code language experience

`.cx` files receive language support appropriate to the NullByte scripting environment.

Features include:

- syntax highlighting
- compiler diagnostics
- completion
- hover
- signature help
- go to definition into player libraries

API information should include useful NullByte-specific documentation, including RAM-cost information where applicable.

Editor suggestions should reflect what the game actually allows scripts to compile against.

---

# 49. VS Code includes

Editor language features understand `#include`.

For a script using libraries, VS Code should be able to:

- diagnose library errors
- complete library types/members
- show hover information
- provide signature help
- navigate to declarations

An error in a library should appear against the library file rather than misleadingly appearing in the including script.

---

# 50. VS Code connection behavior

Editor integration is opt-in.

Normal game startup should not open an editor server unless requested.

Multiple running NullByte games should not inherently prevent each other from using editor integration.

The extension should make connection state understandable to the user.

---

# 51. VS Code workflow improvements

The continuing editor integration milestone adds:

- inspect currently connected external editors
- full diagnostic underline ranges
- run the current script from VS Code
- query current script RAM from VS Code
- launch VS Code from within the game

These features should improve workflow without introducing editor-only gameplay rules.

---

# 52. Progression philosophy

NullByte progression should increasingly reward:

- understanding game mechanics
- automation
- abstraction
- reuse
- scheduling
- resource allocation
- optimization

Progress should not depend primarily on repetitive manual input.

Whenever a task becomes tedious, the player should have a plausible route toward automating it.

---

# 53. Intended early-game arc

The first playable experience should broadly progress as follows.

## Stage 1: Learn the terminal

The player:

- scans
- navigates the network
- analyzes servers
- gains root
- performs manual hacks

## Stage 2: Establish income

The player:

- earns hacking income
- acquires initial Botnet capacity
- begins accumulating passive resources

## Stage 3: Learn scripting

The player writes a first script.

A simple Hack/Grow/Weaken loop should create an obvious improvement over manual play.

## Stage 4: Learn resource constraints

RAM becomes important.

The player learns:

- script RAM cost
- threads
- process management
- remote execution

## Stage 5: Scale

The player begins running automation across multiple servers.

## Stage 6: Build tools

Includes encourage reusable utilities and increasingly sophisticated programs.

## Stage 7: Optimize

The player starts thinking about:

- target selection
- server preparation
- thread allocation
- RAM efficiency
- multiple hosts
- reusable automation

By this point the player's programs, rather than individual terminal commands, should drive much of the economy.

---

# 54. Balance target

The first major balance pass should create a playthrough of roughly:

**1 to 2 hours**

to reach the final tier of the initial in-scope server progression.

By that stage:

- script-driven income should clearly outperform manual hacking
- Botnet should remain useful
- RAM constraints should matter
- better scripts should produce observable benefits
- the player should feel that their own automation caused the progression

The exact duration is a tuning target rather than an immutable requirement.

---

# 55. Tutorial philosophy

The first release should not require external documentation to understand basic play.

The game should teach primarily through:

- `help`
- discoverable commands
- understandable error messages
- API documentation
- sample scripts

A small number of initial example scripts may demonstrate core concepts without solving the entire game for the player.

---

# 56. Milestone philosophy

Each development milestone should end with something runnable and tested.

A milestone is complete when its acceptance criteria are satisfied, not merely when the intended classes exist.

Detailed current completion state belongs in `CURRENT_STATUS.md`.

---

# 57. M0: Foundations

## Deliverables

- solution/project structure
- target framework
- nullable references
- test framework
- basic configuration
- application banner

## Acceptance

- solution builds
- tests run
- application starts

---

# 58. M1: Game loop and synchronization

## Deliverables

- game synchronization context
- game loop
- real/fake game clock
- scheduler
- async continuation support

## Acceptance

- awaited game operations resume on the game thread
- timer ordering is deterministic
- cross-thread posting safely reaches the game thread

---

# 59. M2: Model, network and formulas

## Deliverables

- player
- server
- network
- initial network data
- hacking formulas
- skill formulas
- Hack/Grow/Weaken service

## Acceptance

- formulas have known-value tests
- simulated hacking behaves within expected ranges
- money/security invariants hold

---

# 60. M3: Console and manual actions

## Deliverables

Commands including the initial set for:

- help
- network exploration
- connection
- files
- nuke
- analyze
- Hack
- Grow
- Weaken
- player status

Also:

- interactive prompt
- progress display
- manual action blocking

## Acceptance

A player can discover, connect to, root and manually hack an early server.

The simulation continues while the console waits on long manual actions.

---

# 61. M3.5: Console quality of life

## Deliverables

- command/server completion
- input history

## Acceptance

- Tab completes commands and appropriate values
- Up/Down navigates previous input

---

# 62. M4: Botnet

## Deliverables

- Botnet domain
- node purchase
- upgrades
- production
- costs
- console interaction

## Acceptance

- simulated production matches formulas
- the early economy can bootstrap through manual hacking and Botnet income

---

# 63. M5: Roslyn scripting

## Deliverables

- virtual script files
- Roslyn compilation
- script execution
- diagnostics
- first `nb` API

## Acceptance

- a useful hacking script executes successfully
- compiler errors map back to player source
- generated script assemblies can be released/unloaded

---

# 64. M6: Processes, threads and RAM

## Deliverables

- processes
- PIDs
- cancellation
- static RAM analysis
- server RAM enforcement
- thread counts
- process commands
- remote execution
- expanded script API

## Acceptance

- insufficient RAM is rejected clearly
- killing a script releases its resources
- threads scale both gameplay effect and RAM appropriately

---

# 65. M7: Persistence

## Deliverables

- serialization
- versioned save format
- migration
- manual save/load
- autosave
- process restart behavior

## Acceptance

Saving, quitting and loading restores equivalent gameplay state.

Golden tests protect important format compatibility.

---

# 66. M8: VS Code integration

## Deliverables

- optional editor server
- authenticated local protocol
- VS Code extension
- virtual filesystem
- file/folder operations
- live filesystem notifications
- `.cx` language
- diagnostics
- completion
- hover
- signature help
- editor discovery

## Acceptance

A player can edit a script in VS Code while the game runs and execute the changed source in NullByte.

Game/editor file changes synchronize correctly.

The base game requires no editor integration when it is disabled.

---

# 67. M9: Script includes

## Deliverables

- `#include`
- recursive resolution
- cycle/depth errors
- library validation
- compiler support
- RAM support
- diagnostics
- editor support
- go to definition

## Acceptance

Players can build reusable libraries.

Compiler, RAM analysis and editor behavior agree on included source.

Errors point to the correct file/location.

Copying a script without its required libraries causes a clear failure.

---

# 68. M10: Activities

## Deliverables

- jobs
- employment
- training/study
- activity progression
- related console interaction

## Acceptance

The player can perform work for income and study/train for progression.

---

# 69. M11: VS Code improvements

## Deliverables

- debug visibility into connected editors
- full-range diagnostic underlines
- Run from VS Code
- query RAM from VS Code
- launch VS Code from NullByte

## Acceptance

The updated extension provides a significantly smoother write-test-optimize workflow without bypassing existing script/runtime rules.

Current progress on these deliverables belongs in `CURRENT_STATUS.md`.

---

# 70. M12: Sandbox and safety

## Deliverables

- controlled assembly references
- banned API analysis
- blocking-operation restrictions
- protection against synchronous infinite loops
- containment of script exceptions

## Acceptance

Representative unsafe or broken scripts are either rejected or contained, including attempts involving:

- host filesystem
- reflection
- dangerous runtime APIs
- endless synchronous loops
- unhandled exceptions

The game continues operating correctly.

---

# 71. M13: Progression and balance

## Deliverables

- tuned network progression
- tuned payouts
- Botnet/hacking economic balance
- home RAM upgrades
- tutorial/help improvements
- starting sample scripts

Purchased servers may be included if appropriate.

## Acceptance

A roughly 1-2 hour first playthrough reaches the final initial server tier.

The player experiences the intended arc:

```text
manual play
    ->
passive infrastructure
    ->
first script
    ->
distributed automation
    ->
optimization
```

Script-driven progression is clearly more effective than repeated manual input.

---

# 72. M14: First playable release

## Deliverables

- stabilization
- bug fixes
- README
- sample scripts
- how-to-play documentation
- packaged/tagged build

## Acceptance

Someone other than the author can install NullByte, learn the first progression arc and write a working script using the game and supplied documentation.

---

# 73. After the first playable

Potential expansion areas include:

## WPF host

A graphical host using WPF, potentially including AvalonEdit.

Possible UI:

- terminal
- script editor
- file browser
- status panels
- diagnostics
- completion

The gameplay should remain the same game rather than becoming a separate GUI version.

---

## Improved include workflow

Potential improvements:

- automatically recheck dependent scripts when libraries change
- use unsaved open-library source during editor analysis
- optional reachability-based RAM charging

---

## Console improvements

Potential features:

- recursive `scp`
- recursive listing
- immediate completion refresh for externally changed files

---

## Larger progression

Potential systems:

- factions
- augmentations
- prestige/reset loop
- more servers
- port-opening programs
- purchased servers

---

## Additional economies

Longer-term possibilities:

- stock market
- larger business/economic systems
- offline progress

These should be judged against the core automation/programming fantasy before implementation.

---

## Script API evolution

As the game expands, API compatibility becomes increasingly important.

A deliberate versioning strategy may eventually be required so a large player script library is not broken casually by game updates.

---

# 74. Testing expectations

Correctness is particularly important because players build automation on top of stable rules.

Important testing areas include:

- formulas and numerical invariants
- deterministic simulation
- game-thread ownership
- clocks and scheduling
- filesystem behavior
- path resolution
- persistence and migration
- command behavior
- compiler behavior
- diagnostics
- script execution
- RAM
- includes
- language services
- editor protocol

Tests should protect externally observable game behavior rather than cement arbitrary implementation details.

---

# 75. Resolved design decisions

## Money representation

Use `double`.

Formulas remain isolated so this can be revisited if required.

## Script shape

Top-level C# statements with implicit `nb` and `args`.

A generated wrapper provides the runtime entry point.

## Input during a blocking manual action

Discard it rather than buffering commands for later execution.

## Initial time scale

Approximately 1:1 game time to real time, with configurable acceleration available.

## Target framework

.NET 10.

## Initial network data

Hand-authored JSON.

## Includes

Use:

`#include "path"`

Paths are server-root-relative.

Libraries:

- contain usings/types rather than top-level executable code
- receive `nb` explicitly where needed
- are resolved from the execution server
- are included once
- may be nested up to eight levels
- may not form cycles

RAM currently counts all included source according to the whole-file semantic model.

---

# 76. Open design questions

The previously foundational questions around money type, script shape, blocking input, initial time scale, framework, network format and include semantics are resolved.

New unresolved product/design questions should be added here rather than hidden inside implementation work.

Current likely future questions include:

- What should the prestige/reset progression eventually provide?
- How much stronger should optimized scripted hacking be than Botnet income?
- When should home RAM upgrades enter progression?
- Should purchased servers make the first playable or remain post-release?
- How much sample automation should be shipped without undermining player discovery?
- How strict should sandbox restrictions be while preserving useful ordinary C#?
- Should library RAM eventually use reachability rather than whole-file charging?
- What API compatibility/versioning guarantees should exist after the first playable?

These do not block the current M11 work unless a specific implementation depends on them.

---

# 77. Design test

When considering a new feature, ask:

1. Does it create an interesting problem?
2. Can the player initially understand or interact with the problem manually?
3. Can programming improve or automate it?
4. Can better programming improve it further?
5. Does it interact meaningfully with existing constraints such as time, RAM, money or infrastructure?
6. Does it create opportunities for reusable player-written tools?
7. Does it preserve understandable rules?
8. Does it avoid replacing programming with repetitive UI interaction?

A feature satisfying several of these questions is likely a good fit for NullByte.

The most important question is:

> **Does this give the player a reason to write a better program?**