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