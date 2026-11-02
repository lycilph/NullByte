var target = nb.Data.GetStr("param0", "webmail");

var duration_str = nb.Data.GetStr("param1", "30");
var duration = Int32.Parse(duration_str);

var current_level = nb.Data.GetNum("level");
if (current_level == 0)
{
    current_level = nb.Level;
    nb.Data.SetNum("level", current_level);
}

nb.Log($"Target is {target} and duration {duration} ticks");

while (true)
{
    await nb.Hack(target);

    var executions = nb.Data.GetNum("executions") + 1;
    nb.Data.SetNum("executions", executions);

    nb.Log($"tick {nb.Ticks}: {nb.Money:0.} cr, level {nb.Level:0.}, executions {executions:0.}");

    await nb.Wait(duration);

    if (nb.Level > current_level)
    {
        nb.Log($"New level achieved: {current_level} -> {nb.Level}");
        break;
    }
}