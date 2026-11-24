# M10: `nb.Activities` Implementation Plan

## Objective

Add an Activities sub-API to the global `nb` scripting object so player-written scripts can:

- inspect the current activity;
- inspect available courses, facilities, companies, careers, and crimes;
- calculate player-relative course rates and crime outcomes;
- start studying, training, working, or committing crimes;
- move the current activity between focused and background modes;
- stop the current activity.

The API must reuse the existing `ActivityService` and activity domain. It must not duplicate activity validation, formulas, progression, rewards, employment logic, or simulation.

## Agreed API behavior

### Query operations

The API exposes:

- `GetCurrent()`
- `GetCourses()`
- `GetFacilities()`
- `GetCourseRates(...)`
- `GetCompanies()`
- `GetCrimes()`

All returned values are script-facing snapshots. No mutable activity-domain object is exposed to player scripts.

### Command operations

The API exposes:

- `Study(...)`
- `Train(...)`
- `Work(...)`
- `CommitCrime(...)`
- `SetMode(...)`
- `Stop()`

These calls synchronously request an immediate game-state transition. They do not wait for activity progression or completion.

### Lifecycle rules

- Only one activity may run at a time.
- Starting another activity while busy returns a failed result.
- The API does not automatically replace the current activity.
- Activities belong to the player session rather than the script process.
- Ending or killing the calling script does not stop the activity.
- Script thread count does not multiply activity effects.
- Activity progression remains owned by `ActivitySystem`.
- Study, training, work, and crime use the existing `ActivityService` behavior.
- Crime may be started in repeating or single-attempt mode.
- Mode changes use the existing `ActivityService.SetMode` transition.
- Expected gameplay rejection returns an `ActivityActionResult`.
- Invalid method arguments continue to produce argument exceptions.

## Target public surface

```csharp
public interface INullByteActivitiesApi
{
    [RamCost(0.2)]
    ActivityInfo? GetCurrent();

    [RamCost(0.2)]
    IReadOnlyList<CourseInfo> GetCourses();

    [RamCost(0.2)]
    IReadOnlyList<FacilityInfo> GetFacilities();

    [RamCost(0.2)]
    ActivityRatesInfo GetCourseRates(
        string courseName,
        string? facilityName = null,
        ActivityMode mode = ActivityMode.Focused);

    [RamCost(0.2)]
    IReadOnlyList<CompanyInfo> GetCompanies();

    [RamCost(0.2)]
    IReadOnlyList<CrimeInfo> GetCrimes();

    [RamCost(0.5)]
    ActivityActionResult Study(
        string courseName,
        string? universityName = null,
        ActivityMode mode = ActivityMode.Focused);

    [RamCost(0.5)]
    ActivityActionResult Train(
        string courseName,
        string? gymName = null,
        ActivityMode mode = ActivityMode.Focused);

    [RamCost(0.5)]
    ActivityActionResult Work(
        string companyName,
        string? careerName = null,
        ActivityMode mode = ActivityMode.Focused);

    [RamCost(0.5)]
    ActivityActionResult CommitCrime(
        string crimeName,
        bool repeat = true,
        ActivityMode mode = ActivityMode.Focused);

    [RamCost(0.2)]
    ActivityActionResult SetMode(ActivityMode mode);

    [RamCost(0.2)]
    ActivityActionResult Stop();
}
```

The exact fields of the snapshot records must be finalized from the current domain types before implementation.

---

# Implementation sequence

## Step 1: Finalize the script-facing contract and DTOs

### Add

- `INullByteActivitiesApi`
- script-facing `ActivityMode`
- `ActivityKind`
- `CourseKind`
- `ActivityActionResult`
- `ActivityInfo`
- `ActivityProgressInfo`
- `ActivityRatesInfo`
- `CourseInfo`
- `FacilityInfo`
- `CompanyInfo`
- `CareerInfo`
- `JobRankInfo`
- `HeldJobInfo`
- `ActivityRequirementInfo`
- `CrimeInfo`
- `CrimeRewardsInfo`
- `JobActivityInfo`
- `CrimeActivityInfo`

The exact file split should follow the existing scripting API conventions. If Botnet DTOs use one type per file, Activities should do the same. If small related records are grouped, follow that pattern.

### Requirements

- DTOs must contain values only.
- Collections returned to scripts must not expose mutable catalog collections.
- Domain `Activity`, `ClassActivity`, `JobActivity`, `CrimeActivity`, `Crime`, `Company`, `CareerPath`, and `TrainingFacility` objects must not cross the script boundary.
- Public enum values should use player-facing vocabulary.
- Job ranks exposed to scripts should be one-based.
- Crime cycle progress should follow a documented range, preferably `0.0` to `1.0`.
- Crime rates must not be represented as continuous rates.
- Optional type-specific data should be absent for unrelated activity kinds.

### Design checkpoint

Before implementation, confirm whether shared `SkillKind` is already deliberately exposed to scripts. If not, introduce a script-facing equivalent and map it in the adapter.

---

## Step 2: Implement the Activities adapter

Add a concrete `NullByteActivitiesApi` in the same project and architectural location as the Files and Botnet adapters.

### Dependencies

The adapter should receive only the collaborators necessary to:

- call `ActivityService`;
- inspect player skills where current values are needed;
- calculate or retrieve qualification information not already exposed by `ActivityService`;
- map domain objects into snapshots.

Prefer using `ActivityService` as the principal dependency. Do not inject `GameState` merely for convenience if the required information already has an established service boundary.

### Responsibilities

`NullByteActivitiesApi` should:

- validate public enum values;
- map script enums to domain enums;
- delegate activity transitions to `ActivityService`;
- map `ActivityStartResult` into `ActivityActionResult`;
- map current activity state into `ActivityInfo`;
- map activity catalog data into immutable snapshots;
- calculate player-visible rates, chances, and rewards through existing formulas/services;
- preserve catalog order;
- preserve existing gameplay failure messages where no structured failure code exists.

It should not:

- assign `gameState.Activity.Current` directly;
- calculate rewards independently;
- calculate success chances independently;
- decide employment rank;
- deduct or award resources;
- advance activity progress;
- subscribe to activity events;
- own persistence;
- implement automatic activity replacement;
- parse failure messages into synthetic error codes.

### Internal mapping helpers

Use small private mapping methods rather than placing all conversion logic in each public method.

Likely helper responsibilities:

- map activity mode in both directions;
- identify `ActivityKind`;
- map common progress;
- map continuous rates;
- map class activity details;
- map job activity details;
- map crime activity details;
- map crime rewards;
- map companies, careers, ranks, and requirements;
- map `ActivityStartResult`;
- map stopped activity results.

Do not create a general mapping framework. These mappings are small, explicit, game-specific transformations.

---

## Step 3: Implement query operations

### `GetCurrent()`

Return `null` when `ActivityService.Current` is `null`.

For an active activity, return:

- activity kind;
- player-visible name and description;
- focused/background mode;
- accumulated progress;
- continuous rates for class and job activities;
- job-specific information for work;
- crime-specific information for crimes.

Crime information should include:

- crime name;
- whether attempts repeat;
- cycle progress;
- attempts;
- successes;
- current success chance;
- calculated rewards for success and failure.

Job information should include:

- company;
- career;
- current title;
- one-based rank;
- total ranks;
- company reputation;
- whether the player is at the top rank;
- next-rank information where applicable.

### `GetCourses()`

Return all catalog courses in catalog order.

Map university courses to `CourseKind.Study` and gym courses to `CourseKind.Training`.

Do not include rates in `CourseInfo`, because rates depend on facility and mode.

### `GetFacilities()`

Return all facilities in catalog order, including:

- name;
- study/training kind;
- cost multiplier;
- experience multiplier.

### `GetCourseRates(...)`

This is the only proposed operation that does not currently have a direct purpose-built method on `ActivityService`.

Implement it by resolving the course and facility with the same behavioral rules used by `StartCourse`, then constructing the domain input required by the existing formula path.

The implementation must not duplicate name matching, default-facility selection, or kind compatibility differently from `StartCourse`.

If doing this cleanly requires copying significant validation from `ActivityService.StartCourse`, first extract a shared, non-mutating resolution operation inside Core. Both `StartCourse` and the script query should then use that operation.

Do not start and roll back an activity to calculate rates.

### `GetCompanies()`

Return each company in catalog order with:

- current reputation;
- held job, if any;
- careers;
- ranks;
- player-relative qualification requirements.

Do not reproduce qualification formulas in the adapter. Use `ActivityFormulas` or expose an appropriate read-only query through `ActivityService`.

### `GetCrimes()`

Return each crime in catalog order with:

- name;
- duration;
- current player-relative chance;
- focused success/failure rewards;
- background success/failure rewards.

Use `ActivityService.GetCrimeChance` and `ActivityService.GetCrimeRewards`.

---

## Step 4: Implement activity start operations

### `Study(...)`

Delegate to:

```csharp
ActivityService.StartCourse(
    FacilityKind.University,
    courseName,
    universityName,
    mappedMode)
```

Map the result into `ActivityActionResult`.

### `Train(...)`

Delegate to:

```csharp
ActivityService.StartCourse(
    FacilityKind.Gym,
    courseName,
    gymName,
    mappedMode)
```

Map the result into `ActivityActionResult`.

### `Work(...)`

Delegate directly to `ActivityService.StartWork`.

Preserve the returned notice because it communicates whether the player was:

- hired;
- promoted;
- returning to an existing position.

The returned activity snapshot should reflect the final rank selected by the service.

### `CommitCrime(...)`

Delegate directly to `ActivityService.StartCrime`.

Preserve both:

- `repeat: true`, for ongoing crime activity;
- `repeat: false`, for a single attempt.

Do not add waiting behavior for a single attempt. The call returns once the activity has started.

### Result mapping

For a successful start:

- `Succeeded` is `true`;
- `Activity` contains a snapshot of the started activity;
- `Message` contains the service notice when present, otherwise a concise success message.

For a failed start:

- `Succeeded` is `false`;
- `Activity` is `null`;
- `Message` contains the service failure message.

---

## Step 5: Implement mode and stop operations

### `SetMode(...)`

- Validate the script-facing enum.
- Map it to the domain enum.
- Call `ActivityService.SetMode`.
- Return failure when the player is idle.
- Return the updated snapshot on success.

Calling `SetMode` with the already-active mode should succeed and be idempotent.

### `Stop()`

- Call `ActivityService.Stop`.
- Return failure when the player is idle.
- Map the returned, stopped activity into a final snapshot.
- Preserve accumulated progress in that snapshot.

Do not calculate or apply a separate stop summary in the adapter unless the existing domain exposes a structured summary. The snapshot is the script-readable outcome.

---

## Step 6: Add `Activities` to the root `nb` API

Update the root scripting contract so player scripts can access:

```csharp
nb.Activities
```

Update the concrete root implementation and composition root to supply `NullByteActivitiesApi`.

The sub-API instance should be scoped consistently with the existing Files and Botnet APIs. It must refer to the current session’s `ActivityService` and must not survive session replacement with stale service or game-state references.

This is especially important because reset/load performs session replacement rather than field clearing.

---

## Step 7: Add semantic RAM support

Add `RamCostAttribute` to every interface member:

- `0.2` GB for queries and state inspection;
- `0.5` GB for activity transitions.

Verify that the existing semantic RAM analyzer recognizes calls through:

```csharp
nb.Activities.GetCurrent()
nb.Activities.Study(...)
nb.Activities.CommitCrime(...)
```

Also verify that merely obtaining the `Activities` property does not incorrectly charge all Activities capabilities unless that is the established analyzer behavior.

No RAM cost should depend on:

- activity duration;
- number of activity cycles;
- crime repetition;
- script thread count;
- number of catalog entries returned.

---

## Step 8: Verify Roslyn and editor exposure

Confirm that the new sub-API and DTOs are available consistently to:

- script compilation;
- RAM analysis;
- editor language services;
- included script libraries that receive API dependencies.

Avoid special editor-only registrations. The same script contract should be used by compilation and language services.

Acceptance checks:

- IntelliSense discovers `nb.Activities`.
- Completion shows all public members.
- Named and optional arguments work.
- Invalid enum or argument usage produces normal C# diagnostics.
- The editor and runtime resolve the same types.
- RAM analysis recognizes the same members the compiler accepts.

---

## Step 9: Update documentation

After implementation:

- update `CURRENT_STATUS.md` to mark the M10 Activities API complete;
- update `CODEBASE.md` with the Activities API entry points;
- update `DESIGN.md` only if the script-facing product behavior is not already described;
- update `ARCHITECTURE.md` only if adding the sub-API changes an architectural boundary;
- leave `AI_CONTEXT.md` unchanged unless a new durable invariant was introduced.

---

# Required tests

## 1. Contract and RAM metadata tests

Add tests that verify:

- every `INullByteActivitiesApi` member has `RamCostAttribute`;
- query members use the agreed query cost;
- command members use the agreed command cost;
- the root `INullByteApi` exposes an `Activities` property of the expected interface type.

If equivalent reflection tests already cover other APIs, extend those tests rather than introducing a separate convention.

---

## 2. Current activity query tests

### Idle state

- `GetCurrent()` returns `null` when no activity is active.

### Study activity

- returns `ActivityKind.Study`;
- maps university course and facility correctly;
- maps mode correctly;
- includes accumulated progress;
- includes continuous rates;
- excludes job and crime details.

### Training activity

- returns `ActivityKind.Training`;
- maps gym course and facility correctly;
- maps mode correctly;
- includes accumulated progress and rates.

### Work activity

- returns `ActivityKind.Work`;
- includes company and career;
- exposes the correct title;
- converts zero-based domain rank to one-based script rank;
- exposes total rank count;
- exposes current company reputation;
- exposes next rank when one exists;
- reports top-rank status correctly;
- includes work rates;
- excludes crime details.

### Crime activity

- returns `ActivityKind.Crime`;
- includes repeat mode;
- includes attempts and successes;
- includes cycle progress;
- includes current success chance;
- includes calculated success and failure rewards;
- excludes continuous rates;
- excludes job details.

### Snapshot isolation

- a returned catalog or current-activity snapshot cannot mutate domain state;
- a previously returned snapshot does not silently change when the domain activity advances;
- a second query returns the updated values.

The last test is important if records contain collection properties. Ensure those collections are copied rather than exposing domain-owned mutable dictionaries or lists.

---

## 3. Catalog query tests

### Courses

- returns university and gym courses;
- preserves catalog order;
- maps course kinds correctly;
- does not expose mutable domain collections.

### Facilities

- returns universities and gyms;
- preserves catalog order;
- maps cost and experience multipliers;
- maps facility kind to script vocabulary.

### Course rates

Test at least:

- university course with explicit university;
- university course with omitted facility uses the first university;
- gym course with explicit gym;
- focused mode rates;
- background mode rates;
- unknown course;
- unknown facility;
- course/facility kind mismatch;
- invalid mode;
- query does not start or replace an activity;
- query does not charge money or award experience.

### Companies

Test at least:

- company with one career;
- company with several careers;
- player with no held job;
- player with a held job;
- company reputation;
- current title and one-based rank;
- top rank;
- next-rank requirements;
- met and unmet requirements;
- catalog order;
- returned collections are isolated from the domain.

### Crimes

Test at least:

- catalog order;
- duration mapping;
- chance reflects current player skills;
- focused success rewards;
- focused failure rewards;
- background success rewards;
- background failure rewards;
- querying crimes does not advance or start an activity.

Use deterministic formulas and fixed player state. Do not assert approximate values derived from uncontrolled test setup.

---

## 4. Study tests

Test:

- valid university course starts a study activity;
- default university is selected when omitted;
- explicit university is respected;
- focused mode reaches the service;
- background mode reaches the service;
- successful result contains a study snapshot;
- unknown course returns failure;
- gym course passed to `Study` returns failure;
- unknown university returns failure;
- gym passed as university returns failure;
- insufficient money returns failure;
- existing activity returns the normal busy failure;
- failed start does not replace the current activity;
- null, empty, or whitespace course name throws the expected argument exception.

Prefer assertions against observable state and result values, not private adapter helpers.

---

## 5. Training tests

Mirror the Study tests for gym behavior:

- valid gym course starts training;
- default gym is selected;
- explicit gym is respected;
- mode is mapped;
- university course passed to `Train` fails;
- university passed as gym fails;
- insufficient funds fails;
- busy player fails without replacing the activity;
- invalid course argument throws.

These tests should remain separate enough to catch swapped `FacilityKind` mappings.

---

## 6. Work tests

Test:

- known company with a single career starts work when career is omitted;
- existing held career is selected when career is omitted;
- multi-career company requires a career when none is held;
- explicit valid career starts work;
- unknown company returns failure;
- unknown career returns failure;
- unqualified player returns failure;
- qualifying player is hired at the highest qualifying rank;
- held rank is not reduced;
- switching careers follows existing service behavior;
- returned message preserves hired notice;
- returned message preserves promoted notice;
- returned message preserves back-at-work notice;
- result snapshot reflects the final job and rank;
- busy player fails without modifying employment or activity;
- invalid company argument throws.

The adapter tests should not retest every promotion formula already covered by `ActivityService` tests. Include enough coverage to prove that arguments and results are mapped correctly.

---

## 7. Crime tests

Test:

- valid repeating focused crime starts correctly;
- valid repeating background crime starts correctly;
- `repeat: false` creates a single-attempt crime;
- result snapshot preserves repeat mode;
- unknown crime returns failure;
- busy player returns failure without replacing the current activity;
- invalid crime argument throws;
- returned chance uses current player skills;
- returned rewards match the requested activity mode;
- thread count has no effect on crime state or rewards, if the script API implementation has access to process thread information.

The actual completion of crime attempts belongs in existing `ActivitySystem` and domain tests, not in adapter unit tests.

---

## 8. Mode-change tests

Test:

- focused activity can move to background;
- background activity can move to focused;
- setting the existing mode succeeds and is idempotent;
- returned snapshot contains the new mode;
- idle player receives a failed result;
- invalid scripting enum value throws;
- mode change does not reset progress;
- mode change does not replace the activity instance in domain state;
- subsequent calculated rates/rewards reflect the new mode.

---

## 9. Stop tests

Test:

- stopping an active study activity succeeds;
- stopping training succeeds;
- stopping work succeeds;
- stopping crime succeeds;
- current activity becomes `null`;
- result contains a final snapshot;
- final snapshot preserves accumulated progress;
- stopping while idle returns a failed result;
- stopping twice succeeds once and fails the second time;
- existing `ActivityEnded` behavior still occurs exactly once.

Do not duplicate the service’s event tests unless the adapter alters event behavior. One integration-level assertion is enough to prove the adapter delegates through the service rather than clearing state directly.

---

## 10. Root API and composition tests

Test:

- `NullByteApi.Activities` returns the configured Activities API;
- it is not `null`;
- all APIs within one session are wired to the same activity/player state;
- reset creates an API bound to the new session;
- load creates an API bound to the loaded session;
- the old API is not retained by newly created script contexts;
- calls made in the new session cannot observe or mutate the old activity state.

This area is particularly important because session replacement is an established architecture rule.

---

## 11. Script compilation tests

Compile representative scripts using:

```csharp
var current = nb.Activities.GetCurrent();
```

```csharp
var result = nb.Activities.Study("Algorithms");
```

```csharp
var result = nb.Activities.Train(
    "Strength",
    mode: ActivityMode.Background);
```

```csharp
var result = nb.Activities.Work(
    "Cyberdyne",
    "Engineering",
    ActivityMode.Background);
```

```csharp
var result = nb.Activities.CommitCrime(
    "Shoplift",
    repeat: false);
```

```csharp
nb.Activities.SetMode(ActivityMode.Background);
nb.Activities.Stop();
```

Verify:

- scripts compile;
- optional arguments resolve correctly;
- named arguments resolve correctly;
- DTO properties are accessible;
- no domain activity types are required by the script;
- included libraries can reference the interface when passed the appropriate dependency;
- invalid usage produces Roslyn diagnostics rather than runtime-only failures where possible.

Use actual catalog names from the test fixture rather than the illustrative names above.

---

## 12. RAM analysis tests

Add semantic analyzer tests for:

- `GetCurrent`;
- one catalog query;
- `GetCourseRates`;
- `Study`;
- `Train`;
- `Work`;
- `CommitCrime`;
- `SetMode`;
- `Stop`;
- multiple references use the analyzer’s established deduplication behavior;
- use from an included library is counted;
- unused Activities members are not charged;
- optional arguments do not change RAM cost;
- `repeat: true` and `repeat: false` cost the same;
- focused and background modes cost the same;
- script thread count does not alter static RAM calculation.

Use the real Roslyn compilation path rather than invoking the cost attribute through reflection alone.

---

## 13. Integration tests

Add a small number of end-to-end tests through the public script API.

### Background activity integration test

1. Run a script that starts background training.
2. Assert that the call returns.
3. Advance the game clock.
4. Assert that experience increases through the normal `ActivitySystem`.
5. Assert that the script adapter did not apply rewards itself.

### Single-attempt crime integration test

1. Start a crime with `repeat: false`.
2. Advance the game clock through one attempt.
3. Assert that the crime ends.
4. Assert that the normal deterministic random source decides the outcome.
5. Assert that rewards are applied exactly once.

### Repeating crime integration test

1. Start a crime with `repeat: true`.
2. Advance through more than one attempt.
3. Assert that the activity remains active.
4. Assert that attempts and successes shown by `GetCurrent()` match domain state.
5. Stop through `nb.Activities`.
6. Assert that no additional attempts occur.

### Session replacement integration test

1. Start an activity through the script API.
2. Reset or load.
3. Obtain the new session API.
4. Assert that the new API reports the new session’s activity state.
5. Assert that the previous API is not used by subsequent script execution.

---

# Test-boundary guidance

## Test in the adapter

Test:

- correct delegation;
- enum mapping;
- DTO mapping;
- snapshot isolation;
- public argument behavior;
- public result behavior;
- registration and composition;
- RAM and Roslyn integration.

## Do not duplicate in the adapter

Do not comprehensively retest:

- course cost formulas;
- skill gain formulas;
- promotion formulas;
- crime chance formulas;
- crime reward formulas;
- activity tick progression;
- persistence serialization;
- general activity start validation already covered by `ActivityService`.

Use a few representative integration cases to prove that the adapter reaches those mechanics correctly.

## Test style

- Use xUnit.
- Prefer real `ActivityService`, `ActivityCatalog`, `ActivityFormulas`, `GameState`, and deterministic collaborators.
- Use NSubstitute only where constructing the real collaborator would obscure the test.
- Avoid mocking `ActivityService`; it is a concrete domain boundary whose behavior is central to the adapter.
- Use deterministic time and randomness for integration tests.
- Assert observable behavior rather than private mapping methods.