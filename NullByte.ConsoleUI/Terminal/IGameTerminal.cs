namespace NullByte.ConsoleUI.Terminal;

public interface IGameTerminal
{
    CancellationToken StoppingToken { get; }

    void WriteLine();
    void WriteLine(string message);
    void Clear();
}