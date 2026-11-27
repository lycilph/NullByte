# NullByte

An incremental hacking game in C#. You start by typing console commands, then automate the economy by writing C# scripts against a small game API (the `nb` object), compiled at runtime with Roslyn. Manual commands and scripts use the same underlying game rules.

Inspired by Bitburner. The first iteration is a console game with an optional VS Code extension for editing scripts; a WPF front end with an AvalonEdit editor is a longer-term possibility.

## Documentation

| File | Purpose |
|---|---|
| `NullByte Game Design Document.md` | Vision, pillars, scope and detailed game design |
| `ARCHITECTURE.md` | Layering, ownership rules and architectural principles |
| `CODEBASE.md` | Map of where to look first when changing a feature |
| `DECISIONS.md` | Why the important architectural decisions were made |
| `DEVELOPMENT.md` | Build/test commands, conventions and development workflow |
| `ROADMAP.md` | Milestones |
| `todo.md` | Current tasks and ideas |
| `NARRATIVE.md` | Narrative setting, characters, story arc and capability-unlock principles |
| `NARRATIVE_IMPLEMENTATION_PLAN.md` | Staged plan for designing and implementing the narrative tutorial |
| `NARRATIVE_TUTORIAL.md` | Current command/API capability map and proposed first playable walkthrough |

## Build and run

```text
dotnet build NullByte.slnx
dotnet run --project NullByte.ConsoleUI
```

## Command-line options

Run `dotnet run --project NullByte.ConsoleUI -- --help` (or pass `help`) to list the available options. Help is printed before application settings are loaded.

See `DEVELOPMENT.md` for tests, the VS Code extension and platform notes.

## Misc stuff

Run this in a powershell console (from the root folder) to find (and fix - for fixing add `-fix` flag) formatting issues `.\Scripts\check-formatting.ps1`