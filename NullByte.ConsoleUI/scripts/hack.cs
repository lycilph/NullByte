var target = api.Data.GetStr("param0", "webmail");

var duration_str = api.Data.GetStr("param1", "30");
var duration = Int32.Parse(duration_str);

var current_level = api.Data.GetNum("level");
if (current_level == 0)
{
    current_level = api.Level;
    api.Data.SetNum("level", current_level);
}

api.Log($"Target is {target} and duration {duration} ticks");

while (true)
{
    await api.Hack(target);

    var executions = api.Data.GetNum("executions") + 1;
    api.Data.SetNum("executions", executions);

    api.Log($"tick {api.Ticks}: {api.Money:0.} cr, level {api.Level:0.}, executions {executions:0.}");

    await api.Wait(duration);

    if (api.Level > current_level)
    {
        api.Log($"New level achieved: {current_level} -> {api.Level}");
        break;
    }
}