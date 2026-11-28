using NullByte.Core.Services;

namespace NullByte.ConsoleUI.Terminal;

public interface IGameTerminal : IGameOutput
{
    CancellationToken StoppingToken { get; }

    void SuspendProcessing();
    void ResumeProcessing();

    void StartProgress(string msg, Func<double> progress);
    void StopProgress();

    /// <summary>
    /// Shows a live panel (progress, details and active hotkeys) until the returned scope is disposed.
    /// Hotkeys only work while input is disabled, which is while a command owns the console.
    /// </summary>
    IDisposable ShowLivePanel(LivePanel panel);

    void SetPrompt(string prompt);
    void SetInputEnabled(bool enabled);

    IAsyncEnumerable<string> ReadCommandsAsync(CancellationToken cancellationToken = default);

    void WriteNarrativeLine(string message);

    void Clear();
}