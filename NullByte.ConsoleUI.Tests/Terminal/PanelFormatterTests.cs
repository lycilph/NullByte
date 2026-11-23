using NullByte.ConsoleUI.Terminal;
using Xunit;

namespace NullByte.ConsoleUI.Tests.Terminal;

public sealed class PanelFormatterTests
{
    private static readonly Hotkey Stop = new(ConsoleKey.S, () => { }, "[S] stop");
    private static readonly Hotkey Background = new(ConsoleKey.B, () => { }, "[B] background");
    private static readonly Hotkey Hidden = new(ConsoleKey.Escape, () => { });

    [Fact]
    public void A_known_progress_fills_the_bar()
    {
        var line = PanelFormatter.FormatBar("Hacking n00dles", 0.5, tick: 0);

        Assert.Equal("Hacking n00dles... [============            ] 50%", line);
    }

    [Theory]
    [InlineData(-1.0, "0%")]
    [InlineData(2.0, "100%")]
    [InlineData(double.NaN, "0%")]
    public void A_progress_outside_zero_to_one_is_clamped(double progress, string expected)
    {
        Assert.EndsWith(expected, PanelFormatter.FormatBar("x", progress, tick: 0));
    }

    [Fact]
    public void An_unknown_progress_shows_a_block_that_keeps_the_bar_the_same_width()
    {
        var widths = Enumerable.Range(0, 200)
            .Select(tick => PanelFormatter.FormatBar("Studying", null, tick))
            .Select(line => line.Length)
            .Distinct()
            .ToList();

        Assert.Single(widths);
    }

    [Fact]
    public void An_unknown_progress_moves_across_the_bar_and_back()
    {
        string Block(long tick)
        {
            var line = PanelFormatter.FormatBar("t", null, tick);
            return line[(line.IndexOf('[') + 1)..line.IndexOf(']')];
        }

        Assert.StartsWith("=====", Block(0));              // at the left edge
        Assert.EndsWith("=====", Block(19));               // at the right edge (24 - 5)
        Assert.StartsWith("=====", Block(38));             // and back again
        Assert.NotEqual(Block(0), Block(1));
        Assert.DoesNotContain("%", PanelFormatter.FormatBar("t", null, 3));
    }

    [Fact]
    public void The_detail_line_shows_the_hints_at_the_right_edge()
    {
        var line = PanelFormatter.FormatDetail("1m 05s | +$0.00", [Stop, Background, Hidden], width: 60);

        Assert.Equal(60, line.Length);
        Assert.StartsWith("1m 05s | +$0.00", line);
        Assert.EndsWith("[S] stop  [B] background", line);
    }

    [Fact]
    public void Hotkeys_without_a_hint_are_not_shown()
    {
        Assert.Equal("details", PanelFormatter.FormatDetail("details", [Hidden], width: 60));
    }

    [Fact]
    public void When_the_line_is_too_narrow_the_details_are_cut_and_the_hints_stay()
    {
        var line = PanelFormatter.FormatDetail(new string('x', 100), [Stop, Background], width: 50);

        Assert.Equal(50, line.Length);
        Assert.EndsWith("[S] stop  [B] background", line);
    }

    [Fact]
    public void When_not_even_the_hints_fit_only_the_hints_are_shown()
    {
        Assert.Equal("[S] stop  [B] background", PanelFormatter.FormatDetail("details", [Stop, Background], width: 10));
    }
}