# NullByte: Game Design Document

Version 0.1, first iteration (console), with a later WPF + AvalonEdit front end.

## 1. Vision

NullByte is a text-driven incremental game where the player grows a passive income stream (botnet) and then automates the rest of the economy by writing C# scripts against a small game API (the `nb` object), compiled at runtime with Roslyn. Manual commands and scripts use the same underlying actions, so everything a script can do the player can also do by hand, only slower.

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

### 3.4 Botnet (hacknet in bitburner)

A node has level, RAM and cores. Production per second is roughly `level * 1.5 * 1.035^(ram - 1) * (cores + 5) / 6`, times a global multiplier. Costs grow geometrically per purchase, with separate base costs and growth factors for new node, level, RAM and core. Caps: level 200, RAM 64, cores 16. All values are initial tunables.

Botnet accrues in game time, on the same simulation clock as everything else.

### 3.5 Scripts and RAM

- A script is a C# source file (by convention with the `.cx` extension) stored in the game's virtual file system (kept in memory and saved to JSON; no real disk access for scripts).
- Files live in folders. A file name is a path such as `tools/net/scan.cx`, relative to the server root; `/tools/a.cx` and `tools/a.cx` are the same file. Scripts refer to other scripts and files by such paths. The rules are in 4.9.
- The console has a current directory (`cd`, `pwd`) that its commands resolve paths against. Scripts always use paths from the server root, so they behave the same wherever the player is.
- Each script has a static RAM cost: a base cost plus the sum of costs for each distinct `nb` method it calls, found by analyzing the code with Roslyn's semantic model.
- Running a script with `t` threads costs `ram * t`. It must fit in the target server's free RAM.
- Scripts run as processes (PID, name, args, threads, host) and can be listed and killed.

### 3.5.1 Script shape

A script is a sequence of top-level statements. `nb` (the game API) and `args` (the script's arguments) are in scope without being declared:

```csharp
var target = args[0];
while (true)
{
    if (nb.GetServerSecurityLevel(target) > nb.GetServerMinSecurityLevel(target) + 5)
        await nb.Weaken(target);
    else if (nb.GetServerMoneyAvailable(target) < nb.GetServerMaxMoney(target) * 0.75)
        await nb.Grow(target);
    else
        await nb.Hack(target);
}
```

`using` lines go at the top, and type declarations can follow the statements. The compiler wraps the whole file in a generated class that implements `IScript` (`Task Run(INullByteApi nb, string[] args)`), so players never write an entry point. This is regular C# compiled with a wrapper, not Roslyn's script mode. A few default `using`s (`System`, `System.Collections.Generic`, `System.Linq`, `System.Threading`, `System.Threading.Tasks`) are added for the player, and diagnostics are mapped back to the player's own line numbers.

## 4. Technical architecture

### 4.1 Solution layout

- `NullByte.Core`: simulation, formulas, model, clock, scheduler, NB API. No console or UI references.
- `NullByte.Scripting`: Roslyn compile, analysis (RAM, banned APIs), loading and process management, and the language service (completion, hover, signature help) that editors use.
- `NullByte.Editor`: optional VS Code integration on the host side: a websocket server, a small protocol and the file service (see 4.8). References Core and Scripting.
- `NullByte.vscode`: the VS Code extension (TypeScript), built and packaged with npm rather than as part of the .NET solution.
- `NullByte.ConsoleUI` Host: the first host, with command parser, rendering and input.
- `NullByte` Host (later): WPF host with AvalonEdit, reusing Core and Scripting unchanged.
- `NullByte.Tests`: unit tests, with a fake clock and seeded RNG.

Target framework: .NET 10 with the matching C# language version, nullable reference types enabled.

Hosts talk to the game through an interface (`IGameHost` or similar) for output, input prompts and notifications, so console and WPF are interchangeable.

### 4.2 Single-threaded simulation

All game state is read and written only on the game thread.

- A custom `GameSynchronizationContext : SynchronizationContext` holds a queue of posted callbacks. `Post` enqueues; the game loop drains the queue on its thread.
- The game loop runs on a dedicated thread (or the main thread in the console host), repeatedly: advance game time, fire due timers, drain the posted queue, render if needed, sleep briefly.
- `SynchronizationContext.SetSynchronizationContext` is set on that thread, so `await` in scripts and in console commands resumes on the game thread.
- Other threads (console `ReadLine`, Roslyn compilation if we want it off-thread) never touch game state; they `Post` results back.

&#91;embedded content: one tick of the game loop · 3 stages, 4 components\]

A timer completed in stage 2 posts its continuation to the mailbox, and stage 3 of the same tick runs it. Other threads can only add to the mailbox, never run game code themselves.

### 4.3 Game time and timers

- `IGameClock` provides game time. Production uses a fixed time step driven by real elapsed time; tests use a manually advanced clock.
- A scheduler (priority queue) holds timers keyed by game time. `nb.Hack`, `nb.Grow`, `nb.Weaken` and `nb.Sleep` create a `TaskCompletionSource`, schedule a timer, and complete it when due. Continuations then run through the synchronization context, on the game thread.
- This keeps long operations cheap and makes time controllable (speed-up, pause, fast tests).

### 4.4 Console blocking behavior

- Manual `hack`, `grow` and `weaken` start the same action as a script would, with a progress indicator.
- While it runs, the console does not accept the next command, and anything typed in the meantime is discarded. Implementation: the command dispatcher awaits the action's task before printing the next prompt and before consuming the next queued input line.
- **The simulation does not pause.** Background scripts and botnet keep running while the player is blocked. Output from other sources is printed above or after the progress line and must not corrupt the prompt.
- Optional: Ctrl+C cancels the manual action through a `CancellationToken` (decide in M3).

### 4.5 Roslyn pipeline

1. Parse the player's source into a `SyntaxTree`. Syntax errors are reported against the player's own text, so line numbers are right.
2. Wrap the source: a generator splits it into `using` lines, top-level statements and type declarations, and emits a class implementing `IScript` around them. `#line` directives make every later diagnostic point at the player's lines. The default global `using`s go into a second syntax tree.
3. Create a `CSharpCompilation` with a curated list of metadata references (an allow-list of core runtime assemblies we allow, plus `NullByte.Core` API types).
4. Run analysis: the RAM cost calculation using the semantic model and the `[RamCost]` attributes. Banned API usage (file, network, process, reflection, threading, `Environment`, `Assembly` loading) and loop checks arrive with M9; the entry point is supplied by the wrapper, so it is not checked.
5. Emit to an in-memory assembly (with a portable PDB) and load it into a collectible `AssemblyLoadContext`, so recompiling or killing a script lets it be unloaded.
6. Instantiate the generated class and run `Run(nb, args)` as a process on the game thread.
7. Report diagnostics (line, column, severity, message) to the host as game output. Editors get the same diagnostics from the same compiler (see 4.8).

Compile results are cached by source hash.

### 4.6 NB API (first set)

Each method has a RAM cost declared with an attribute (for example `[RamCost(0.1)]`) so analysis can find it.

| Group | Methods |
| --- | --- |
| Actions | `Hack`, `Grow`, `Weaken`, `Sleep` |
| Info | `GetServerMoneyAvailable`, `GetServerMaxMoney`, `GetServerSecurityLevel`, `GetServerMinSecurityLevel`, `GetServerRequiredHackingLevel`, `GetHackingLevel`, `GetServerMaxRam`, `GetServerUsedRam`, `HasRootAccess` |
| Network | `Scan`, `Nuke` |
| Scripts | `Run`, `Exec`, `Kill`, `Ps`, `Scp`, `ScriptRunning`, `GetScriptRam` |
| Files | `Files.List`, `Files.Delete`, `Files.Move`, `Files.Copy` (paths relative to the server root; `List` returns every file on the server) |
| Botnet | `Botnet.NumNodes`, `PurchaseNode`, `UpgradeLevel`, `UpgradeRam`, `UpgradeCore`, cost getters |
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
| A web page connects to the localhost websocket and reads or overwrites scripts | Loopback only, handshakes with an `Origin` header rejected, a fresh per-run token kept in a file only the user can read |
| Roslyn cold start (several seconds) makes the editor feel stuck or starves file operations | Background warm-up of every language feature at startup, a concurrency gate for language requests, and file operations that never wait on it |
| A path that is both a file and a folder, or paths that grow without bound | One namespace per server enforced by `VirtualFileSystem` (a path is a file or a folder, never both), with limits of 8 levels and 256 characters |

### 4.8 VS Code integration

Optional, started with `--vscode`. It adds one host-side project (`NullByte.Editor`) and a separate TypeScript extension (`NullByte.vscode`). Core and the hosts are otherwise unaffected.

```
VS Code (extension)                       NullByte process
  FileSystemProvider (nullbyte:)   <--->  NullByte.Editor (Kestrel, websocket, JSON)
  language providers                         |  GameThread.InvokeAsync
  status bar, commands                       v  VirtualFileSystem, Player, Network
                                          Language service (Roslyn, off the game thread)
```

- **Protocol**: JSON text frames. Requests carry an `id` and an `op` (`stat`, `readDirectory`, `readFile`, `writeFile`, `createDirectory`, `delete`, `rename`, `diagnose`, `complete`, `hover`, `signatureHelp`). `writeFile` and `rename` take `create` and `overwrite` flags, and `delete` takes a `recursive` flag. `stat` returns the type, the size in bytes and, for files, the creation and modification time in milliseconds since the Unix epoch. Responses echo the `id` with `ok` and either a `result` or an `error` code (`NotFound`, `FileExists`, `IsADirectory`, `NotADirectory`, `NoPermissions`, `BadRequest`, `Unavailable`). The server also pushes events without an id: `fileChanged` (created, changed or deleted, with a path and whether it is a folder) and `reset` (the running game was replaced, for example by `load`).
- **Paths**: `/<server>/<path>` on the wire, where the path inside the server follows the rules in 4.9. The root lists the servers the player has discovered; undiscovered servers answer "not found". Servers cannot be created, renamed or deleted from the editor. In VS Code the game appears as a single root folder named `NullByte`; the extension maps `nullbyte:/NullByte/<server>/<path>` to the wire path. Folders can be created, renamed, dragged and deleted (recursively when VS Code asks for it). Replacing something that exists is only allowed for the same kind (file for file, folder for folder), and nothing can be moved between servers.
- **Threading**: file operations are marshalled onto the game thread with `GameThread.InvokeAsync`, which keeps the rule from 4.2. `VirtualFileSystem` and `Player` raise change events on the game thread; each connection has an outbox so only one writer sends at a time. Language requests never touch game state, so they run on the thread pool behind a small concurrency gate and cannot starve file operations.
- **Conflicts**: `stat` reports the file times from 4.9, which enables VS Code's own save check: saving a script that is newer than the version the editor loaded, and has a different size, fails with Compare and Overwrite instead of silently replacing the game's version. Changes that keep the exact byte length are not detected by that check.
- **Language service**: reuses the compiler's source wrapper, default usings and whitelisted references, so suggestions match what actually compiles. Positions are mapped between the player's text and the wrapped source. Documentation comes from the XML docs of `NullByte.Core` and RAM costs from the `[RamCost]` attributes; framework members have signatures only. A background warm-up at startup pays the Roslyn start-up cost before the first request.
- **Discovery and security**: the server listens on loopback only. At startup it writes a connection file with the `url`, a fresh random `token` and the `pid`, readable only by the user, and removes it on exit if it is still its own. The extension reads the file on every connection attempt. Handshakes with an `Origin` header (browsers) or without the bearer token are rejected. As with the sandbox, this is hobby-level protection for a local tool, not a security boundary.
- **Arguments**: `--vscode` starts the integration. `--vscode-port <n>` sets the port; without it the game tries 8765 and falls back to a free port. `--vscode-file <path>` sets where the connection file is written (default `~/.nullbyte/editor.json`).
- **Scripts**: the extension registers `.cx` as a language with C# highlighting. Scripts outside `.cx` files get no language features.

### 4.9 Virtual file system

Each server has its own files and folders, held in memory and saved with the game. `VirtualFileSystem` owns the rules; the console commands, the script API and the editor server all go through it.

- **Paths**: `/`-separated and relative to the server root, for example `tools/net/scan.cx`. A leading `/` is accepted and removed, and so is one trailing `/` on a folder. Names are case-sensitive. A segment cannot be empty, `.` or `..`, or contain a backslash or control characters. A path is at most 8 levels deep and 256 characters long. `VirtualPath` normalizes paths, so `/tools/a.cx` and `tools/a.cx` are the same file everywhere, including process names and `ScriptRunning`.
- **Model**: a path is a file or a folder, never both. Folders can be empty and survive deleting their last file. Every folder above a file exists. The root always exists.
- **Operations**: writing, moving and copying create missing parent folders, like `mkdir -p`. Deleting a folder with content needs an explicit recursive flag. Moving a folder moves everything below it and keeps the files' times; a folder cannot be moved into itself, onto something that exists, or below a file. A failed operation changes nothing.
- **File times**: every file has a creation and a modification time, taken from an injected `TimeProvider` (wall-clock time in whole milliseconds, not game time). The modification time is strictly increasing per file, so two quick writes never look identical, and moving a file keeps both times. The times are saved with the game.
- **Events**: `VirtualFileSystem.Changed` is raised on the game thread with the kind (created, changed, deleted), the path and whether it is a folder. Folders created implicitly are reported outermost first, before the file. Deleting a folder is one event however much was below it, and moving is a delete plus a create. Restoring from a save raises no events.
- **Console**: `ls [directory]`, `cat`, `edit`, `rm [-r]`, `mkdir`, `rmdir`, `mv` (files and folders), `scp` (files only), `cd` and `pwd`. A bad path produces a one-line explanation instead of an error.
- **Current directory**: the player has a current directory on the current server, shown in the prompt (`root@home:/tools $`) and by `pwd`. Console commands resolve the paths they are given against it: a path starting with `/` is absolute from the server root, anything else is relative, and `.` and `..` are understood there (the file system itself never sees them, and `..` at the root stays at the root). This covers the file commands, `run` and `mem`; `scp` keeps a file's full path on the destination server. Error messages echo what was typed, results show the full path, and `ps` always shows full paths. The current directory goes back to the root when the player connects to another server, and it is not saved. If the directory is deleted (by a command, a script or the editor), the next command says so and puts the player back at the root. The editor and scripts never use the current directory: `nb.Files`, `nb.Run` and `nb.Exec` take paths from the server root.
- **Tab completion**: completes one path segment at a time, relative to the current directory (from the root after a leading `/`, and through `.` and `..`). Directories end in a slash and add no space, so a path can be built up in steps. `cd`, `ls`, `mkdir` and `rmdir` offer only directories, commands that take files offer files and directories, and `scp` also offers known servers. The completion data is refreshed after each command.
- **Scripts**: `nb.Files.List` returns every file on the server as a full path; `Delete`, `Move` and `Copy` take paths from the server root, and so do `nb.Run` and `nb.Exec`.
- **Saves**: format 3 stores every folder per server (empty ones included) next to the files, whose names are paths. Older saves load through migrations (1 to 2 adds file times, 2 to 3 adds an empty folder list), and a save with an invalid folder entry is refused as damaged data. The player's current directory is not saved: a loaded game starts at the root of the current server.

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

### M3.5: Console shell QoL improvements

**Deliverables**

- Autocomplete for server names and commands in the console
- History for input in the console

**Acceptance**

- The user can tab to autocomplete server names commands
- The user can use up/down arrows to cycle through input history

### M4: Botnet (called Hacknet in bitburner)

**Deliverables**

- Botnet model, cost formulas, production per tick, node purchase and upgrades.
- Console commands: `botnet`, `botnet buy`, `botnet upgrade <n> level|ram|core`, `botnet status`.

**Acceptance**

- Production matches the formula in tests over simulated time.
- A new game can reach several nodes using only hand-hack money plus Botnet income, so the early loop works before scripting exists.

### M5: Roslyn scripting, compile and run

**Deliverables**

- Virtual file system with `ls`, `cat`, `rm`, `edit` (console editor stub: paste or external editor reload).
- Compile pipeline from 4.5 minus RAM analysis and sandbox hardening.
- `run <script> [args]` executes the script on the game thread; diagnostics are printed.
- A first `nb` surface: `Print`, `Sleep`, `Args`, and the three actions.

**Acceptance**

- The example script from 3.5.1 runs against a server and increases the player's money over time.
- Compile errors show line and column.
- Recompiling a changed script unloads the old assembly (verify with a weak reference test).

### M6: Process manager, threads and RAM

**Deliverables**

- Processes with PID, host, threads and cancellation.
- Static RAM analysis from `[RamCost]` attributes; RAM enforcement on `run` and `exec`.
- `ps`, `kill`, `killall`, `free`, `mem <script>`, `scp` and `exec` for running on other rooted servers.
- Full first NB set from 4.6.

**Acceptance**

- Running with too little free RAM fails with a clear message.
- `kill` stops a script at its next `await` and frees its RAM.
- Multi-thread runs scale effect and RAM cost correctly.

### M7: Save and load

**Deliverables**

- Serialization of player, servers, Botnet, files, running processes (restarted on load) and game time.
- Versioned save format with a migration hook.
- Save format 2 stores file creation and modification times; format 3 stores folders, including empty ones (see 4.9). Older saves load through migrations.
- Autosave and manual `save` and `load`.

**Acceptance**: save, quit, load restores an equivalent state; a golden-file test guards the format.

### M8: Visual Studio Code editor integration

Edit scripts in VS Code while the game runs. The game hosts a websocket server and a VS Code extension presents the game's servers and files as a virtual file system (design in 4.8). The integration is optional and only starts with `--vscode`.

**Deliverables**

- `NullByte.Editor`: a loopback-only, token-authenticated websocket server with a small JSON protocol and pushed events. All access to game state is marshalled onto the game thread.
- `NullByte.vscode`: a TypeScript extension with a `FileSystemProvider` for the `nullbyte:` scheme (one folder per discovered server), a `.cx` language definition, the commands `Open Game Workspace`, `Reconnect` and `Show Log`, a status bar indicator showing the connection state, and a `.vsix` package.
- File and folder operations: list, read, write (create and overwrite), create folder, delete and rename. Changes made in the game (`edit`, `scp`, `rm`, `mkdir`, `mv`) and newly discovered servers are pushed to VS Code as events.
- Only discovered servers are visible to the editor.
- File creation and modification times, so VS Code can detect that a script changed in the game after it was opened.
- Folders in the game and in VS Code (see 4.9): `mkdir`, `rmdir`, `rm -r`, `ls [directory]`, moving folders with `mv`, `cd` and `pwd` with a current directory and relative paths, and tab completion that follows it; in the explorer New Folder, rename, drag and drop and recursive delete, with live updates in both directions.
- Language features for `.cx` scripts, backed by the same Roslyn setup as the compiler: compile errors and warnings in the Problems panel, completion, hover and signature help, with documentation and the RAM cost of each API method. Suggestions follow the compiler's reference whitelist.
- Startup and discovery: `--vscode`, `--vscode-port <n>` and `--vscode-file <path>` (see 4.8), a connection file instead of fixed ports and tokens, and the extension setting `nullbyte.connectionFile`.
- Script crashes are caught, logged and do not take down the game loop (already in place).

**Acceptance**

- A script can be edited in VS Code and executed in the game, and changes made in the game show up in VS Code without a refresh.
- Saving a script in VS Code after it was replaced in the game with content of a different length offers Compare and Overwrite instead of silently overwriting it.
- A folder created, moved or removed in the game shows up in VS Code without a refresh, and the other way around. An empty folder survives save and load.
- Without `--vscode` the game opens no port and writes no connection file.
- A second game started with `--vscode` does not break the first: it falls back to a free port, or reports that it could not start.
- The packaged extension installs in a clean VS Code profile and connects without any configuration.

### M9: #includes

**Deliverables**

- Include other scripts with `#include "<file>"`.
- Full vs code support for included scripts

**Acceptance**

- scripts can include other scripts with the `#include "<file>"` statement

### M10: Visual studio Code editor integration - improvements

**Deliverables**

- Debug command to see if any (external) editors is connected
- full-range underlines for diagnostics,
- Run from VS Code: a command, or a run operation that executes the current file.
- Get ram from VS Code: a command, or a ram operation that gets the ram cost of the current file.
- launch vs-code from the game
   
**Acceptance**

- Updated vs code extension

### M11: Sandbox and safety

**Deliverables**

- Allow-listed references and banned API analysis.
- Guard against synchronous infinite loops (analyzer rule plus an investigated runtime guard).

**Acceptance**

- A set of malicious or buggy sample scripts (file access, reflection, `while(true){}`, thrown exceptions) are rejected or contained, and the game continues to run.

### M12: Progression and balance pass

**Deliverables**

- Tuned network, requirements and payouts so that a first playthrough has a clear arc: manual hack, Botnet, first script, script on several servers, home RAM upgrades.
- Home RAM upgrade purchase (and optionally purchased servers).
- Botnet and hack economy balanced so neither dominates.
- Short in-game tutorial via `help` and a few sample scripts shipped in the starting file system.

**Acceptance**: a play session of roughly 1 to 2 hours reaches the final in-scope tier of servers, with a script-based economy clearly better than manual play.

### M13: First playable release

**Deliverables**: bug fixing, README, sample scripts, a short "how to play" document, a tagged build.

**Acceptance**: someone other than the author can install, play through the first arc and write a working script using only the in-game help and README.

## 6. After the first playable

- **WPF host**: terminal control, script editor with AvalonEdit, Roslyn-powered completion and diagnostics (squiggles, reusing the language service from M8), file browser, status panels. Core and Scripting stay untouched; only a new `IGameHost` implementation is needed.
- **VS Code integration follow-ups**:
  - Full-range underlines for diagnostics (needs end positions on `ScriptDiagnostic`).
  - A `run` command in VS Code that runs the current script and shows its output.
  - A `ram` command in VS Code that checks the ram cost for the current script.
  - Opening vs code from within the game
- **Console follow-ups**: `scp -r` for copying folders, a recursive `ls`, and refreshing tab completion when files change outside the console (scripts, the editor).
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
- File system tests: path rules, folder operations, change events, file times and restoring, with a manual clock.
- Save tests: round trips including empty folders, migrations from every older version, damaged saves refused, and a golden file that guards the format.
- Command tests through the real dispatcher with a recording terminal, including relative paths and the current directory.
- Path resolution and tab completion tests: the resolver, the current directory state (including its resets), and the completion engine against hand-made snapshots.
- Editor protocol tests that send requests through the file service against a real game state.

## 8. Open questions (resolved)

1. **Money type**: `double`, with formulas isolated so it can change. Decided.
2. **Script surface**: top-level statements with implicit `nb` and `args`, wrapped by the compiler into a generated class (changed from the original plan of a class with `Main`). Decided.
3. **Typing during a blocking command**: the input is discarded, not buffered. Decided.
4. **Real-time versus game-time ratio**: 1 to 1 initially, with a speed multiplier in the config. Decided.
5. **Target framework**: .NET 10. Decided.
6. **Network authoring format**: a JSON data file. Decided.