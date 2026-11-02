public void PrintTargets(NullByteApi nb, List<Target> targets, string msg)
{
    nb.Log(msg);
    foreach (var t in targets)
        nb.Log($"  {t.Id,-14} {t.Name,-22} [lvl: {t.MinLevel}, cpu: {t.RequiredCpu}, ram: {t.RequiredRam}]");
}