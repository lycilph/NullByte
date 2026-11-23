namespace NullByte.ConsoleUI.Terminal;

/// <summary>
/// Builds the text lines of a <see cref="LivePanel"/>.
/// </summary>
public static class PanelFormatter
{
    public const int BarWidth = 24;

    private const int MarqueeWidth = 5;

    /// <summary>
    /// Formats the bar line: a progress bar when the progress is known, otherwise a
    /// block that bounces from side to side as the tick advances.
    /// </summary>
    public static string FormatBar(string title, double? progress, long tick)
    {
        if (progress is { } known)
        {
            var fraction = double.IsNaN(known) ? 0 : Math.Clamp(known, 0.0, 1.0);
            var filled = (int)Math.Round(fraction * BarWidth);

            // Not {fraction:P0}: that puts a space before the % in some cultures ("50 %").
            return $"{title}... [{new string('=', filled)}{new string(' ', BarWidth - filled)}] {(int)Math.Round(fraction * 100)}%";
        }

        var travel = BarWidth - MarqueeWidth;
        var step = (int)(Math.Abs(tick) % (2 * travel));
        var position = step <= travel ? step : 2 * travel - step;

        return $"{title}... [{new string(' ', position)}{new string('=', MarqueeWidth)}{new string(' ', travel - position)}]";
    }

    /// <summary>
    /// Formats the detail line, with the hotkey hints at the right edge. When the line is
    /// too narrow it is the details that are cut, never the hints.
    /// </summary>
    public static string FormatDetail(string detail, IReadOnlyList<Hotkey> hotkeys, int width)
    {
        var hints = string.Join("  ", hotkeys.Select(h => h.Hint).Where(h => !string.IsNullOrEmpty(h)));

        if (hints.Length == 0)
            return detail;

        const string gap = "   ";
        var room = width - hints.Length - gap.Length;

        if (room <= 0)
            return hints;

        if (detail.Length > room)
            detail = detail[..room];

        return detail.PadRight(room) + gap + hints;
    }
}