namespace NullByte.Core.Services;

/// <summary>
/// All text that the game output will go through this
/// </summary>
/// <remarks>
/// The core scripting API knows nothing about the concrete user interface.
/// ConsoleUI can write this output to the terminal, while a later WPF host
/// can provide its own implementation.
/// This must be thread-safe
/// </remarks>
public interface IGameOutput
{
    void WriteLine(string message);
    void WriteLine();
}
