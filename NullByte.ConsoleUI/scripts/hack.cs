var target = Data.GetStr("param0", "webmail");

var duration_str = Data.GetStr("param1", "30");
var duration = Int32.Parse(duration_str);

Log($"Target is {target} and duration {duration} ticks");

while (true)
{
    await Hack(target);

    var executions = Data.GetNum("executions") + 1;
    Data.SetNum("executions", executions);

    Log($"tick {Ticks}: {Money:0.} cr, level {Level:0.}, executions {executions:0.}");

    await Wait(duration);
}