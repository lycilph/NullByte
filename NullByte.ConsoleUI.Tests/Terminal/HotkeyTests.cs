using NullByte.ConsoleUI.Terminal;

namespace NullByte.ConsoleUI.Tests.Terminal;

public sealed class HotkeyTests
{
    private static ConsoleKeyInfo Press(ConsoleKey key, bool shift = false, bool alt = false, bool control = false)
        => new('\0', key, shift, alt, control);

    [Fact]
    public void A_plain_hotkey_matches_its_key_only()
    {
        var hotkey = new Hotkey(ConsoleKey.S, () => { });

        Assert.True(hotkey.Matches(Press(ConsoleKey.S)));
        Assert.False(hotkey.Matches(Press(ConsoleKey.B)));
    }

    [Fact]
    public void Shift_is_ignored()
    {
        var hotkey = new Hotkey(ConsoleKey.S, () => { });

        Assert.True(hotkey.Matches(Press(ConsoleKey.S, shift: true)));
    }

    [Fact]
    public void Control_and_alt_must_match_exactly()
    {
        var plain = new Hotkey(ConsoleKey.S, () => { });
        var control = new Hotkey(ConsoleKey.C, () => { }, Modifiers: ConsoleModifiers.Control);

        Assert.False(plain.Matches(Press(ConsoleKey.S, control: true)));
        Assert.False(plain.Matches(Press(ConsoleKey.S, alt: true)));
        Assert.True(control.Matches(Press(ConsoleKey.C, control: true)));
        Assert.False(control.Matches(Press(ConsoleKey.C)));
    }
}