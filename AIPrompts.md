You are an expert programmer and architect.

I would like to make an incremental game like bitburner, but using c# and the the roslyn compiler.
The game should use the same mechanics as bitburner, but I would like to start out with a simpler game that can grow later.

The goal for the first iteration is a game with the hacknet functionality from bitburner (for passive income) and the hacking/scripting functionality.

The application should start as a console application but will be changed to wpf with avalonedit (for script editing) as a later goal.

The player should also be able to do the hack/grow/weaken from the console (not just scripts), which should block the input until it completes.

So, i would like to begin with a very simple (but still somewhat complete) poc application that:
* Manual commands like: hack, scan etc.
* Scripting support for: hack, query, log
* A central simulator that owns game state