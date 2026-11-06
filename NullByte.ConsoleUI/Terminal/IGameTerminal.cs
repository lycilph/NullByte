namespace NullByte.ConsoleUI.Terminal;

public interface IGameTerminal
{
    CancellationToken StoppingToken { get; }

    void SetPrompt(string prompt);
    void SetInputEnabled(bool enabled);

    void WriteLine();
    void WriteLine(string message);
    void Clear();
}