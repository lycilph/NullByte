namespace NullByte.Core.Scripting;

/// <summary>
/// Receives text written by a running player script.
/// </summary>
/// <remarks>
/// The core scripting API knows nothing about the concrete user interface.
/// ConsoleUI can write this output to the terminal, while a later WPF host
/// can provide its own implementation.
/// </remarks>
public interface IScriptOutput
{
    void WriteLine(string message);
}