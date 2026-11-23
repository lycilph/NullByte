namespace NullByte.ConsoleUI.Terminal;

/// <summary>
/// What the terminal shows while a long-running command owns the console: a
/// progress bar, a line of live details and the keys that are active.
/// </summary>
/// <remarks>
/// The functions are called repeatedly on the terminal's UI thread, so they
/// may only read data that is safe to read from another thread.
/// </remarks>
/// <param name="Title">What is going on, e.g. "Studying algorithms at rothman-university".</param>
/// <param name="Progress">A fraction from 0 to 1, or null when the duration is not known (an animated bar is shown).</param>
/// <param name="Detail">A line of live details, e.g. what has been earned so far.</param>
/// <param name="Hotkeys">The keys that work while the panel is shown.</param>
public sealed record LivePanel(
    string Title,
    Func<double?> Progress,
    Func<string> Detail,
    IReadOnlyList<Hotkey> Hotkeys);