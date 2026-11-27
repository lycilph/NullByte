using NullByte.Core.Domain;

namespace NullByte.Core.World;

public static class InitialGameStateFactory
{
    private const string HomeHostname = "home";
    private const double StartingMoney = 1000;

    private static readonly string NetworkDefinitionPath = Path.Combine("Data", "initial-network.json");

    public static GameState Create(NarrativeProgressionMode narrativeMode = NarrativeProgressionMode.Tutorial)
    {
        var network = LoadNetwork();

        var homeServer = network.GetServer(HomeHostname);

        var player = new Player(
            homeServer.Id,
            startingMoney: StartingMoney);

        var state = new GameState(player, network);
        state.Narrative.Mode = narrativeMode;
        return state;
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