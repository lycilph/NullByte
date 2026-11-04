namespace NullByte.ConsoleUI.Scripting;

public sealed record ServerInfo(
    string Hostname,
    decimal Money,
    decimal MaxMoney,
    double Security);