# NullByte Architecture Decisions

This is a lightweight record of decisions that are important enough to preserve. It is intentionally not a formal ADR system.

The rules themselves are stated in `ARCHITECTURE.md`; this file records why they exist.

## 1. Core owns gameplay rules

**Decision:** Gameplay rules live in `NullByte.Core`, primarily in domain services, formulas and systems.

**Why:** Console commands, scripts and the external editor must behave consistently. Duplicating rules in adapters makes balance changes and bug fixes unreliable.

**Consequence:** New interfaces should normally adapt existing Core behavior rather than implement it again.

## 2. Mutable game state has one owner

**Decision:** The game thread owns mutable gameplay state.

**Why:** A single ownership model is simpler and more deterministic than locking arbitrary parts of the domain.

**Consequence:** External requests must cross the game-thread boundary. Do not introduce locks merely to make concurrent access appear safe.

## 3. Game time is scheduler-controlled

**Decision:** Long-running gameplay uses the game clock and scheduler.

**Why:** Real-time waits block threads and make tests slow and nondeterministic.

**Consequence:** Use `IGameClock`, `FakeGameClock` and `GameScheduler` instead of `Thread.Sleep` or synchronous waits for gameplay timing.

## 4. Activities share one lifecycle

**Decision:** Study/training, jobs and crimes use the same activity lifecycle, including focused/background modes.

**Why:** They are variations of the same player-facing concept: an activity consumes time and eventually resolves.

**Consequence:** New comparable activities should reuse the existing model, persistence and scheduling.

## 5. Script API delegates to Core

**Decision:** The C# scripting API is an adapter over Core services.

**Why:** Scripts should expose gameplay capabilities without becoming a second gameplay engine.

**Consequence:** If a script function needs a new rule, add the rule to Core first, then expose it through the API.

## 6. VFS is authoritative

**Decision:** Player files and folders live in the game's virtual filesystem.

**Why:** The same files must be accessible from the console, scripts and external editor.

**Consequence:** File operations should use the VFS/path abstractions rather than maintaining separate editor or console file stores.

## 7. Editor integration is optional infrastructure

**Decision:** The external editor is an adapter around the game rather than a required part of the Core runtime.

**Why:** The game must remain playable without VS Code or another external editor.

**Consequence:** Editor availability/status is exposed through a narrow interface and editor-specific code stays outside gameplay logic.

## 8. Host owns session replacement

**Decision:** Load/reset/shutdown are host-level operations represented as requests.

**Why:** Replacing the active GameSession while a command is still executing against it can cause self-disposal and stale subscriptions.

**Consequence:** Commands request lifecycle changes; GameHost performs them at a safe boundary.

## 9. Persistence uses explicit migrations

**Decision:** Save formats are versioned and migrated explicitly between versions.

**Why:** A save file is a long-lived compatibility boundary.

**Consequence:** Schema changes should add a migration and tests, rather than silently changing the interpretation of old saves.

## 10. Running scripts keep their compiled snapshot

**Decision:** A running script executes the compiled source snapshot from when it started.

**Why:** Editing a file should not mutate already-running code underneath a process.

**Consequence:** Source edits affect the next compilation/start, while current processes remain stable.

## 11. Background activities trade reward for availability

**Decision:** Background activities remain available while the console is usable, but receive a 20% reward penalty.

**Why:** This creates a clear trade-off between uninterrupted focused play and keeping the rest of the game accessible.

**Consequence:** New activity implementations should use the same distinction unless there is a deliberate design reason not to.

## 12. Project documentation stays compact

**Decision:** Maintain a small set of focused architecture/development documents rather than a generated dump of the entire codebase.

**Why:** The repository itself is the source of truth. Documentation should provide navigation, constraints and rationale without duplicating implementation details.

**Consequence:** Update documentation when structure or important decisions change, but prefer targeted source inspection for implementation details.

## 13. Facility experience bonuses apply only to paid courses

**Decision:** A course with a zero per-second cost does not receive a training facility's experience multiplier. Paid courses continue to receive it. Global class-experience balance and focused/background reward factors apply to both.

**Why:** Higher-tier facilities are a paid progression upgrade with better rewards. Applying their experience bonus to free courses would let players bypass that progression advantage without paying for the course.

**Consequence:** Enforce this rule in Core's activity-rate calculation so every client and activity entry point observes the same behavior. Free courses remain available at higher-tier facilities, but their facility experience multiplier is treated as 1.

## 14. Script execution uses a practical sandbox, not hardened isolation

**Decision:** Player scripts receive compile-time restrictions and runtime failure containment, but execute in-process without a hard execution-time limit or operating-system isolation.

**Why:** The goal is to prevent common mistakes and ordinary attempts to access the host environment without introducing the complexity and overhead of a hardened sandbox. The game is a hobby project, not a platform for executing hostile code.

**Consequence:** Keep the reference allow-list, banned-API diagnostics, blocking-call checks, cooperative-loop guard and process-failure cleanup as defense-in-depth guardrails. Document their limitations clearly. Do not describe the scripting runtime as safe for determined attackers or arbitrary untrusted code.

**Runtime guard:** The M12 investigation into an instruction-count or time-budget guard concluded that none is added. Injecting checks into player code adds overhead and complexity, and a guard that cannot interrupt arbitrary synchronous code gives a false sense of safety. The compile-time loop rule and cooperative cancellation are the accepted protection; memory and stack exhaustion are documented limitations.

## 15. The loop guard targets unbounded loops

**Decision:** `NB1009` rejects only loops that have no built-in end (`while`, `do`, and `for` without a condition or with a constant-true condition) unless they contain a real yield point. `foreach` loops and `for` loops with a condition are allowed without an `await`.

**Why:** The goal is to stop a script from freezing the single game thread. Rejecting every loop would also reject ordinary code such as `foreach (var host in nb.Scan())` and synchronous helper methods in libraries, which makes the language feel broken without making the game meaningfully safer. An `await` of an already-completed task does not yield, so it does not count.

**Consequence:** The check stays syntactic and is a guardrail, not a termination proof; its limits are listed in `ARCHITECTURE.md`. If playtesting shows that bounded-looking loops freeze the game in practice, tighten the rule rather than adding a runtime guard.

## 16. Scripts may use only the nb API from NullByte.Core

**Decision:** Scripts compile against the whole `NullByte.Core` assembly, but the security analyzer bans every Core namespace and the concrete API and service types except `NullByte.Core.Scripting`, the `INullByteApi` interfaces and the result types in `NullByte.Core.Services`.

**Why:** Splitting the script API into its own assembly would be cleaner but is a larger change than the sandbox milestone warrants. The ban list gives the same compile-time result for now.

**Consequence:** When a new Core namespace or service type is added, decide whether scripts need it and extend the banned list otherwise. Moving the script-facing API into a dedicated assembly would remove this maintenance step.

