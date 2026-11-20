# In progress

* M8 milestone - Visual Studio Code editor integration
  * Subfolders: this needs a change to VirtualFileSystem.
  * Run from VS Code: a command, or a run operation that executes the current file.
  * Debug command to see if any (external) editors is connected
  * (Do we need this?) full-range underlines for diagnostics,
  * Maybe: launch vs-code from the game?

# Todo

* include other files
* Add options for Ram usage and compilation to the NanoEditor 
* Align script method names and console commands
* Add the backdoor command
  * Time = same as hack, level required = same as hack, effect: the player can always connect directly to the server


# Future ideas

- Expand the virtual filesystem to also handle folders
- Later on extra servers would live in api.cloud.* etc.
- Make botnet and cloud servers unlockable
  - Make a system to unlock features on a trigger
- Add debugdisplay to domain classes
- Use mocks in the tests
- Add animated startup (see version 0.5)
- Add a compile script option in the game (when we have a UI)
- print/tprint destinction (script output versus player terminal output)
- Move base-cost of scripts (1.6 in ScriptRamAnalyzer) to the balance class