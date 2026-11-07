namespace NullByte.ConsoleUI.Terminal;

/// <summary>
/// Supplies tab completion to the terminal.
/// </summary>
/// <remarks>
/// Called from the terminal UI thread, so implementations must be thread-safe
/// and must never touch game state directly.
/// </remarks>
public interface ICompletionProvider
{
    CompletionResult Complete(string input, int cursorIndex);
}