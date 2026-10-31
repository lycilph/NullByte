var target = Data.GetStr("param0", "webmail");
Log($"Target is {target}");
await Hack(target);
await Wait(30);
Log("Script completed...");