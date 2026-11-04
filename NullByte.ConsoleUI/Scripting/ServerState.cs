namespace NullByte.ConsoleUI.Scripting;

public sealed class ServerState
{
    public required string Hostname { get; init; }
    public decimal Money { get; set; }
    public decimal MaxMoney { get; set; }
    public double Security { get; set; }
}