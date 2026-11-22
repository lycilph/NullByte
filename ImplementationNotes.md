# Settled architecture

Area	Decision
Syntax	#include "path/to/lib.cx", root-relative, resolved on the server the script runs on
Library files	Usings and type declarations only. nb is passed as a parameter.
Graph	Include once, cycles are errors, max depth 8
Compile	The main script keeps its wrapper. Each include is its own syntax tree in the same compilation.
RAM	Whole-file cost of every included file
Errors	Missing, cyclic, too-deep or malformed includes give clear diagnostics with file, line and column
Editor	Diagnostics, completion, hover and signature help across includes. Libraries compile standalone while edited.

## Step 1: find and resolve includes (no compiler changes yet)

This step turns a root script into an ordered list of files: dependencies first, de-duplicated, with errors reported.
Nothing else changes yet, so it's easy to test in isolation.

Roslyn parses #include "x" as a bad directive, so the scanner asks the syntax tree for those directives. 
That way #include inside strings or comments is ignored automatically, and then it matches the text of each one.

## Step 2 makes the compiler accept includes. 

Each included file becomes its own syntax tree, and run and mem work end to end.

**One design point first**

ScriptService.Start compiles inside Task.Run, which is off the game thread.
A provider that reads the virtual file system lazily during compile would touch game state from
the thread pool and break the single-thread rule. So the includes are resolved on the game thread
into a SourceSnapshot (plain strings), and the snapshot is handed to the compiler. Callers already
on the game thread (mem) can pass the live provider.

**Known gaps, each fixed in a later step**
RAM: mem and run still ignore nb calls inside included files (step 3).
VS Code: diagnose doesn't pass a provider yet, so a script with includes shows "Include not found" in 
the Problems panel until the editor step.
#include placement: the directive is accepted anywhere at the top level of a file.

**Step 3 makes RAM cost cover every file in the compilation.**
It's a small step, because the analyzer only needs to look at more syntax trees.

What changes

The analyzer currently takes one semantic model and one tree. It should take the whole compilation and 
every tree that belongs to the script. The base cost (1.6) is added once, and each distinct [RamCost] method 
is counted once across all files. A script and its library that both call nb.Hack therefore pay for it once, as before.

What's next

One rough edge showed up while reading mem: when compilation fails, GetScriptRam returns 0 and the console prints
"Something went wrong...". Missing includes make that much more likely, so step 4 will show the diagnostics there. 
After that:

Step 4: mem and console error polish, plus the editor diagnose op passing a source provider (the editor uses the unsaved buffer for the open file and the file system for everything else). The extension then shows each diagnostic in the file it belongs to.
Step 5: the language service builds its project from the script plus its includes (completion, hover, signature help).
Step 6: library files checked standalone while edited, and go-to-definition if it's cheap.
Step 7: design doc update.

**Step 4 makes the compile errors visible in the console and the editor.**

Console: mem shows the real reason when it fails.
Editor: the diagnose op gives the compiler the included files, so the editor stops reporting false "Include not found" errors.
Extension: diagnostics land in the file they belong to, so an error in lib/util.cx shows up in util.cx even 
while you're editing main.cx.

I re-read GetScriptRam, the mem command, the editor's Diagnose and diagnostics.ts before writing this.

Known limits
No automatic dependent re-check: after you fix a library, the script that includes it only updates when you edit it. 
That's the "no dependent re-check" decision from earlier. If it annoys you in practice, it's cheap to add later.
Unsaved library edits: includes come from the saved files, so an unsaved library edit isn't seen by the script that 
includes it until you save.

**Step 5 makes completion, hover and signature help work in a script that includes libraries.**

One bug to fix on the way

The extension sends two different path formats. diagnose sends the game path (/home/a.cx), but complete, 
hover and signatureHelp send doc.uri.path (/NullByte/home/a.cx). The server never used that path for those ops, 
so it didn't matter. Now it does, because it tells us which server's files to read the includes from.
So the three providers switch to toGamePath.