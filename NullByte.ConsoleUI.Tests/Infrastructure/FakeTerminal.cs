using System.Runtime.CompilerServices;
using NullByte.ConsoleUI.Terminal;

namespace NullByte.ConsoleUI.Tests.Infrastructure;

/// <summary>
/// The terminal for tests: it records what is written and lets a test press the hotkeys of the live panel.
/// </summary>
internal sealed class FakeTerminal : IGameTerminal
{
    private readonly List<string> lines = [];

    public CancellationToken StoppingToken => CancellationToken.None;

    /// <summary>The panel that is showing right now, or null.</summary>
    public LivePanel? Panel { get; private set; }

    /// <summary>A snapshot of everything written so far, one entry per row.</summary>
    public IReadOnlyList<string> Lines
    {
        get
        {
            lock (lines)
                return lines.ToArray();
        }
    }

    /// <summary>Everything written so far, one line per row.</summary>
    public string Text
    {
        get
        {
            lock (lines)
                return string.Join('\n', lines);
        }
    }

    public void WriteLine(string message)
    {
        lock (lines)
            lines.Add(message);
    }

    public void WriteLine() => WriteLine(string.Empty);

    public void Clear()
    {
        lock (lines)
            lines.Clear();
    }

    public IDisposable ShowLivePanel(LivePanel panel)
    {
        Panel = panel;
        return new Scope(this, panel);
    }

    /// <summary>Presses a key the way the terminal would: only a matching hotkey of the showing panel reacts.</summary>
    /// <returns>Whether a hotkey reacted.</returns>
    public bool Press(ConsoleKey key, ConsoleModifiers modifiers = 0)
    {
        var info = new ConsoleKeyInfo('\0', key,
            shift: modifiers.HasFlag(ConsoleModifiers.Shift),
            alt: modifiers.HasFlag(ConsoleModifiers.Alt),
            control: modifiers.HasFlag(ConsoleModifiers.Control));

        var hotkey = Panel?.Hotkeys.FirstOrDefault(h => h.Matches(info));
        hotkey?.Pressed();

        return hotkey is not null;
    }

    public void StartProgress(string msg, Func<double> progress) { }
    public void StopProgress() { }
    public void SetPrompt(string prompt) { }
    public void SetInputEnabled(bool enabled) { }
    public void SuspendProcessing() { }
    public void ResumeProcessing() { }

    public async IAsyncEnumerable<string> ReadCommandsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    private sealed class Scope(FakeTerminal terminal, LivePanel panel) : IDisposable
    {
        public void Dispose()
        {
            if (ReferenceEquals(terminal.Panel, panel))
                terminal.Panel = null;
        }
    }
}
