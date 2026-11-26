using NullByte.Core.Domain;
using NullByte.Core.Domain.Activities;

namespace NullByte.ConsoleUI.Tests.Commands;

/// <summary>Working at a company from the console.</summary>
public sealed class WorkCommandTests : ActivityCommandsTestBase
{
    [Fact]
    public async Task Work_without_arguments_lists_the_companies_with_their_entry_requirements()
    {
        await Run("work");

        Assert.Contains("testcorp (reputation 0.00)", Terminal.Text);
        Assert.Contains("software   entry: Intern (no requirements)", Terminal.Text);
        Assert.Contains("hardcorp", Terminal.Text);
        Assert.Contains("entry: Hard Intern (hacking level 5)", Terminal.Text);
        Assert.Contains("Usage: work <company> [career] [-b]", Terminal.Text);
        Assert.Null(Service.Current);
    }

    [Fact]
    public async Task The_listing_shows_where_the_player_stands()
    {
        await Run("work testcorp software -b");
        Advance(TenSeconds);
        Terminal.WriteLine("-----");

        await Run("work");

        var listing = Terminal.Text[Terminal.Text.IndexOf("-----", StringComparison.Ordinal)..];
        Assert.Contains("testcorp (reputation 8.00)", listing);
        Assert.Contains("you are Intern; next: Developer (hacking level 3, reputation 10)", listing);
    }

    [Fact]
    public async Task Working_in_the_background_hires_the_player_and_returns_at_once()
    {
        await Run("work testcorp software -b");

        Assert.Contains("Hired as Intern.", Terminal.Text);
        Assert.Contains("Working at testcorp (software) (pays $3.20/s, +0.40 hacking xp/s, +0.80 reputation/s)", Terminal.Text);
        Assert.Equal(ActivityMode.Background, Service.Current!.Mode);
        Assert.Null(Terminal.Panel);

        var money = State.Player.Money;
        Advance(TenSeconds);

        Assert.Equal(money + 32, State.Player.Money, precision: 6);
    }

    [Fact]
    public async Task Focused_work_holds_the_console_and_the_summary_includes_reputation()
    {
        var command = Run("work testcorp software");

        Assert.False(command.IsCompleted);
        Assert.Equal("Working at testcorp (software)", Terminal.Panel!.Title);

        Advance(TenSeconds);

        Assert.Contains("+$40.00", Terminal.Panel.Detail());
        Assert.Contains("+10.00 reputation", Terminal.Panel.Detail());

        Terminal.Press(ConsoleKey.S);
        await command.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        Assert.Contains("Stopped. Working at testcorp (software): 10s, +$40.00, +5.00 hacking xp, +10.00 reputation.", Terminal.Text);
    }

    [Theory]
    [InlineData("work nope", "Unknown company 'nope'")]
    [InlineData("work testcorp", "several careers: software, security")]
    [InlineData("work testcorp nope", "Unknown career 'nope' at testcorp")]
    [InlineData("work hardcorp", "you need hacking level 5 (you have 1)")]
    public async Task Problems_with_a_job_are_explained_and_nothing_starts(string commandLine, string expected)
    {
        await Run(commandLine);

        Assert.Contains(expected, Terminal.Text);
        Assert.Null(Service.Current);
    }

    [Fact]
    public async Task A_promotion_is_announced_while_the_player_is_working()
    {
        GiveHackingLevel(3);
        var command = Run("work testcorp software");

        Advance(TenSeconds);

        Assert.Contains("Promoted! You are now Developer at testcorp (software): pays $10.00/s", Terminal.Text);

        Terminal.Press(ConsoleKey.S);
        await command.WaitAsync(Timeout, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Stopping_and_coming_back_resumes_the_held_rank()
    {
        await Run("work testcorp software -b");
        await Run("stop");

        await Run("work testcorp -b");                     // the career is remembered

        Assert.Contains("Back at work as Intern.", Terminal.Text);
    }

    [Fact]
    public async Task Activity_shows_the_position_the_reputation_and_the_next_rank()
    {
        await Run("work testcorp software -b");
        Advance(TenSeconds);

        await Run("activity");

        Assert.Contains("Position: Intern (rank 1 of 3)", Terminal.Text);
        Assert.Contains("Reputation at testcorp: 8.00", Terminal.Text);
        Assert.Contains("Next rank: Developer, needs hacking level 3, reputation 10", Terminal.Text);
    }

    [Fact]
    public async Task Activity_at_the_top_rank_says_so()
    {
        GiveHackingLevel(10);
        await Run("work testcorp software -b");
        Advance(TimeSpan.FromSeconds(200));          // 160 reputation in the background

        Terminal.WriteLine("-----");
        await Run("activity");

        Assert.Contains("Position: Lead (rank 3 of 3)", Terminal.Text);
        Assert.Contains("Next rank: you are at the top.", Terminal.Text);
    }
}
