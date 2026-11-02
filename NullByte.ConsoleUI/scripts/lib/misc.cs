public List<Target> GetEligebleTargets(NullByteApi nb)
    => nb.ScanNetwork().Where(t => t.MinLevel <= nb.Level).ToList();