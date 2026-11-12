# NULLBYTE — C# Incremental Hacker Game - DevJournal

## 11-11-2026

### In Progress
- Simplification of entire codebase

### Done

### Decisions
- Removed scriptruntime (kept a per-script api, which was unneeded)

### Future
- Refactor tab-completion

---
## 09-11-2026

### In Progress
- M5 milestone
- add files to autocomplete

### Done
- Fix tab completion for botnet commands
- M4 milestone
- Hardening pass done over the code

### Decisions
- Investigate solution with a queue in the gameloop - Claude seems to think it a bad idea
- Change the name of the gameloop into something different (like gameclient or something) - maybe GameKernel?

### Future
- Generalize the subcommand handling in the completion engine
- Add debugdisplay to domain classes
- Add animated startup (see version 0.5)

---
## 08-11-2026

### In Progress
- Hardening pass done over the code

### Done
- M3.5 milestone

### Decisions

### Future

---
## 05-11-2026

### In Progress
- M3 milestone

### Done
- M2 milestone done
- M0 and M1 milestones from design document is completed
- Hardening pass done over the code

### Decisions
- Restarted entire project

### Future

---
## 03-11-2026

### In Progress
- Fix issues identified by ChatGPT

- State issues
  - ~~Fix GameSession: fix the synchronization boundary~~
  - Although the scripting API uses session.Read() for synchronized access, other consumers can access the mutable collections directly.
	- Expose snapshot collections

## TODO
- ScriptingSystem has concurrency risks
  - Consider using a ConcurrentDictionary<string, ScriptInstance> keyed by script name.
- ApiCallsThisTick is incremented without synchronization.
  - I would make the API quota accounting thread-safe or enforce sequential execution of API operations per script.
- Handle missing target state when loading older saves
  - HackSystem.EnsureTargetStates(State, content);
	- Initialize missing targets.
	- Preserve existing target state.
	- Handle removed targets deliberately.
	- Avoid silently resetting existing progress.
-  Corrupt saves should not silently become new games
	-  would distinguish between:
		- No save exists.
		- Save loaded successfully.
		- Save is corrupt.
		- Save version is unsupported.
- Naming conventions are inconsistent. For example, there is a mixture of snake_case and PascalCase/camelCase in the C# implementation.

### Done
- Fixed issues identified by Mistral (gave it the entire codebase to look for errors) - see CodebaseIssues.md

### Decisions
- Removed lazy initialization of target state to remove potential threading issues in the gametick vs script api reads

### Future
- Network of nodes (like bitburner) to be discovered using network scans
- Auto-save (every x secs)
- Implement the CPU quota part (so it actually does something)

---
## 02-11-2026

### In Progress
- ~~Make an autohack script to test scripting api~~
- ~~Update scripting api (get targets)~~
- ~~Implement support for libraries~~

### Done
- Implemented support for libraries
- Updated scripting api (get targets)
- Made an autohack script to test scripting api

### Future
- Autocomplete targets etc.
- Add a compile script option in the game (when we have a UI)

---
## 01-11-2026

### In Progress

### Done
- Added the scriptglobals, so that all interaction goes through the api explicitly
- Added tests for the scripting system
- Split scripting tests into separate classes for better overview
- Implemented the new Hack/Grow/Scrub setup
- Fixed EngineTests (commented out now)
- Fixed ScriptingTests (commented out now)

### Future
- Clean up files (add a domain folder - gamestat + etc. in separate files)
- Add a scan command (shows, current money, heat etc. for a given target)
- Update stats (total grow and scrub jobs)

---
## 31-10-2026

### In Progress

### Done
- First version of the scripting api
- Implemented script persistence
- Passing parameters to scripts implemented
- Change the print loop to use concurrentqueue

### Future
- ~~Getting the lists of targets, upgrads, etc. is not implemented in the scripting api~~
- ~~Add option (in api) to get calculated duration of a hack~~