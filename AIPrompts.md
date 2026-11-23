Please disregard earlier chats about this project, and start over
You are an expert programmer and architect.

I would like to make an incremental game like bitburner, but using c# and the the roslyn compiler.
The game should use the same mechanics as bitburner, but I would like to start out with a simpler game that can grow later.

The goal for the first iteration is a game with the hacknet functionality from bitburner (for passive income) and the hacking/scripting functionality.

The application should start as a console application but will be changed to wpf with avalonedit (for script editing) as a later goal.

The player should also be able to do the hack/grow/weaken from the console (not just scripts), which should block the input until it completes.

I would like the game simulation to run on a single thread using a custom synchronization context.

We should build this step by step, so please start by making a game design document with milestone on the way to the first playable game.




----------------- Follow up prompt -------------------
Help me implement the game described in this design document and this codebase. I have reached milestone x
Help me finish x



---- Misc ---
When posting code, don't wrap lines unless they are at least 80 characters long.


You are an expert developer and architect.
Please review this codebase and suggest where I can simplify the code.
Secondarily review for best practises

You are an expert developer and architect.
<request here>
We should settle on a target architecture, and then work towards that.
Before we start the implementation, ask any clarifying questions. 
When ready, go one step at a time and explain the steps in a concise manner.


---- Repomix ---
Exclusion parameter:
*.md, *.git*, NullByte/* 

Or without the test projects
*.md, *.git*, NullByte/*, NullByte*Test*/**