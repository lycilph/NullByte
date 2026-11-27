# In progress

- Add a player sub-api (get skills, money etc.)
 
# Todo

- New milestone
  -  Story/narrative, features are unlocked slowly

- Maybe: add cancel manual hack, weaken and grow (backdoor?)
- Make the text in the terminal green (so it doesn't look like a normal console)
- Do the full solution format check

# Future ideas

- Test projects clean up
  - Consolidate startup/fixture/infrastructure code in a shared class
  - Use Mocks where appropriate
  - Look at the tests themselves
	- Done: ConsoleUI.Tests, Core.Tests
	- Missing: Editor.Tests, Scripting.Tests, Shell.Tests
  - OR start over with new tests...

- Make botnet and cloud servers unlockable
  - Make a system to unlock features on a trigger
- Do another cleanup round (see `AIPrompts.md`)
- Change the editor integration to something that can be enabled/disabled while the game is running
- Add options for Ram usage and compilation to the NanoEditor 
- Align script method names and console commands
- Later on extra servers would live in api.cloud.* etc.
- Add debugdisplay to domain classes
- print/tprint destinction (script output versus player terminal output)
- Move base-cost of scripts (1.6 in ScriptRamAnalyzer) to the balance class
- A tutorial
- Script sandbox follow-ups (see `ARCHITECTURE.md`, Script sandbox and safety)
  - Move the script-facing API into its own assembly so the banned-namespace list for `NullByte.Core` is no longer needed
  - Memory and stack limits, or running scripts out of process, if the game is ever opened to untrusted scripts
  - Document the script restrictions (loops, banned APIs) in the in-game help and the tutorial