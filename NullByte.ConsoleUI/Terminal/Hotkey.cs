namespace NullByte.ConsoleUI.Terminal;

/// <summary>
/// A key that does something while a command owns the console (input is
/// disabled), such as stopping what the player is doing.
/// </summary>
/// <remarks>
/// <see cref="Pressed"/> is called on the terminal's UI thread, so it must be
/// quick and thread-safe: it should signal, not do the work.
/// </remarks>
/// <param name="Key">The key.</param>
/// <param name="Pressed">What to do when the key is pressed.</param>
/// <param name="Hint">
/// How the key is shown to the player, e.g. <c>[S] stop</c>. Null hides it
/// (for an alternative key that is not worth the screen space).
/// </param>
/// <param name="Modifiers">Ctrl and Alt must match exactly. Shift is ignored.</param>
public sealed record Hotkey(
    ConsoleKey Key,
    Action Pressed,
    string? Hint = null,
    ConsoleModifiers Modifiers = 0)
{
    private const ConsoleModifiers Significant = ConsoleModifiers.Control | ConsoleModifiers.Alt;

    /// <summary>Gets whether a pressed key is this hotkey.</summary>
    public bool Matches(ConsoleKeyInfo pressed)
        => pressed.Key == Key && (pressed.Modifiers & Significant) == (Modifiers & Significant);
}