using NullByte.Core.Domain;

namespace NullByte.ConsoleUI.Tests.Infrastructure;

internal static class TestGameFactory
{
    public static Server CreateServer(string hostname, bool hasRootAccess = true) => new(
        hostname,
        maximumMoney: 0,
        startingMoney: 0,
        baseDifficulty: 1,
        minimumSecurity: 1,
        startingSecurity: 1,
        requiredHackingLevel: 1,
        growthParameter: 0,
        maximumRam: 8,
        hasRootAccess: hasRootAccess);

    public static Network CreateNetwork(params Server[] servers)
    {
        var network = new Network();

        foreach (var server in servers)
            network.AddServer(server);

        return network;
    }

    public static GameState CreateGame(double money = 0, params Server[] additionalServers)
    {
        var home = CreateServer("home");
        var network = CreateNetwork([home, .. additionalServers]);

        return new GameState(new Player("home", startingMoney: money), network);
    }
}
