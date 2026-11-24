# The resulting workflow

| File | Purpose | Changes |
| :--- | :--- | :--- |
| DESIGN.md	| Full game specification | Occasionally |
| ARCHITECTURE.md | Technical architecture | When architecture changes |
| CODEBASE.md | Map of important files/types | When structure changes |
| CURRENT_STATUS.md | Where development currently stands | Frequently |
| AI_CONTEXT.md | Compact instructions/context for AI-assisted development | Rarely |

## Token budget
AI_CONTEXT       ~2.0k
CODEBASE         ~3.0k
CURRENT_STATUS   ~1.5k
ARCHITECTURE     ~2.5k
DESIGN           ~3.0k
               ------
Baseline        ~12.0k


## For each new milestone:

1. Load the five small context documents.
2. Describe what you want to implement.
3. I determine the impacted subsystem.
4. I ask for the specific current source files needed.
5. We agree on the architecture.
6. We implement incrementally.
7. We add/update tests.
8. We update CURRENT_STATUS.md.
9. If architecture changed, update ARCHITECTURE.md/DESIGN.md.

For a project the size NullByte is becoming, this is the model I'd use rather than attaching the complete codebase on every conversation.

There is also a more advanced next step worth considering for you: making the repository itself queryable so I can retrieve individual 
files or code symbols on demand, rather than you manually supplying them. That's essentially a lightweight codebase-RAG / MCP approach, 
and for a project you're developing over months it can remove much of the remaining manual context management.


## Or just

Using AI_CONTEXT.md, CODEBASE.md, and the current design document, I want to implement M11.3.

First review the architecture and identify which implementation files you'll need.


## Files
AI_CONTEXT.md, CODEBASE.md, CURRENT_STATUS.md, ARCHITECTURE.md, DESIGN.md