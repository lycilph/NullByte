using NullByte.ConsoleUI.Terminal;
using NullByte.Core.Services;

namespace NullByte.ConsoleUI.Tests;

internal sealed class FakeTerminal : IGameTerminal, IGameOutput
{
    public List<string> Lines { get; } = [];
    
    public CancellationToken StoppingToken => CancellationToken.None;

    public void StartProgress(string msg, Func<double> progress) { }
    public void StopProgress() { }
    public void SetPrompt(string prompt) { }
    public void SetInputEnabled(bool enabled) { }

    public async IAsyncEnumerable<string> ReadCommandsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    public void WriteLine() => Lines.Add(string.Empty);
    public void WriteLine(string message) => Lines.Add(message);
    public void Clear() => Lines.Clear();
}