Recommended implementation order
So, in practical terms, I'd work through M5 as:
Step	Component	Goal
M5.1	Existing code review	Establish what M1–M4 already provide
M5.2	Virtual FS	Store scripts
M5.3	FS tests	Make storage reliable
M5.4	Roslyn	Add compiler
M5.5	Compiler	Compile trivial C#
M5.6	Diagnostics	Line/column errors
M5.7	Script context	Bridge scripts to game
M5.8	NS	Add first scripting API
M5.9	Script references	Make NS visible to scripts
M5.10	run	Execute scripts
M5.11	Sleep	Integrate game clock
M5.12	Actions	Connect hack/grow/weaken
M5.13	Example script	Prove the game loop works
M5.14	Load context	Support unloading
M5.15	WeakReference test	Verify unloading
M5.16	Acceptance	Run the complete M5 scenario