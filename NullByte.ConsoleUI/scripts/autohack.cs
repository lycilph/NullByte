// uses: misc, print

var targets = nb.ScanNetwork();
PrintTargets(nb, targets, "All");

var eligeble = GetEligebleTargets(nb);
PrintTargets(nb, eligeble, "Eligeble");

var selected = eligeble.OrderByDescending(t => t.MinLevel).First();
nb.Log($"Target found: {selected.Name}");

var max_jobs = nb.MaxConcurrentJobs;
nb.Log($"Max concurrency: {max_jobs}");

var current_level = nb.Level;
var iteration = 1;
while (true)
{
    nb.Log($"Iteration {iteration}");
    iteration++;

    var heat = nb.TargetHeat(selected.Id);
    var current_money = nb.TargetMoney(selected.Id);
    var max_money = nb.TargetMax(selected.Id);

    nb.Log($" * Target heat: {heat}");
    nb.Log($" * Target money: {current_money} of {max_money}");

    if (heat > 35)
    {
        var duration = nb.TargetScrubDuration(selected.Id);
        nb.Log($"Target is too hot, starting scrub [duration {duration}]");
        await nb.Scrub(selected.Id);
        await nb.Wait(duration+1);
        nb.Log($" * Scrub done, target heat is now: {nb.TargetHeat(selected.Id)}");
    }
    else if (current_money <  max_money*0.5)
    {
        var duration = nb.TargetGrowDuration(selected.Id);
        nb.Log($"Target is depleted, starting grow [duration {duration}]");
        List<Task> jobs = [];
        for (int i = 0; i < max_jobs; i++)
        {
            jobs.Add(nb.Grow(selected.Id));
            nb.Log($"Scheduling grow job {i}");
        }
        await Task.WhenAll(jobs);
        await nb.Wait(duration + 1);
        nb.Log($" * Grow done, target heat is now: {nb.TargetHeat(selected.Id)}");
    }
    else
    {
        var duration = nb.TargetHackDuration(selected.Id);
        nb.Log($"Target is ready, starting hack [duration {duration}]");
        List<Task> jobs = [];
        for (int i = 0; i < max_jobs; i++)
        {
            jobs.Add(nb.Hack(selected.Id));
            nb.Log($"Scheduling hack job {i}");
        }
        await Task.WhenAll(jobs);
        await nb.Wait(duration + 1);
        nb.Log($" * Hack done, target heat is now: {nb.TargetHeat(selected.Id)}");
    }

    if (nb.Level > current_level)
    {
        nb.Log($"New level achieved: {current_level} -> {nb.Level}");
        break;
    }
}