while (true)
{
    await Hack("webmail");
    if (Money > 150) await Buy("cpu");
    if (Money > 1000) await Buy("botnet");
    Log($"tick {Ticks}: {Money:0.} cr, level {Level:0.}");
    await Wait(30);
}