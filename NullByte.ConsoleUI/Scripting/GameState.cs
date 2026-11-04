namespace NullByte.ConsoleUI.Scripting;

public sealed class GameState
{
    public decimal PlayerMoney { get; set; }

    public ServerState Server { get; } = new()
    {
        Hostname = "foodmart",
        Money = 100_000m,
        MaxMoney = 100_000m,
        Security = 10
    };
}