# No atomic save.
File.WriteAllText mid-crash corrupts save.json and the player loses everything. 
Standard fix: write to save.json.tmp, then File.Move(..., overwrite: true). Also consider periodic autosaving (you currently only save on quit — a crash or Ctrl+C loses the session, and scripts run for hours by design).


# Design smells & smaller things
Tunables (StealBase, HeatPerHack, etc.) are exactly the kind of numbers you'll want to balance without recompiling — your own GameDesign Phase 6 says "move constants to content"; the HackSystem block is the prime candidate now.