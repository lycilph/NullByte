# Todo

1. Simplify tab-completion
2. `nb`-API'et eksponerer `CancellationToken` på `Hack/Grow/Weaken/Sleep`. Sæt annullering pr. proces (M6), og lad `kill` virke implicit.
3. Del kommandoerne op i grupper med automatisk arity/usage, og flyt dem til `NullByte.Shell`.
4. Do another clean up round
5. M6 milestone


--- Old stuff below ---

# Review af NullByte, M0–M5

Jeg har læst designdokumentet og al kode undtagen detaljerne i testene og terminalens tegnekode. Kort version: **fundamentet er solidt, men det er over-konstrueret på tværs af hele løsningen.** Mængden af ceremoni skjuler den gode arkitektur.

## Hvad der er godt

- **Lagdelingen er ren.** `Core` kender hverken UI eller Roslyn, og Roslyn bor i `Scripting`.
- **Tråd- og tidsmodellen følger dokumentets søjler.** Scheduleren er en prioritetskø på spilletid, `RunContinuationsAsynchronously` sikrer, at continuations aldrig kører inde i `Update`, og `DrainBatch` forhindrer, at et script med `Task.Yield()` i en løkke sulter resten.
- **`ISimulationSystem`, formler og domæne er adskilt**, så et system kan testes ved at kalde `Update(delta)` direkte.
- **Scriptpipelinen** med collectible `AssemblyLoadContext`, in-memory emit og diagnostics med linje og kolonne virker og er testet, inklusive weak-reference-test af unload.
- **Testdækningen er bred.**

## Hvor det er indviklet, og hvad jeg ville gøre

### 3. Kommandolaget ligger forkert og er fuld af gentagelser

- **`CommandRegistryFactory` er 992 linjer.** Registrering og handler står adskilt, og `usage` står både i `CommandDefinition` og som hårdkodet `"Usage: money"` i handleren. Næsten alle ca. 20 handlere starter med den samme arity-kontrol. Giv `CommandDefinition` `MinArgs/MaxArgs`, og lad dispatcheren udskrive usage automatisk.
- **Opdel i grupper:** `NetworkCommands`, `FileCommands`, `ActionCommands`, `BotnetCommands`, hver med kun de services den bruger. Så forsvinder `CommandContext`, der med sine ti services i praksis er en service locator.
- **`hack`, `grow` og `weaken` er tre næsten identiske handlere** (ca. 150 linjer). Én `RunManualActionAsync(label, duration, Func<CancellationToken, Task<ActionResult>>)` klarer det.
- **Kommandolaget ligger i `ConsoleUI`.** Så er påstanden "WPF kræver kun en ny `IGameHost`" ikke sand: WPF-værten ville skulle duplikere alle kommandoer. Flyt parser, registry, handlere og completion-motor til et UI-agnostisk projekt (`NullByte.Shell`), så kun `ThreadSafeConsoleTerminal` og `Program` bliver i `ConsoleUI`.
- **`IScriptOutput` (Core) og `IGameTerminal.WriteLine` (ConsoleUI) er det samme**, og `TerminalScriptOutput` er kun en adapter mellem dem. Lav én `IGameOutput` i Core.
- **Tab-completion hårdkoder botnet** (`BotnetSubcommand`, `BotnetNodes`, `BotnetUpgradeKinds` i snapshot og motor). Det skalerer ikke til flere kommandoer. Lad hver kommando levere sin egen completer, så snapshot og `ArgumentCompletionKind` kan forsvinde.
- **`edit` er stadig en stub:** den skriver et hårdkodet script til `test.cx` uanset argument, og der ligger udkommenteret kode. Ret den, eller fjern den, indtil WPF-editoren kommer. Der er også rester i `ConsoleUI.csproj` (`TempTests\`, `temp\`).

## Forslag til rækkefølge

1. ~~Skift `ScriptRuntime` til `AsyncLocal` + `global using static`, og slet kildeomskrivningen. Det frigør M6, og testene kan køre parallelt.~~
2. ~~Gør `Tick()` offentlig og `GameLoop` afhængig af `IGameClock`. Fjern `Send`.~~
3. ~~Slå `Id` og `Hostname` sammen, og indfør én `ActionResult`.~~
4. Del kommandoerne op i grupper med automatisk arity/usage, og flyt dem til `NullByte.Shell`.
5. Ryd op i validering og null-tjek, og ret formlerne mod Bitburner, før I balancerer i M8.