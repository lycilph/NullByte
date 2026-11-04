namespace NullByte.ConsoleUI.Scripting;

public sealed class ScriptApi : IScriptApi
{
    private readonly GameState game;
    private readonly IGameTerminal terminal;

    public ScriptApi(GameState game, IGameTerminal terminal)
    {
        this.game = game;
        this.terminal = terminal;
    }

    public ServerInfo GetServer(string hostname)
    {
        var server = FindServer(hostname);

        return new ServerInfo(
            server.Hostname,
            server.Money,
            server.MaxMoney,
            server.Security);
    }

    public async Task<HackResult> HackAsync(
        string hostname,
        CancellationToken cancellationToken = default)
    {
        var server = FindServer(hostname);

        Log($"Starting hack against {hostname}...");

        await Task.Delay(
            TimeSpan.FromSeconds(3),
            cancellationToken);

        var stolen = Math.Min(
            server.Money,
            1_000m);

        server.Money -= stolen;
        server.Security += 0.1;

        game.PlayerMoney += stolen;

        Log($"Hack completed. Stole {stolen:C}.");

        return new HackResult(
            true,
            stolen);
    }

    public void Log(string message) =>
        terminal.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");

    private ServerState FindServer(string hostname)
    {
        if (!game.Server.Hostname.Equals(
                hostname,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Server '{hostname}' does not exist.");
        }

        return game.Server;
    }
}