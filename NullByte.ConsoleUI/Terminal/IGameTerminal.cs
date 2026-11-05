namespace NullByte.ConsoleUI.Terminal;

public interface IGameTerminal
{
    CancellationToken StoppingToken { get; }

    public void SetPrompt(string newPrompt);

    void WriteLine();
    void WriteLine(string message);
    void Clear();
}