while (true)
{
    await Hack("webmail");
    
    var executions = Data.GetNum("executions") + 1;
    Data.SetNum("executions", executions);
    
    if (Money > 150) await Buy("cpu");
    if (Money > 1000) await Buy("botnet");
    Log($"tick {Ticks}: {Money:0.} cr, level {Level:0.}, executions {executions:0.}");
    await Wait(30);
}