while (true)
{
    await ns.Hack("webmail");
    if (ns.Money > 150) await ns.Buy("cpu");
    if (ns.Money > 1000) await ns.Buy("botnet");
    ns.Log($"tick {ns.Ticks}: {ns.Money:0.} cr, level {ns.Level:0.}");
    await ns.Wait(20);
}