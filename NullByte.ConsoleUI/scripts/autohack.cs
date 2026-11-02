// uses: misc, print

var targets = nb.ScanNetwork();
PrintTargets(nb, targets, "All");

var eligeble = GetEligebleTargets(nb);
PrintTargets(nb, eligeble, "Eligeble");