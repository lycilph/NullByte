# Todo


* Start M6 milestone
	* ~~M6.1 — RAM metadata + static RAM analysis~~
	* ~~M6.2 — Process model and process manager~~
	* ~~M6.3 — Thread-aware script execution and RAM allocation~~
	  * ~~Fix all commented out tests~~
	  * ~~Make sure extensions is cx (in all tests)~~
	* M6.4 — Expand the nb API to the full M6 API
	  * ~~Ps, Kill, ScriptRunning, GetScriptRam~~
		* ~~in scan and ps - an empty hostname should resolve to the hostname of the process - when called from a script, if manual the current server~~
	  * ~~Run, Exec~~
	  * ~~scp, rm, ls, mv (should be in a sub class)~~
	  * All botnet stuff (Botnet.NumNodes, PurchaseNode, UpgradeLevel, UpgradeRam, UpgradeCore, cost getters) 
		* Place botnet methods in a separate api (so a script would do api.botnet.buy etc.)
	* ~~M6.5 — Console commands: ps, kill, killall, scp, mv, free, mem~~
	* M6.6 — Integration/acceptance tests and cleanup

* Use mocks in the tests
* Align script method names and console commands
* tab-completion (file or hostname, for 1-x arguments - see scp command for usage)
* Analyze should be a timed command
* Add the backdoor command
  * Time = same as hack, level required = same as hack, effect: the player can always connect directly to the server