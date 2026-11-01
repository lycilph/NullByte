var target = api.Data.GetStr("param0", "webmail");
api.Log($"Target is {target}");
await api.Hack(target);
await api.Wait(30);
api.Log("Script completed...");