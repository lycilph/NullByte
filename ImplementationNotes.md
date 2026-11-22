# Activties (jobs, studying, training and crimes)

**Implementation steps**
1. Skills model and SkillFormulas.
2. Activity data and loader.
3. ActivityService and ActivitySystem with cycles, the focus flag and the 0.8 factor (study and train first).
4. Console: study, train, focus, background, stop, the focused screen with the indeterminate bar, and the interrupt keys.
5. Jobs.
6. Crimes (cycle loop, progress bar that restarts).
7. Save v4 and the migration.
8. nb.Activities.
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