while (true)
{
    await nb.Hack("webmail");
    
    var executions = nb.Data.GetNum("executions") + 1;
    nb.Data.SetNum("executions", executions);
    
    if (nb.Money > 150) await nb.Buy("cpu");
    if (nb.Money > 1000) await nb.Buy("botnet");
    nb.Log($"tick {nb.Ticks}: {nb.Money:0.} cr, level {nb.Level:0.}, executions {executions:0.}");
    await nb.Wait(30);
}