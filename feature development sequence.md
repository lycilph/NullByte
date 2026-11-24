# New Feature Development Sequence

Use this sequence when implementing a meaningful new feature.

The goal is to provide enough context for accurate architectural and implementation decisions without loading the entire repository.

---

## Phase 1: Define the feature

### Context to provide

Attach:

- `AI_CONTEXT.md`
- `CURRENT_STATUS.md`
- `DESIGN.md` only if the feature introduces or changes player-facing behavior

Do not attach the full codebase yet.

### Prompt

You are an expert C#/.NET developer and software architect helping me develop NullByte.

Read the attached project context.

I would like to implement:

**[FEATURE]**

The intended outcome is:

**[OUTCOME]**

Known requirements:

- [requirement]
- [requirement]
- [requirement]

Before discussing implementation, help me settle the feature requirements.

Identify:

1. unclear or missing behavioral decisions,
2. important edge cases,
3. interactions with existing features,
4. decisions that materially affect the architecture.

Give your recommendation for each unresolved point.

Do not design exact classes or APIs yet. We should settle behavior first.

---

## Phase 2: Identify required source

Use this after the requirements are settled.

### Additional context

Attach:

- `CODEBASE.md`

Retain:

- `AI_CONTEXT.md`
- `CURRENT_STATUS.md`

`DESIGN.md` can be dropped unless still needed.

### Prompt

The requirements for **[FEATURE]** are now settled.

Using `CODEBASE.md` and the project context, identify the smallest useful set of current source files you need to inspect before designing the implementation.

Separate them into:

### Required
Files needed to understand the existing implementation and propose a reliable design.

### Relevant tests
Tests that establish existing behavior or conventions.

### Possibly required
Files that may be needed depending on what the primary files reveal.

For each file, give a one-sentence reason.

Do not design the feature yet.

Keep the initial source set small. We can expand it after inspecting dependencies.

---

## Phase 3: Architecture review

Attach the current versions of the requested source and test files.

If this is an architectural change, also attach:

- `ARCHITECTURE.md`

### Prompt

Here are the current implementation files you requested.

Review the actual implementation before proposing changes.

For **[FEATURE]**, describe:

1. how the relevant system works today,
2. which existing concepts should be reused,
3. where the new responsibility should live,
4. the proposed target architecture,
5. files/types that should be added or changed,
6. important threading/lifecycle/persistence concerns,
7. alternatives you considered and why you do not recommend them,
8. any additional source files you now need.

Prefer the smallest architecture that fits the existing codebase.

Do not start implementation yet.

If the code contradicts our context documents, call that out explicitly.

---

## Phase 4: Implementation plan

Use after agreeing on the architecture.

### Prompt

The target architecture is agreed.

Create an incremental implementation plan for **[FEATURE]**.

Break it into small steps that compile or can be validated independently where practical.

For each step specify:

- goal,
- files to add/change,
- responsibility being introduced,
- tests to add/update,
- expected validation.

Order the steps so foundational changes come before consumers.

Do not provide the implementation yet.

I will ask you to proceed one step at a time.

---

## Phase 5: Implement incrementally

Use this for each implementation step.

### Prompt

Proceed with **Step [N]: [STEP NAME]**.

Use the current source already provided.

For each changed file:

1. explain what is changing and why,
2. provide the exact code change,
3. preserve existing naming and architectural conventions,
4. identify any tests that should be added or changed.

Do not proceed to the next implementation step.

If the current source does not contain enough information to implement this accurately, tell me exactly which additional file is needed rather than guessing its API.

---

## Phase 6: Tests

Usually perform testing alongside each implementation step.

After the implementation is complete, perform a dedicated test pass.

### Prompt

The feature now compiles.

Review the completed **[FEATURE]** implementation from a testing perspective.

Identify the smallest meaningful test matrix covering:

- happy path,
- important boundary conditions,
- failure behavior,
- regression risks,
- relevant architectural invariants.

Prefer xUnit and existing test helpers. Use NSubstitute only where it makes the test clearer.

Then provide the tests that are still missing.

Avoid duplicating coverage already provided by existing tests.

---

## Phase 7: Final implementation review

Provide any modified files that have changed substantially since they were last shown.

### Prompt

The implementation is complete and all tests are green.

Perform a final review of **[FEATURE]** against the agreed requirements and architecture.

Review specifically for:

1. requirement completeness,
2. edge cases,
3. game-thread/thread-safety correctness,
4. lifecycle/resource ownership,
5. duplicated responsibilities,
6. unnecessary abstractions,
7. persistence implications,
8. scripting implications,
9. editor/protocol implications,
10. missing regression tests.

Separate findings into:

### Must fix
Correctness or architectural problems.

### Should consider
Meaningful improvements that are not blockers.

### Looks good
Important areas that are correctly implemented.

Do not propose unrelated refactoring.

---

## Phase 8: Documentation update

After the implementation and tests are accepted:

### Prompt

The feature is complete and tests are green.

Determine which NullByte context documents need updating:

- `CURRENT_STATUS.md`
- `CODEBASE.md`
- `ARCHITECTURE.md`
- `DESIGN.md`
- `AI_CONTEXT.md`

Apply this rule:

- `CURRENT_STATUS.md`: update progress, completed work, current focus and next step.
- `CODEBASE.md`: update only if important files, entry points or subsystem ownership changed.
- `ARCHITECTURE.md`: update only if architectural relationships, boundaries or lifecycle changed.
- `DESIGN.md`: update only if player-facing behavior, product decisions or roadmap changed.
- `AI_CONTEXT.md`: update only if a development invariant or important AI guidance changed.

First tell me which documents require changes and why.

Then provide the updated sections only, unless I explicitly request the complete documents.

Keep the context files compact and within their established token budgets.

---

# Context-loading rules

Do not attach every context document to every phase.

Use approximately:

| Situation | Context |
|---|---|
| Routine implementation | `AI_CONTEXT` + `CURRENT_STATUS` + relevant source/tests |
| Finding source | + `CODEBASE` |
| Architectural change | + `ARCHITECTURE` |
| Game/design decision | + `DESIGN` |
| Cross-cutting feature | all relevant context files, selectively |

Always provide the current version of a source file before modifying it.

Prefer adding one newly discovered dependency over loading an entire project.

---

# Feature completion criteria

A feature is complete when:

- requirements are resolved,
- architecture is agreed,
- implementation is complete,
- relevant tests are green,
- important edge cases are handled,
- final review has no unresolved must-fix findings,
- relevant context documents are updated.

Do not treat "the code compiles" as feature completion.