nb.Log("Testing a log message");
nb.Log($"Reading current money: {nb.Money}");
nb.Log($"Reading level: {nb.Level}");
await nb.Hack("webmail");
await nb.Wait(30);
nb.Log("Hack completed");