# In progress

* Launch vs code with: `code -n --folder-uri "nullbyte:/NullByte/"`
```
using System.Diagnostics;

Process.Start(new ProcessStartInfo
{
    FileName = "vscode://command/nullbyte.openWorkspace",
    UseShellExecute = true
});
```

- Milestone M10: Activities (jobs, training, studying etc.)
- Clean up
  - Skill/Hacking formulas
  - etc?
  - session.Services.Activities.ActivityEnded += _ => terminal.SetPrompt(BuildPrompt(session.State)); (in gamehost)
	- What about loading/saving in this case???
- Remove/delete the ActivityState class? 
	
# Todo

- Make botnet and cloud servers unlockable
  - Make a system to unlock features on a trigger

# Future ideas

- Do another cleanup round (see `AIPrompts.md`)
	- Both game and vscode extension 
- Change the editor integration to something that can be enabled/disabled while the game is running
- Add options for Ram usage and compilation to the NanoEditor 
- Align script method names and console commands
- Add the backdoor command
	- Time = same as hack, level required = same as hack, effect: the player can always connect directly to the server
- Later on extra servers would live in api.cloud.* etc.
- Add debugdisplay to domain classes
- Use mocks in the tests
- Add animated startup (see version 0.5)
- Add a compile script option in the game (when we have a UI)
- print/tprint destinction (script output versus player terminal output)
- Move base-cost of scripts (1.6 in ScriptRamAnalyzer) to the balance class