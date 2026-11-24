# Activties (jobs, studying, training and crimes)

**Implementation steps**
~~1. Skills model and SkillFormulas.~~
~~2. Activity data and loader.~~
~~3. ActivityService and ActivitySystem with cycles, the focus flag and the 0.8 factor (study and train first).~~
~~4. Console: study, train, focus, background, stop, the focused screen with the indeterminate bar, and the interrupt keys.~~
~~5. Jobs.~~
~~6. Crimes (cycle loop, progress bar that restarts).~~
~~7. Save v4 and the migration.~~
~~8. nb.Activities.~~
9 Design doc update.

## Step 1: skills model (no behaviour change)
SkillKind and PlayerSkills hold experience for the six skills.
SkillFormulas is the old hacking-level formula, now generic, with a per-skill multiplier in BalanceConfig.
Player.HackingExperience and AddHackingExperience still work as before. They now read and write Player.Skills, 
so no callers or tests change.
HackingFormulas delegates to SkillFormulas. I compared old and new hacking levels on 20,000 values and they match.
The save format is untouched for now. Only hacking can gain XP until step 3, and step 7 adds the other skills to the save.

## Step 2: activity data and loader

The shape mirrors your network loading: ActivityCatalogDefinition (JSON shape), ActivityCatalogLoader (validation), 
ActivityCatalogFactory.CreateDefault(), and Data/activities.json, which is registered in the csproj like initial-network.json.

**Domain.** The catalog holds facilities, courses, companies (with career paths and ranks) and crimes. It is fixed data, never saved.
**One simplification of my earlier plan.** Universities and gyms are both facilities that offer courses of their kind, 
differing only in price and quality. That means one "class" activity in step 3 covers both study and train.
**Validation.** Errors name the broken entry, e.g. Unknown skill 'hackin' in course 'algorithms'. The rules are:
Names are lowercase slugs (rothman-university), like hostnames, because they're typed as command arguments.
Names are unique, and skills are known.
Numbers are finite and in range.
Every course has a facility of its kind.
A career never gets easier or pays less as you climb.
**Data.** There are 2 universities, 2 gyms, 8 courses, 3 companies (each with software and security careers of 5 ranks) and 6 crimes. 
Every number is a placeholder for M13 and is flagged as such in the file.
Next: step 3

I'll build ActivityService and ActivitySystem, covering the focus flag with its 0.8 reward factor, the cycle model, 
and study/train first, and wire them into GameSession. Say "continue" and I'll start.

## Step 3
I verified it with a compile of Core on a tree that mirrors your Domain.Activities namespace move,
and a run of all 74 tests (steps 1 to 3) through a stand-in test runner. I could not compile NullByte.Shell 
(it needs the Roslyn packages), so the GameSession.cs edits at the end are the one part I checked only by reading them.

What this step does
**Activity is state only.** ClassActivity is the first subclass, and jobs and crimes will add siblings. It remembers its mode, elapsed time, net money and experience gained. GameState.Activity.Current holds the one running activity.
**ActivityFormulas.CalculateRates(activity)** returns money per second (signed: negative means it costs) plus experience per second. The 0.8 factor reduces rewards only, never tuition. Jobs in step 5 reuse the same method.
**ActivityService** is the single entry point: StartCourse, Stop, SetMode, and an ActivityEnded event raised on the game thread. Only one activity runs at a time.
**ActivitySystem** applies the rates each tick. It pays first, and if the player can't afford that slice, the activity ends with CannotAfford and gives nothing for that slice.

### Decisions to be aware of
With no facility named, the course goes to the first facility of that kind in the catalog (the catalog lists the cheapest first).
Starting a course needs enough money for one second of tuition.
GameServices gains a trailing ActivityService Activities parameter. Only GameSession.Create builds it in the pack, but any test that builds it by hand needs the extra argument.
Nothing is saved yet. A loaded game starts idle until step 7.

## Step 4: Console commands

Step 4 is done: study, train, stop, focus, activity and skills, with the focused screen and its hotkeys. My stand-in runner passes all 129
tests for steps 1 to 4, and still passes with Danish as the default culture, which I confirmed is actually in effect. 
I have not run the new terminal rendering or hotkey reading in a real console. It compiles, and its formatting logic is tested, but please 
try it by hand (checklist at the end). I also couldn't compile NullByte.Shell or NullByte.ConsoleUI as whole projects. The edits to GameSession.cs, 
CommandSetup.cs and GameHost.cs are checked by diff only.

What changed
Focused mode shows a two-line live panel above the prompt. The first line is a bouncing bar with the activity name, and the second is 
live totals (1m 23s | -$830.00 | +166.00 hacking xp) with the hotkeys at the right edge: [S] stop [B] background. Ctrl+C also stops and 
Esc also backgrounds (both hidden hints). A hotkey only signals. The command does the work when it resumes on the game thread, so the 
service is never touched from the UI thread.
Background mode (-b, or B from the panel) leaves the console free. The prompt shows [studying] or [training]. focus takes it back, and stop ends it.
study and train with no arguments list the courses (with rates) and the facilities. The -b flag can go anywhere.
Auto-end. If you can't pay tuition any more, the activity ends with a message and a summary, whichever mode it was in, and the prompt updates.
Thread safety. The UI thread reads the activity to draw the panel, so Activity now publishes an immutable ActivityProgress snapshot and a 
volatile Mode. Before, the UI could have read a dictionary the game thread was changing.
Dropped the background command I proposed earlier. While focused you can't type, and when already in the background it would do nothing,
so the hotkey covers it.
Culture bug caught. {x:P0} prints 50 % in da-DK, so my first tests would have failed on your machine. The new code builds percentages by hand, 
and the tests that check money pin the invariant culture. Your existing {...:N2} output follows your culture, and I left that alone.
What you'll need to adjust
Any test class that implements IGameTerminal needs the new member public IDisposable ShowLivePanel(LivePanel panel) => ...;.
GameServices gained ActivityService Activities in step 3 and now also SkillFormulas Skills. Anything that builds it by hand needs both.

## Step 5 is jobs. 
Before writing code, here is the design, grounded in the catalog and activity plumbing you already have:

State. A new Employment on GameState holds reputation per company and one job per company, as Job(career, rankIndex). 
Reputation is kept even if you move to another career.
Activity. A JobActivity is a new subclass of Activity. ActivityRates gains reputation, so the 0.8 factor covers salary, 
XP and reputation, and still never costs.
Starting work. ActivityService.StartWork hires you at the highest rank you qualify for (never lower than your current rank).
Promotions. ActivitySystem promotes you automatically while you work, and the service raises a JobPromoted event that the console prints.
Commands. work [company] [career] [-b] starts work or, with no arguments, lists the companies with your standing. quit is dropped: 
with automatic promotion and a persistent rank it would do nothing, and switching career at the same company already replaces the job.

## Step 6 is done: you can crime [name] [-b]. 
All 217 tests (steps 1 to 6) pass in my stand-in runner, in the default culture and under da-DK. 
The real terminal rendering is untested here, and the one-line GameSession.cs edit is uncompiled.

What changed
Attempts. A CrimeActivity is a cycle of fixed length. The roll happens when an attempt ends, using the same IRandomSource 
the hack roll uses (so tests can script it). ActivitySystem now takes it as a constructor parameter, and GameSession 
passes the one it already has.
Chance is the weighted average of the skill levels the crime asks for, divided by difficulty × CrimeSkillScale (100), 
clamped to 0 to 1. Not being focused never changes the chance.
Rewards. Success pays the money and all the XP. Failure pays no money and 25% of the XP (CrimeFailureExperienceFactor). 
Background multiplies everything by 0.8.
Timing. One update can finish several attempts, each rolled with the skills the player has by then (a success can lift 
your level mid-update). Left-over time carries into the next attempt.
repeat. The console always repeats. StartCrime(name, repeat: false) ends after one attempt with the new ActivityEndReason.
Completed, ready for the script API in step 8.
Console.
crime lists each crime with its duration, your current chance and the success reward.
Focused, the panel shows a real progress bar that fills and starts over, with 3/5 succeeded in the detail line.
A one-line result is printed per attempt while focused (shoplift: success (20% chance): +$300.00, …) and none in the background.
activity shows attempts, chance now and the reward.
Event order fix. CrimeAttempted is raised after progress is recorded, so attempt.Activity.Attempts is always current. 
The earlier order would have been a trap for the script API.
No constant rates. CalculateRates throws for a crime. Use GetCrimeChance and GetCrimeRewards instead.
**Balance flag for M13**

I ran the shipped numbers (seeded dice). Shoplift starts at 20%, reaches 100% after about 2 game-minutes, and then pays about 
$9,000 per game-minute. The entry job pays $240. So crime is roughly 40 times better than the intern job, which makes jobs pointless early on. 
That is partly Bitburner-like, but 40 times is a lot, and I haven't compared with hacking income. Money per attempt, not the formula, 
is probably the knob to turn.

Please judge by hand

Shoplift attempts every 2 game-seconds at game speed 5 means about 2.5 result lines per real second while focused. That may bury the console,
and I'd like your opinion before I add a throttle or collapse repeated lines.

## Step 7 is done: save format v4, with all six skills, jobs and reputation, and the current activity. 
All 259 tests (steps 1 to 7) pass in my stand-in runner, in the default culture and under da-DK. 
I also broke the mapper and the migration on purpose in three ways (skipping the progress restore, resuming focused, 
leaving the old field behind), and the tests failed each time.

Three caveats about what I could and couldn't verify:

Serializer copy. The round-trip tests ran against a copy of SaveGameSerializer without its two .NET 9+ options (NewLine, RespectNullableAnnotations), 
because my sandbox is .NET 8. Your real file is unchanged.
Uncompiled edits. The GameSession.cs and GameHost.cs edits are uncompiled.
Your own tests. I can't see your existing persistence tests, so the breakages in the list below are inferred from the code.
What the format does
Skills. PlayerSave.HackingExperience is replaced by Skills, a dictionary keyed by lowercase skill name. All six are always written,
so the file has the same shape every time.
Employment. EmploymentSave holds jobs (company, career, rank) and reputation per company, both sorted for deterministic output.
Activity. ActivitySave? is null when idle. Otherwise it holds the progress totals plus exactly one of class, job or crime. 
Unused kinds aren't written. A job stores only company and career, because the rank comes from the saved jobs and there's one 
source of truth. A crime stores its repeat flag and how far into the current attempt it was.
Names only. The save never stores rates or ranks, so rebalancing in M13 can't corrupt a save. A resumed activity always comes 
back in the background, as decided.
Content that vanished (a renamed course, a removed company, a shortened career) is not corruption. The activity or job is dropped 
and RestoredGame.Warnings says so. GameHost prints each warning as Warning: …. Reputation is kept even for a vanished company. 
A dropped job isn't lost either: working there again hires you at the highest rank you qualify for.
Invalid data is still refused with SaveFormatException: unknown skill, negative or non-finite numbers, successes above attempts, 
two kinds of activity at once, working without holding the job, duplicate companies.
Migration v3→v4 turns hackingExperience into skills.hacking, removes the old field, and adds an empty employment. 
A v3 save never had an activity.
Catalog. Restore(save, catalog = null) defaults to the shipped catalog, so existing calls still compile. 
RestoredGame remembers the catalog it used, and WithSave hands it to the session.
When you load, the host prints You were busy when this was saved: … and the prompt shows the tag. The same happens 
when the game resumes from autosave.

Saved numbers will show floating-point noise (for example 17.999999999999968). That's normal, and it round-trips exactly.

What breaks on your side
The golden-file test will fail, as designed, because the format changed. Regenerate it for v4. 
I'd keep your old v3 file as a migration fixture. My migration test builds the v3 shape in code, so it doesn't depend on yours.
Any test that builds a PlayerSave or SaveGame by hand won't compile. Replace HackingExperience = x with 
Skills = new Dictionary<string, double> { ["hacking"] = x }, and add Employment = new EmploymentSave { Jobs = [], Reputation = [] }.





You are an expert C#/.NET developer and software architect helping me develop NullByte.
Read the attached project context.
I would like to implement: The nb.Activities api of M10 Milestone

The intended outcome is: A sub-api like the Files and Botnet sub-apis, 
such that the player can interact with the activities part of the game, including working, studying, training and committing crimes.


Before discussing implementation, help me settle the feature requirements.

Identify:

1. unclear or missing behavioral decisions,
2. important edge cases,
3. interactions with existing features,
4. decisions that materially affect the architecture.

Give your recommendation for each unresolved point.

Do not design exact classes or APIs yet. We should settle behavior first.