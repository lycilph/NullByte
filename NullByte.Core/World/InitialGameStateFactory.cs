using NullByte.Core.Domain;

public static class InitialGameStateFactory
{
    private const string HomeServerId = "home";
    private const double StartingMoney = 0;

    public static GameState Create()
    {
        Network network = CreateNetwork();

        var player = new Player(
            homeServerId: HomeServerId,
            startingMoney: StartingMoney);

        return new GameState(
            player,
            network);
    }

    private static Network CreateNetwork()
    {
        var home =
                    new Server(
                        id: "home",
                        hostname: "home",
                        maximumMoney: 0,
                        minimumSecurity: 0,
                        startingMoney: 0,
                        startingSecurity: 0);

        var n00dles =
            new Server(
                id: "server-n00dles",
                hostname: "n00dles",
                maximumMoney: 50_000,
                minimumSecurity: 1,
                startingMoney: 25_000,
                startingSecurity: 5);

        var foodNStuff =
            new Server(
                id: "server-foodnstuff",
                hostname: "foodnstuff",
                maximumMoney: 100_000,
                minimumSecurity: 2,
                startingMoney: 60_000,
                startingSecurity: 8);

        var sigmaCosmetics =
            new Server(
                id: "server-sigma-cosmetics",
                hostname: "sigma-cosmetics",
                maximumMoney: 150_000,
                minimumSecurity: 3,
                startingMoney: 75_000,
                startingSecurity: 10);

        var network = new Network();
        network.AddServer(home);
        network.AddServer(n00dles);
        network.AddServer(foodNStuff);
        network.AddServer(sigmaCosmetics);

        network.Connect(home.Id, n00dles.Id);
        network.Connect(home.Id, foodNStuff.Id);
        network.Connect(n00dles.Id, sigmaCosmetics.Id);
        return network;
    }
}