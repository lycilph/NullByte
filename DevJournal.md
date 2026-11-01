# NULLBYTE — C# Incremental Hacker Game - DevJournal

## 01-11-2026

### In Progress
- Implement the new Hack/Grow/Scrub setup

### Done
- Added the scriptglobals, so that all interaction goes through the api explicitly
- Added tests for the scripting system
- Split scripting tests into separate classes for better overview

### Future
- Fix EngineTests (commented out now)
- Fix ScriptingTests (commented out now)
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
- Getting the lists of targets, upgrads, etc. is not implemented in the scripting api
- Add option (in api) to get calculated duration of a hack