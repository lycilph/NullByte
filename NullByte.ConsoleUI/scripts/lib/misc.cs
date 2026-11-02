public List<Target> GetEligebleTargets(NullByteApi nb)
    => nb.ScanNetwork().Where(
        t => t.MinLevel <= nb.Level 
             && t.RequiredCpu <= nb.CpuLevel 
             && t.RequiredRam <= nb.RamLevel).ToList();