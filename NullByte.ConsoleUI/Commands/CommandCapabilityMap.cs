namespace NullByte.ConsoleUI.Commands;

/// <summary>Maps console command definitions to the stable narrative capability that protects them.</summary>
internal static class CommandCapabilityMap
{
    private static readonly IReadOnlyDictionary<string, string> Capabilities =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["help"] = "host.controls",
            ["journal"] = "host.controls",
            ["exit"] = "host.controls",
            ["clear"] = "host.controls",
            ["vscode"] = "host.controls",
            ["save"] = "host.controls",
            ["load"] = "host.controls",
            ["saves"] = "host.controls",
            ["reset"] = "host.controls",

            ["status"] = "player.inspect",
            ["money"] = "player.profile",
            ["skills"] = "player.profile",

            ["scan"] = "network.scan",
            ["analyze"] = "network.analyze",
            ["connect"] = "network.connect",
            ["home"] = "network.connect",

            ["nuke"] = "server.root-access",
            ["backdoor"] = "server.backdoor",
            ["hack"] = "server.operations",
            ["grow"] = "server.operations",
            ["weaken"] = "server.operations",

            ["ls"] = "files.navigation",
            ["cd"] = "files.navigation",
            ["pwd"] = "files.navigation",
            ["cat"] = "files.read",
            ["rm"] = "files.manage",
            ["mkdir"] = "files.manage",
            ["rmdir"] = "files.manage",
            ["mv"] = "files.manage",
            ["scp"] = "files.manage",
            ["edit"] = "files.manage",

            ["run"] = "scripts.execute",
            ["mem"] = "scripts.inspect",
            ["free"] = "scripts.inspect",
            ["ps"] = "scripts.inspect",
            ["kill"] = "scripts.control",
            ["killall"] = "scripts.control",

            ["botnet"] = "botnet.inspect",
            ["botnet buy"] = "botnet.purchase",
            ["botnet upgrade"] = "botnet.upgrade",

            ["cloud"] = "cloud.inspect",
            ["cloud buy"] = "cloud.purchase",
            ["cloud upgrade"] = "cloud.manage",
            ["cloud rename"] = "cloud.manage",

            ["study"] = "activities.learn",
            ["train"] = "activities.learn",
            ["work"] = "activities.work",
            ["crime"] = "activities.crime",
            ["stop"] = "activities.manage",
            ["focus"] = "activities.manage",
            ["activity"] = "activities.manage"
        };

    public static string GetRequiredCapability(string commandName)
        => Capabilities.TryGetValue(commandName, out var capability)
            ? capability
            : throw new InvalidOperationException($"Command '{commandName}' has no narrative capability mapping.");
}
