using NullByte.Core.Domain;

namespace NullByte.Core.World;

public static class InitialGameStateFactory
{
    private const string HomeServerId = "home";
    private const double StartingMoney = 1000;

    private static readonly string NetworkDefinitionPath = Path.Combine("Data", "initial-network.json");

    public static GameState Create()
    {
        var network = LoadNetwork();

        var player = new Player(
            homeHostname: HomeServerId,
            startingMoney: StartingMoney);

        return new GameState(player, network);
    }

    private static Network LoadNetwork()
    {
        var path = Path.Combine(AppContext.BaseDirectory, NetworkDefinitionPath);
        if (!File.Exists(path))
            throw new FileNotFoundException("The initial network definition could not be found.", path);

        var json = File.ReadAllText(path);
        var loader = new NetworkLoader();
        return loader.Load(json);
    }
}