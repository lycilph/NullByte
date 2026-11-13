I'll start by reading the design document and the repository dump.

I'm partway through the review: I've read the design doc and the Core project, and next I'm reading the rest of the GameLoop, the Scripting project and the ConsoleUI.

Still reviewing: I've finished the terminal and command layer, and I'm now reading tab completion, the hacking formulas and the tests.

Nearly done reading: the tests for the `run` command look out of sync with the implementation, so I'm doing one last check before writing up the review.

I read the design doc and all production code in the packed repo, and skimmed the tests. I didn't build or run anything, so the points about failing tests come from reading the code.

## Verdict

The foundation is solid and M0–M5 are all there. The single-thread model is well executed: `GameSynchronizationContext` with `DrainBatch` stops a script that keeps yielding from starving the loop. The scheduler uses deterministic ordering. The formulas look faithful to Bitburner as I remember it. The collectible `AssemblyLoadContext` has weak-reference unload tests, and the `#line` mapping gives correct line numbers for compile errors.

Your "convoluted" feeling is accurate. Most of it comes from a few causes: repeated structure, a service-locator context, and leftover scaffolding. It isn't an architecture problem. Fixing it should remove several hundred lines without changing behaviour.

## Bugs and things to verify first

- ~~**`RunCommandTests` looks out of sync with the code.** It asserts `"Started hello.cs."` and `"Usage: run <script> [args...]"`, but the code prints `"Executing {file}..."` and declares `run <filename> [args...]`. Run `dotnet test`. Either those tests fail or the snapshot is stale.~~
- ~~**The progress bar can get stuck.** In the hack/grow/weaken handlers, `StopProgress()` is not in a `finally`. If the action throws or is cancelled, the bar stays up.~~
- **Duration is computed twice.** The console computes it for the progress bar and `ServerActions` computes it again. They can diverge. A refused hack, for example when you have no root, also briefly shows a progress bar for something that never ran.
- ~~**Dead code in `GameLoop.cs`.** 414 of its 731 lines are the commented-out previous implementation. Delete it. There's also a stale `<see cref="GameSimulation...">` and a malformed `<Gets` XML comment in `GameState`.~~
- ~~**`HackAsync` writes `player.Money += ...` directly.** That bypasses the validated `AddMoney`. Make the setter private.~~
- ~~**Please double-check against Bitburner:** security increases on a failed hack, and XP on failure. I believe Bitburner only fortifies on success and gives reduced XP on failure, but I'm not certain. The doc already says to verify these in M2.~~
- ~~**The RNG seed is fixed at 1337 in production**, so every new game has identical hack luck. Use a time-based seed outside tests.~~
- ~~**Scripts compile on the game thread.** First-compile latency freezes the whole simulation. The handler is already async, so `await Task.Run(() => compile)` is a small change, and the continuation resumes on the game thread. Also warm the compiler at startup.~~
- ~~**Inconsistent extensions:** the debug scripts are `.cx` and the tests use `.cs`.~~

## Where the complexity comes from, and how to cut it

### 1. `CommandContext` is a service locator, and `CommandRegistryFactory` is 954 lines
Every handler can reach 11 services, and registration, implementations and debug scaffolding all live in one file.
- Split into small groups (`NetworkCommands`, `FileCommands`, `BotnetCommands`, `ScriptCommands`, `ActionCommands`). Each takes only what it needs through its constructor and has a `Register(registry)` method.
- Delete the `[M1]` demo, `thread` and `time` commands, and the `scripts` command once you have the host-folder sync. Moving them to tests also removes the `Debug` flag and the special case for the name `"debug"` in `CompletionCatalog`.
- Replace `ArgumentsCountCheck = c => ...` lambdas with `MinArgs` and `MaxArgs`.
- `exit` is registered with a no-op handler and also special-cased in `Program` with its own parsing. Pick one.

### 2. Hack, grow and weaken are written three times at every layer
That's three handlers of about 35 lines, three result records, and three near-identical `ServerActions` methods.
- Merge the three results into one `ActionResult(Kind, Succeeded, FailureMessage, MoneyDelta, SecurityDelta, Xp, Duration)`.
- Give `ServerActions` one private pipeline (resolve, check root, duration, delay, apply, XP) with the effect passed in as a delegate.
- Make the start of an action return a handle, so the console never recomputes the duration:

```csharp
var action = context.ServerActions.Begin(ActionKind.Hack, host, threads: 1, ct);
terminal.StartProgress($"Hacking {host}", () => action.Progress);   // Progress derived from the clock and action.Duration
var result = await action.Task;
```

One `RunManualAction(...)` helper then replaces the three handlers.

### 3. Input blocking belongs in the command loop, not in three handlers
The loop in `Program` should disable input before dispatching every command and re-enable it after. That removes `RunWithInputBlockedAsync` entirely.

It also fixes a small race. `SetInputEnabled(false)` goes through a channel and applies on the next UI tick. A line submitted in that 10–30 ms window gets queued and runs after the hack, which violates "input is discarded". Disabling input synchronously on Enter closes the gap.

### 4. The composition root is in the console host
`Program.Main` wires about 25 objects by hand, and `CommandContext` repeats 11 of them. The doc says a WPF host should reuse Core and Scripting unchanged, so the wiring should be reusable too.
- Add a `GameSession` (GameState, loop, scheduler, services) that both hosts create.
- The doc's `IGameHost` doesn't exist yet. Today there is a console-specific `IGameTerminal`, plus `IScriptOutput` and a `TerminalScriptOutput` adapter that only forwards `WriteLine`. Introduce one `IGameOutput` (`WriteLine`) in Core. Scripts and commands both write to it, and the adapter goes away. Terminal-only features stay in ConsoleUI.
- Construct `GameScheduler` first and pass it to `GameLoop`. `ServerActions` and `NullByteApi` then depend on the scheduler, not the loop. `GameLoop` is currently used as a service provider.
- `context.GameLoop` is only used for `InvokeAsync` and `IsGameThread`. A tiny `IGameThread` interface covers that.
- Prompt state lives in two places: `Player.CurrentHostname`, and `SetPrompt` called by hand in `connect`, in `home`, and hardcoded in `Program`. After each command, set the prompt and refresh completion from one place. Both already need to happen there.

### 5. The terminal mixes three jobs in 700 lines
`ThreadSafeConsoleTerminal` contains the key-handling state machine, the renderer and the cross-thread marshalling (seven request records). Because key handling is welded to `Console.KeyAvailable`, none of it can be tested, including the "discarded input" acceptance criterion.
- Extract a pure `LineEditor` that owns the buffer, cursor, history and completion, with `HandleKey(ConsoleKeyInfo)`.
- Keep the terminal as renderer plus marshalling, which brings it to roughly 250 lines.
- You already test `InputHistory` and `CompletionEngine` in isolation, so this follows the same pattern.

### 6. Tab completion spans nine types
`ArgumentCompletion`, `CompletionCondition`, `CompletionKind`, `CompletionSnapshot`, `CommandCompletionInfo`, `CompletionResult`, `ICompletionProvider`, `CompletionCatalog` and `CompletionEngine`.
- The immutable snapshot is the right idea, because it keeps game state off the terminal thread.
- `CompletionCondition` and `When` exist only for `botnet upgrade`. If `botnet status`, `botnet buy` and `botnet upgrade` become registered subcommands, both disappear. You'd also stop encoding the grammar twice, once in the hand-written `switch` and once in the completion metadata.
- `CommandCompletionInfo` just mirrors `CommandDefinition`, and the registry is immutable after startup. Read the registry directly and snapshot only the dynamic values.

### 7. Result types and guard clauses
- There are seven hand-rolled result types with Success/Failure factories, and some user errors are thrown as exceptions (`NetworkService.GetServer` throws, and `Program` catches it as "Command failed"). Pick one convention: results for expected failures, exceptions for bugs. A small generic `Result<T>` would do.
- There are about 90 `ThrowIfNull` or `?? throw new ArgumentNullException` calls with `<Nullable>enable</Nullable>` already on. Keep validation at real boundaries (JSON loading, user input, domain invariants) and drop the rest. Primary constructors help here.
- `Server` has a 10-parameter constructor, called 20 times in tests. You already have `ServerDefinition`, so build `Server` from it, and give the tests a builder with defaults.

### 8. The scripting project: 13 types for about 600 lines
- `ScriptAssemblyLoader`, `ScriptAssembly` and `ScriptLoadContext` collapse into one disposable `LoadedScript`.
- `ScriptService` takes `GameState` only to read a file. Pass the source in instead. M6 replaces it with a process manager anyway, so don't invest more in it now.
- `ScriptCompleted` is never wired, so there's no message when a script finishes normally. `OperationCanceledException` is swallowed silently.

### 9. Smaller items
- `IGameClock` exposes `Start()` and `Stop()`, and `FakeGameClock` implements both as empty methods. Keep lifecycle off the read interface.
- Six test files each define their own copy of `TestNullByteApi`, `TestScriptOutput` or `FixedRandomSource`. Create one shared test-support project.
- `HackingFormulasTests` is 1,105 lines and is probably a `[Theory]`/`InlineData` candidate (I didn't read it closely).
- `NullByteApi` uses `_field` naming while the rest of the code doesn't.

## Decide this before M6/M7

Referencing `System.Private.CoreLib.dll` exposes `File`, `Environment`, reflection and `Thread`, because they all live in CoreLib. The "allow-listed assemblies" idea from the doc can't work at assembly level. You also reference all of `NullByte.Core`, so a script can reflect into private fields and reach the whole `GameState`.

Two fixes:
- Compile against reference assemblies (the `Microsoft.NETCore.App.Ref` pack or a `ReferenceAssemblies` NuGet package) plus a symbol-level ban list.
- Put `INullByteApi` and its result types in a small contracts assembly, so scripts reference only that.

It's cheap now and expensive after M6.

For M6, give each process its own `INullByteApi` instance carrying its `CancellationToken`, host and thread count. Then `kill` is a token cancel and scripts don't pass tokens around. `Server.AllocateRam` and `ReleaseRam` already exist.

## Suggested order

1. Delete the dead code, run `dotnet test` and fix the stale `run` tests.
2. Unify the action result and add `Begin(...)` on `ServerActions`. Collapse the three handlers and move input blocking into the loop.
3. Add `GameSession` and `IGameOutput`, and break `CommandContext` into command groups.
4. Extract `LineEditor` and test the discard behaviour.
5. Add the host-folder script sync and move compilation off the game thread.
6. Make the sandbox-reference decision, then start M6.

If you want, I can sketch the refactored `ServerActions` and command-group layout in code, since that's the biggest simplification.