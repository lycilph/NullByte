The order is:

~~1. the file system model, with tests (this step)~~
~~2. the save format~~
~~3. the console commands~~
4. nb.Files, running scripts from folders, and completion
5. the editor server and protocol
6. the VS Code extension
7. the design doc
8. cd and pwd

Step 1 adds folders to VirtualFileSystem. Nothing in the console or VS Code changes yet.

Design decisions, within your defaults:

**Paths**: a leading / is optional, and a single trailing / is allowed on folder paths. 
Segments can’t be empty, . or .., and can’t contain a backslash or control characters. 
Paths are case-sensitive, at most 8 levels deep and 256 characters long.

**Parent folders are created automatically** when a file is written, moved or copied, like mkdir -p. 
That keeps edit dir/x.cx and scp simple. A path can never be both a file and a folder.

**Empty folders survive deleting** their last file.

**Change events** carry a new IsDirectory flag. Deleting or moving a folder sends one event for the folder, not one per file. 
FileChange.Filename is now a path relative to the server root, and I kept the name so the editor server still compiles.