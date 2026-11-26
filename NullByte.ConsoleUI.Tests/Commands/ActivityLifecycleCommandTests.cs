using NullByte.Core.Domain;
using NullByte.Core.Domain.Activities;

namespace NullByte.ConsoleUI.Tests.Commands;

/// <summary>Focus, background, stop and the hotkeys of the live panel, which are the same for every kind of activity.</summary>
public sealed class ActivityLifecycleCommandTests : ActivityCommandsTestBase
{
    [Fact]
    public async Task A_focused_activity_holds_the_console_and_shows_a_live_panel()
    {
        var command = Run("study algorithms");

        Assert.False(command.IsCompleted);
        Assert.NotNull(Terminal.Panel);
        Assert.Equal("Studying algorithms at plain-university", Terminal.Panel!.Title);
        Assert.Null(Terminal.Panel.Progress());                       // indeterminate

        Advance(TenSeconds);

        Assert.Contains("hacking", Terminal.Panel.Detail());
        Assert.Contains("-$100.00", Terminal.Panel.Detail());

        Terminal.Press(ConsoleKey.S);
        await command.WaitAsync(Timeout, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task S_stops_the_activity_and_prints_a_summary()
    {
        var command = Run("study algorithms");
        Advance(TenSeconds);

        Assert.True(Terminal.Press(ConsoleKey.S));
        await command.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        Assert.Null(Service.Current);
        Assert.Null(Terminal.Panel);
        Assert.Contains("Stopped. Studying algorithms at plain-university: 10s, -$100.00, +20.00 hacking xp.", Terminal.Text);
    }

    [Fact]
    public async Task Ctrl_C_also_stops_the_activity()
    {
        var command = Run("study algorithms");

        Assert.True(Terminal.Press(ConsoleKey.C, ConsoleModifiers.Control));
        await command.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        Assert.Null(Service.Current);
    }

    [Theory]
    [InlineData(ConsoleKey.B)]
    [InlineData(ConsoleKey.Escape)]
    public async Task B_and_Escape_send_the_activity_to_the_background_where_it_keeps_running(ConsoleKey key)
    {
        var command = Run("study algorithms");

        Assert.True(Terminal.Press(key));
        await command.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        Assert.Equal(ActivityMode.Background, Service.Current!.Mode);
        Assert.Null(Terminal.Panel);
        Assert.Contains("continues in the background", Terminal.Text);

        Advance(TenSeconds);   // background: 80% of 20 xp

        Assert.Equal(16, State.Player.Skills.GetExperience(SkillKind.Hacking), precision: 6);
    }

    [Fact]
    public async Task A_hotkey_that_is_not_on_the_panel_does_nothing()
    {
        var command = Run("study algorithms");

        Assert.False(Terminal.Press(ConsoleKey.X));
        Assert.False(command.IsCompleted);

        Terminal.Press(ConsoleKey.S);
        await command.WaitAsync(Timeout, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_focused_activity_that_cannot_be_paid_for_any_more_ends_the_command_by_itself()
    {
        State.Player.TrySpendMoney(State.Player.Money - 15);
        var command = Run("study algorithms");

        Advance(TimeSpan.FromSeconds(5));                     // $50 of tuition, only $15 available

        await command.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        Assert.Null(Service.Current);
        Assert.Null(Terminal.Panel);
        Assert.Contains("You can no longer afford it", Terminal.Text);
    }

    [Fact]
    public async Task A_background_activity_that_cannot_be_paid_for_any_more_says_so()
    {
        State.Player.TrySpendMoney(State.Player.Money - 15);
        await Run("study algorithms -b");

        Advance(TimeSpan.FromSeconds(5));

        Assert.Null(Service.Current);
        Assert.Contains("You can no longer afford it", Terminal.Text);
    }

    [Fact]
    public async Task Stop_ends_a_background_activity_and_prints_the_summary()
    {
        await Run("study algorithms -b");
        Advance(TenSeconds);

        await Run("stop");

        Assert.Null(Service.Current);
        Assert.Contains("Stopped. Studying algorithms at plain-university: 10s, -$100.00, +16.00 hacking xp.", Terminal.Text);
    }

    [Fact]
    public async Task Stop_when_idle_says_so()
    {
        await Run("stop");

        Assert.Contains("You are not doing anything.", Terminal.Text);
    }

    [Fact]
    public async Task Focus_takes_over_a_background_activity_and_holds_the_console()
    {
        await Run("study algorithms -b");

        var command = Run("focus");

        Assert.False(command.IsCompleted);
        Assert.Equal(ActivityMode.Focused, Service.Current!.Mode);
        Assert.NotNull(Terminal.Panel);

        Terminal.Press(ConsoleKey.S);
        await command.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        Assert.Null(Service.Current);
    }

    [Fact]
    public async Task Focus_when_idle_says_so()
    {
        await Run("focus");

        Assert.Contains("You are not doing anything.", Terminal.Text);
        Assert.Null(Terminal.Panel);
    }

    [Fact]
    public async Task Activity_shows_the_mode_rates_and_progress()
    {
        await Run("study algorithms -b");
        Advance(TenSeconds);

        await Run("activity");

        Assert.Contains("Mode: in the background (rewards reduced by 20%)", Terminal.Text);
        Assert.Contains("Now: costs $10.00/s, +1.60 hacking xp/s", Terminal.Text);
        Assert.Contains("So far: 10s | -$100.00 | +16.00 hacking xp", Terminal.Text);
    }

    [Fact]
    public async Task Activity_when_idle_says_so()
    {
        await Run("activity");

        Assert.Contains("You are not doing anything.", Terminal.Text);
    }

    [Fact]
    public async Task Skills_lists_every_skill_with_its_level_and_experience()
    {
        await Run("train strength -b");
        Advance(TimeSpan.FromSeconds(100));

        await Run("skills");

        foreach (var name in new[] { "hacking", "strength", "defense", "dexterity", "agility", "charisma" })
            Assert.Contains(name, Terminal.Text);

        Assert.Contains("80.00", Terminal.Text);       // 100 s at 0.8 xp/s
    }
}
