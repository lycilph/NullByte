var target = Data.GetStr("param0", "webmail");

var duration_str = Data.GetStr("param1", "30");
var duration = Int32.Parse(duration_str);

var current_level = Data.GetNum("level");
if (current_level == 0)
{
    current_level = Level;
    Data.SetNum("level", current_level);
}

Log($"Target is {target} and duration {duration} ticks");

while (true)
{
    await Hack(target);

    var executions = Data.GetNum("executions") + 1;
    Data.SetNum("executions", executions);

    Log($"tick {Ticks}: {Money:0.} cr, level {Level:0.}, executions {executions:0.}");

    await Wait(duration);

    if (Level > current_level)
    {
        Log($"New level achieved: {current_level} -> {Level}");
        break;
    }
}