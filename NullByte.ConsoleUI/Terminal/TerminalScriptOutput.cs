using NullByte.Core.Scripting;

namespace NullByte.ConsoleUI.Terminal;

/// <summary>
/// Forwards output from player scripts to the game terminal.
/// </summary>
public sealed class TerminalScriptOutput : IScriptOutput
{
    private readonly IGameTerminal terminal;

    public TerminalScriptOutput(IGameTerminal terminal)
    {
        ArgumentNullException.ThrowIfNull(terminal);
        this.terminal = terminal;
    }

    public void WriteLine(string message) => terminal.WriteLine(message);
}