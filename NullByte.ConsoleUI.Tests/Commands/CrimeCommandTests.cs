using NullByte.Core.Domain;
using NullByte.Core.Domain.Activities;

namespace NullByte.ConsoleUI.Tests.Commands;

/// <summary>Committing crimes from the console.</summary>
public sealed class CrimeCommandTests : ActivityCommandsTestBase
{
    [Fact]
    public async Task Crime_without_arguments_lists_the_crimes_with_the_odds_and_the_rewards()
    {
        await Run("crime");

        Assert.Contains("sure-thing", Terminal.Text);
        Assert.Contains("100%", Terminal.Text);
        Assert.Contains("coin-flip", Terminal.Text);
        Assert.Contains("50%", Terminal.Text);
        Assert.Contains("long-shot", Terminal.Text);
        Assert.Contains("1%", Terminal.Text);
        Assert.Contains("+$100.00, +4.00 strength xp, +2.00 agility xp", Terminal.Text);
        Assert.Contains("Usage: crime <name> [-b]", Terminal.Text);
        Assert.Null(Service.Current);
    }

    [Fact]
    public async Task A_crime_in_the_background_returns_at_once_and_does_not_print_every_attempt()
    {
        await Run("crime coin-flip -b");

        Assert.Contains("Committing coin-flip (4s per attempt, 50% chance, +$400.00, +6.40 dexterity xp on success).", Terminal.Text);
        Assert.Equal(ActivityMode.Background, Service.Current!.Mode);
        Assert.Null(Terminal.Panel);

        Advance(TimeSpan.FromSeconds(12));                   // three attempts

        Assert.DoesNotContain("coin-flip: success", Terminal.Text);
        Assert.Equal(3, Service.Current!.Attempts);
    }

    [Fact]
    public async Task A_failed_attempt_is_shown_as_such()
    {
        Random.Enqueue(0.9);
        var command = Run("crime coin-flip");

        Advance(TimeSpan.FromSeconds(4));

        Assert.Contains("coin-flip: failed (50% chance): no money, +2.00 dexterity xp", Terminal.Text);

        Terminal.Press(ConsoleKey.S);
        await command.WaitAsync(Timeout, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Sending_a_crime_to_the_background_stops_the_attempt_lines_but_not_the_crime()
    {
        var command = Run("crime sure-thing");
        Advance(TimeSpan.FromSeconds(2));

        Terminal.Press(ConsoleKey.B);
        await command.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        var linesBefore = Terminal.Text.Split('\n').Count(l => l.StartsWith("sure-thing: success", StringComparison.Ordinal));

        Advance(TimeSpan.FromSeconds(4));                    // two more attempts, in the background

        var linesAfter = Terminal.Text.Split('\n').Count(l => l.StartsWith("sure-thing: success", StringComparison.Ordinal));
        Assert.Equal(1, linesBefore);
        Assert.Equal(1, linesAfter);
        Assert.Equal(3, Service.Current!.Attempts);
    }

    [Fact]
    public async Task Focus_brings_a_background_crime_back_to_a_progress_bar()
    {
        await Run("crime sure-thing -b");
        Advance(TimeSpan.FromSeconds(1));

        var command = Run("focus");

        Assert.Equal(0.5, Terminal.Panel!.Progress()!.Value, precision: 6);       // the attempt in progress carries on

        Terminal.Press(ConsoleKey.S);
        await command.WaitAsync(Timeout, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("crime nope", "Unknown crime 'nope'")]
    [InlineData("crime sure-thing coin-flip", "Usage: crime [name] [-b]")]
    [InlineData("crime sure-thing -x", "Unknown option '-x'")]
    public async Task Problems_with_a_crime_are_explained_and_nothing_starts(string commandLine, string expected)
    {
        await Run(commandLine);

        Assert.Contains(expected, Terminal.Text);
        Assert.Null(Service.Current);
    }

    [Fact]
    public async Task Activity_shows_the_attempts_the_chance_and_the_reward_of_a_crime()
    {
        await Run("crime coin-flip -b");
        Advance(TimeSpan.FromSeconds(12));                                 // three attempts, 19.2 xp: dexterity level 2

        await Run("activity");

        Assert.Contains("Committing coin-flip", Terminal.Text);
        Assert.Contains("Attempts: 3 (3 succeeded)", Terminal.Text);
        Assert.Contains("Chance now: 100%", Terminal.Text);                      // was 50% at level 1
        Assert.Contains("On success: +$400.00, +6.40 dexterity xp", Terminal.Text);
        Assert.DoesNotContain("Now: ", Terminal.Text);
    }

    [Fact]
    public async Task A_focused_crime_shows_a_bar_that_fills_and_starts_over_and_a_line_for_every_attempt()
    {
        var command = Run("crime sure-thing");

        Assert.False(command.IsCompleted);
        Assert.Equal("Committing sure-thing", Terminal.Panel!.Title);
        Assert.Equal(0.0, Terminal.Panel.Progress()!.Value, precision: 6);

        Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(0.5, Terminal.Panel.Progress()!.Value, precision: 6);

        Advance(TimeSpan.FromSeconds(1));                    // the attempt ends and the next one begins

        Assert.Equal(0.0, Terminal.Panel.Progress()!.Value, precision: 6);
        Assert.Contains("sure-thing: success (100% chance): +$100.00, +4.00 strength xp, +2.00 agility xp", Terminal.Text);
        Assert.Contains("1/1 succeeded", Terminal.Panel.Detail());

        Terminal.Press(ConsoleKey.S);
        await command.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        Assert.Null(Service.Current);
        Assert.Contains("Stopped. Committing sure-thing: 2s, +$100.00, +4.00 strength xp, +2.00 agility xp, 1 of 1 succeeded.", Terminal.Text);
    }
}
