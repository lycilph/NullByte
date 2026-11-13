using NullByte.Core.Services;

namespace NullByte.ConsoleUI.Terminal;

public interface IGameTerminal : IGameOutput
{
    CancellationToken StoppingToken { get; }

    void StartProgress(string msg, Func<double> progress);
    void StopProgress();


    void SetPrompt(string prompt);
    void SetInputEnabled(bool enabled);

    IAsyncEnumerable<string> ReadCommandsAsync(CancellationToken cancellationToken = default);

    void Clear();
}