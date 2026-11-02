var target = nb.Data.GetStr("param0", "webmail");
nb.Log($"Target is {target}");
await nb.Hack(target);
await nb.Wait(30);
nb.Log("Script completed...");