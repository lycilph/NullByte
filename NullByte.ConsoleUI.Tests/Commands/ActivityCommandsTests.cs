using NullByte.ConsoleUI.Commands;
using NullByte.ConsoleUI.Commands.Groups;
using NullByte.Core.Configuration;
using NullByte.Core.Domain;
using NullByte.Core.Domain.Activities;
using NullByte.Core.Formulas;
using NullByte.Core.Services;
using NullByte.Core.Systems;
using NullByte.Core.World;

namespace NullByte.ConsoleUI.Tests.Commands;

public sealed class ActivityCommandsTests : IDisposable
{
    private readonly InvariantCulture culture = new();

    public void Dispose() => culture.Dispose();

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan TenSeconds = TimeSpan.FromSeconds(10);

    // "algorithms" costs $10/s for 2 hacking xp/s, "free-course" is free, "strength" costs $5/s. The player starts with $1000.
    private static readonly ActivityCatalog Catalog = new(
        facilities:
        [
            new TrainingFacility("plain-university", FacilityKind.University, 1, 1),
            new TrainingFacility("fancy-university", FacilityKind.University, 2, 3),
            new TrainingFacility("test-gym", FacilityKind.Gym, 1, 1)
        ],
        courses:
        [
            new Course("algorithms", FacilityKind.University, 10, new Dictionary<SkillKind, double> { [SkillKind.Hacking] = 2 }),
            new Course("free-course", FacilityKind.University, 0, new Dictionary<SkillKind, double> { [SkillKind.Charisma] = 1 }),
            new Course("strength", FacilityKind.Gym, 5, new Dictionary<SkillKind, double> { [SkillKind.Strength] = 1 })
        ],
        companies:
        [
            // testcorp: software has three ranks (Intern, Developer needs reputation 10 and hacking level 3,
            // Lead needs reputation 100 and hacking level 10); security has one. hardcorp's only rank needs hacking level 5.
            new Company("testcorp",
            [
                new CareerPath("software",
                [
                    Rank("Intern", salary: 4, reputation: 1, requiredReputation: 0, hackingLevel: 0, hackingXp: 0.5),
                    Rank("Developer", salary: 10, reputation: 2, requiredReputation: 10, hackingLevel: 3, hackingXp: 1),
                    Rank("Lead", salary: 30, reputation: 5, requiredReputation: 100, hackingLevel: 10, hackingXp: 2)
                ]),
                new CareerPath("security",
                [
                    new JobRank("Trainee", 3, 1, 0, new Dictionary<SkillKind, int>(), new Dictionary<SkillKind, double> { [SkillKind.Strength] = 0.5 })
                ])
            ]),
            new Company("hardcorp",
            [
                new CareerPath("software",
                [
                    Rank("Hard Intern", salary: 8, reputation: 1, requiredReputation: 0, hackingLevel: 5, hackingXp: 1)
                ])
            ])
        ],
        crimes:
        [
            // chance = weighted average skill level / (difficulty * 100), so at level 1:
            // sure-thing is certain, coin-flip is 50%, long-shot is 1%. mixed counts strength three times as much as agility.
            new Crime("sure-thing", 2, 100, 0.01, Amounts((SkillKind.Strength, 4), (SkillKind.Agility, 2)), Amounts((SkillKind.Strength, 1))),
            new Crime("coin-flip", 4, 500, 0.02, Amounts((SkillKind.Dexterity, 8)), Amounts((SkillKind.Dexterity, 1))),
            new Crime("long-shot", 10, 1000, 1.0, Amounts((SkillKind.Charisma, 10)), Amounts((SkillKind.Charisma, 1))),
            new Crime("mixed", 4, 200, 0.05, Amounts((SkillKind.Strength, 1)), Amounts((SkillKind.Strength, 3), (SkillKind.Agility, 1)))
        ]);

    private static JobRank Rank(string title, double salary, double reputation, double requiredReputation, int hackingLevel, double hackingXp)
        => new(title, salary, reputation, requiredReputation,
        hackingLevel > 0 ? new Dictionary<SkillKind, int> { [SkillKind.Hacking] = hackingLevel } : new Dictionary<SkillKind, int>(),
        new Dictionary<SkillKind, double> { [SkillKind.Hacking] = hackingXp });

    private static IReadOnlyDictionary<SkillKind, double> Amounts(params (SkillKind Skill, double Amount)[] amounts)
        => amounts.ToDictionary(a => a.Skill, a => a.Amount);

    private readonly ScriptedRandom random = new();
    private readonly BalanceConfig balance = new();
    private readonly GameState state = InitialGameStateFactory.Create();
    private readonly ActivityTestTerminal terminal = new();
    private readonly ActivityService service;
    private readonly ActivitySystem system;
    private readonly CommandDispatcher dispatcher;

    public ActivityCommandsTests()
    {
        var balance = new BalanceConfig();
        var formulas = new ActivityFormulas(balance);

        service = new ActivityService(state, Catalog, formulas);
        system = new ActivitySystem(state, formulas, service, random);

        var registry = new CommandRegistry();
        new ActivityCommands(service, state.Player, new SkillFormulas(balance), terminal).Register(registry);
        dispatcher = new CommandDispatcher(registry, terminal);
    }

    private Task<bool> Run(string commandLine) => dispatcher.DispatchAsync(commandLine);
    private void GiveHackingLevel(int level)
    => state.Player.Skills.AddExperience(SkillKind.Hacking, new SkillFormulas(balance).CalculateExperienceForLevel(SkillKind.Hacking, level));

    [Fact]
    public async Task Crime_without_arguments_lists_the_crimes_with_the_odds_and_the_rewards()
    {
        await Run("crime");

        Assert.Contains("sure-thing", terminal.Text);
        Assert.Contains("100%", terminal.Text);
        Assert.Contains("coin-flip", terminal.Text);
        Assert.Contains("50%", terminal.Text);
        Assert.Contains("long-shot", terminal.Text);
        Assert.Contains("1%", terminal.Text);
        Assert.Contains("+$100.00, +4.00 strength xp, +2.00 agility xp", terminal.Text);
        Assert.Contains("Usage: crime <name> [-b]", terminal.Text);
        Assert.Null(service.Current);
    }

    [Fact]
    public async Task A_crime_in_the_background_returns_at_once_and_does_not_print_every_attempt()
    {
        await Run("crime coin-flip -b");

        Assert.Contains("Committing coin-flip (4s per attempt, 50% chance, +$400.00, +6.40 dexterity xp on success).", terminal.Text);
        Assert.Equal(ActivityMode.Background, service.Current!.Mode);
        Assert.Null(terminal.Panel);

        system.Update(TimeSpan.FromSeconds(12));                   // three attempts

        Assert.DoesNotContain("coin-flip: success", terminal.Text);
        Assert.Equal(3, service.Current!.Attempts);
    }

    [Fact]
    public async Task A_focused_crime_shows_a_bar_that_fills_and_starts_over_and_a_line_for_every_attempt()
    {
        var command = Run("crime sure-thing");

        Assert.False(command.IsCompleted);
        Assert.Equal("Committing sure-thing", terminal.Panel!.Title);
        Assert.Equal(0.0, terminal.Panel.Progress()!.Value, precision: 6);

        system.Update(TimeSpan.FromSeconds(1));

        Assert.Equal(0.5, terminal.Panel.Progress()!.Value, precision: 6);

        system.Update(TimeSpan.FromSeconds(1));                    // the attempt ends and the next one begins

        Assert.Equal(0.0, terminal.Panel.Progress()!.Value, precision: 6);
        Assert.Contains("sure-thing: success (100% chance): +$100.00, +4.00 strength xp, +2.00 agility xp", terminal.Text);
        Assert.Contains("1/1 succeeded", terminal.Panel.Detail());

        terminal.Press(ConsoleKey.S);
        await command.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        Assert.Null(service.Current);
        Assert.Contains("Stopped. Committing sure-thing: 2s, +$100.00, +4.00 strength xp, +2.00 agility xp, 1 of 1 succeeded.", terminal.Text);
    }

    [Fact]
    public async Task A_failed_attempt_is_shown_as_such()
    {
        random.Enqueue(0.9);
        var command = Run("crime coin-flip");

        system.Update(TimeSpan.FromSeconds(4));

        Assert.Contains("coin-flip: failed (50% chance): no money, +2.00 dexterity xp", terminal.Text);

        terminal.Press(ConsoleKey.S);
        await command.WaitAsync(Timeout, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Sending_a_crime_to_the_background_stops_the_attempt_lines_but_not_the_crime()
    {
        var command = Run("crime sure-thing");
        system.Update(TimeSpan.FromSeconds(2));

        terminal.Press(ConsoleKey.B);
        await command.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        var linesBefore = terminal.Text.Split('\n').Count(l => l.StartsWith("sure-thing: success", StringComparison.Ordinal));

        system.Update(TimeSpan.FromSeconds(4));                    // two more attempts, in the background

        var linesAfter = terminal.Text.Split('\n').Count(l => l.StartsWith("sure-thing: success", StringComparison.Ordinal));
        Assert.Equal(1, linesBefore);
        Assert.Equal(1, linesAfter);
        Assert.Equal(3, service.Current!.Attempts);
    }

    [Fact]
    public async Task Focus_brings_a_background_crime_back_to_a_progress_bar()
    {
        await Run("crime sure-thing -b");
        system.Update(TimeSpan.FromSeconds(1));

        var command = Run("focus");

        Assert.Equal(0.5, terminal.Panel!.Progress()!.Value, precision: 6);       // the attempt in progress carries on

        terminal.Press(ConsoleKey.S);
        await command.WaitAsync(Timeout, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("crime nope", "Unknown crime 'nope'")]
    [InlineData("crime sure-thing coin-flip", "Usage: crime [name] [-b]")]
    [InlineData("crime sure-thing -x", "Unknown option '-x'")]
    public async Task Problems_with_a_crime_are_explained_and_nothing_starts(string commandLine, string expected)
    {
        await Run(commandLine);

        Assert.Contains(expected, terminal.Text);
        Assert.Null(service.Current);
    }

    [Fact]
    public async Task Activity_shows_the_attempts_the_chance_and_the_reward_of_a_crime()
    {
        await Run("crime coin-flip -b");
        system.Update(TimeSpan.FromSeconds(12));                                 // three attempts, 19.2 xp: dexterity level 2

        await Run("activity");

        Assert.Contains("Committing coin-flip", terminal.Text);
        Assert.Contains("Attempts: 3 (3 succeeded)", terminal.Text);
        Assert.Contains("Chance now: 100%", terminal.Text);                      // was 50% at level 1
        Assert.Contains("On success: +$400.00, +6.40 dexterity xp", terminal.Text);
        Assert.DoesNotContain("Now: ", terminal.Text);
    }

    [Fact]
    public async Task Work_without_arguments_lists_the_companies_with_their_entry_requirements()
    {
        await Run("work");

        Assert.Contains("testcorp (reputation 0.00)", terminal.Text);
        Assert.Contains("software   entry: Intern (no requirements)", terminal.Text);
        Assert.Contains("hardcorp", terminal.Text);
        Assert.Contains("entry: Hard Intern (hacking level 5)", terminal.Text);
        Assert.Contains("Usage: work <company> [career] [-b]", terminal.Text);
        Assert.Null(service.Current);
    }

    [Fact]
    public async Task The_listing_shows_where_the_player_stands()
    {
        await Run("work testcorp software -b");
        system.Update(TenSeconds);
        terminal.WriteLine("-----");

        await Run("work");

        var listing = terminal.Text[terminal.Text.IndexOf("-----", StringComparison.Ordinal)..];
        Assert.Contains("testcorp (reputation 8.00)", listing);
        Assert.Contains("you are Intern; next: Developer (hacking level 3, reputation 10)", listing);
    }

    [Fact]
    public async Task Working_in_the_background_hires_the_player_and_returns_at_once()
    {
        await Run("work testcorp software -b");

        Assert.Contains("Hired as Intern.", terminal.Text);
        Assert.Contains("Working at testcorp (software) (pays $3.20/s, +0.40 hacking xp/s, +0.80 reputation/s)", terminal.Text);
        Assert.Equal(ActivityMode.Background, service.Current!.Mode);
        Assert.Null(terminal.Panel);

        var money = state.Player.Money;
        system.Update(TenSeconds);

        Assert.Equal(money + 32, state.Player.Money, precision: 6);
    }

    [Fact]
    public async Task Focused_work_holds_the_console_and_the_summary_includes_reputation()
    {
        var command = Run("work testcorp software");

        Assert.False(command.IsCompleted);
        Assert.Equal("Working at testcorp (software)", terminal.Panel!.Title);

        system.Update(TenSeconds);

        Assert.Contains("+$40.00", terminal.Panel.Detail());
        Assert.Contains("+10.00 reputation", terminal.Panel.Detail());

        terminal.Press(ConsoleKey.S);
        await command.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        Assert.Contains("Stopped. Working at testcorp (software): 10s, +$40.00, +5.00 hacking xp, +10.00 reputation.", terminal.Text);
    }

    [Theory]
    [InlineData("work nope", "Unknown company 'nope'")]
    [InlineData("work testcorp", "several careers: software, security")]
    [InlineData("work testcorp nope", "Unknown career 'nope' at testcorp")]
    [InlineData("work hardcorp", "you need hacking level 5 (you have 1)")]
    public async Task Problems_with_a_job_are_explained_and_nothing_starts(string commandLine, string expected)
    {
        await Run(commandLine);

        Assert.Contains(expected, terminal.Text);
        Assert.Null(service.Current);
    }

    [Fact]
    public async Task A_promotion_is_announced_while_the_player_is_working()
    {
        GiveHackingLevel(3);
        var command = Run("work testcorp software");

        system.Update(TenSeconds);

        Assert.Contains("Promoted! You are now Developer at testcorp (software): pays $10.00/s", terminal.Text);

        terminal.Press(ConsoleKey.S);
        await command.WaitAsync(Timeout, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Stopping_and_coming_back_resumes_the_held_rank()
    {
        await Run("work testcorp software -b");
        await Run("stop");

        await Run("work testcorp -b");                     // the career is remembered

        Assert.Contains("Back at work as Intern.", terminal.Text);
    }

    [Fact]
    public async Task Activity_shows_the_position_the_reputation_and_the_next_rank()
    {
        await Run("work testcorp software -b");
        system.Update(TenSeconds);

        await Run("activity");

        Assert.Contains("Position: Intern (rank 1 of 3)", terminal.Text);
        Assert.Contains("Reputation at testcorp: 8.00", terminal.Text);
        Assert.Contains("Next rank: Developer, needs hacking level 3, reputation 10", terminal.Text);
    }

    [Fact]
    public async Task Activity_at_the_top_rank_says_so()
    {
        GiveHackingLevel(10);
        await Run("work testcorp software -b");
        system.Update(TimeSpan.FromSeconds(200));          // 160 reputation in the background

        terminal.WriteLine("-----");
        await Run("activity");

        Assert.Contains("Position: Lead (rank 3 of 3)", terminal.Text);
        Assert.Contains("Next rank: you are at the top.", terminal.Text);
    }

    [Fact]
    public async Task Study_without_arguments_lists_the_courses_and_universities()
    {
        await Run("study");

        Assert.Contains("algorithms", terminal.Text);
        Assert.Contains("costs $10.00/s, +2.00 hacking xp/s", terminal.Text);
        Assert.Contains("fancy-university", terminal.Text);
        Assert.DoesNotContain("strength", terminal.Text);
        Assert.Null(service.Current);
    }

    [Fact]
    public async Task Train_without_arguments_lists_the_gym_courses_only()
    {
        await Run("train");

        Assert.Contains("strength", terminal.Text);
        Assert.Contains("test-gym", terminal.Text);
        Assert.DoesNotContain("algorithms", terminal.Text);
    }

    [Fact]
    public async Task Starting_in_the_background_returns_at_once_and_keeps_the_activity_running()
    {
        await Run("study algorithms -b");

        Assert.Equal(ActivityMode.Background, service.Current!.Mode);
        Assert.Null(terminal.Panel);
        Assert.Contains("Studying algorithms at plain-university", terminal.Text);
        Assert.Contains("reduced by 20%", terminal.Text);
    }

    [Fact]
    public async Task A_focused_activity_holds_the_console_and_shows_a_live_panel()
    {
        var command = Run("study algorithms");

        Assert.False(command.IsCompleted);
        Assert.NotNull(terminal.Panel);
        Assert.Equal("Studying algorithms at plain-university", terminal.Panel!.Title);
        Assert.Null(terminal.Panel.Progress());                       // indeterminate

        system.Update(TenSeconds);

        Assert.Contains("hacking", terminal.Panel.Detail());
        Assert.Contains("-$100.00", terminal.Panel.Detail());

        terminal.Press(ConsoleKey.S);
        await command.WaitAsync(Timeout, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task S_stops_the_activity_and_prints_a_summary()
    {
        var command = Run("study algorithms");
        system.Update(TenSeconds);

        Assert.True(terminal.Press(ConsoleKey.S));
        await command.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        Assert.Null(service.Current);
        Assert.Null(terminal.Panel);
        Assert.Contains("Stopped. Studying algorithms at plain-university: 10s, -$100.00, +20.00 hacking xp.", terminal.Text);
    }

    [Fact]
    public async Task Ctrl_C_also_stops_the_activity()
    {
        var command = Run("study algorithms");

        Assert.True(terminal.Press(ConsoleKey.C, ConsoleModifiers.Control));
        await command.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        Assert.Null(service.Current);
    }

    [Theory]
    [InlineData(ConsoleKey.B)]
    [InlineData(ConsoleKey.Escape)]
    public async Task B_and_Escape_send_the_activity_to_the_background_where_it_keeps_running(ConsoleKey key)
    {
        var command = Run("study algorithms");

        Assert.True(terminal.Press(key));
        await command.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        Assert.Equal(ActivityMode.Background, service.Current!.Mode);
        Assert.Null(terminal.Panel);
        Assert.Contains("continues in the background", terminal.Text);

        system.Update(TenSeconds);   // background: 80% of 20 xp

        Assert.Equal(16, state.Player.Skills.GetExperience(SkillKind.Hacking), precision: 6);
    }

    [Fact]
    public async Task A_hotkey_that_is_not_on_the_panel_does_nothing()
    {
        var command = Run("study algorithms");

        Assert.False(terminal.Press(ConsoleKey.X));
        Assert.False(command.IsCompleted);

        terminal.Press(ConsoleKey.S);
        await command.WaitAsync(Timeout, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_focused_activity_that_cannot_be_paid_for_any_more_ends_the_command_by_itself()
    {
        state.Player.TrySpendMoney(state.Player.Money - 15);
        var command = Run("study algorithms");

        system.Update(TimeSpan.FromSeconds(5));                     // $50 of tuition, only $15 available

        await command.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        Assert.Null(service.Current);
        Assert.Null(terminal.Panel);
        Assert.Contains("You can no longer afford it", terminal.Text);
    }

    [Fact]
    public async Task A_background_activity_that_cannot_be_paid_for_any_more_says_so()
    {
        state.Player.TrySpendMoney(state.Player.Money - 15);
        await Run("study algorithms -b");

        system.Update(TimeSpan.FromSeconds(5));

        Assert.Null(service.Current);
        Assert.Contains("You can no longer afford it", terminal.Text);
    }

    [Fact]
    public async Task Stop_ends_a_background_activity_and_prints_the_summary()
    {
        await Run("study algorithms -b");
        system.Update(TenSeconds);

        await Run("stop");

        Assert.Null(service.Current);
        Assert.Contains("Stopped. Studying algorithms at plain-university: 10s, -$100.00, +16.00 hacking xp.", terminal.Text);
    }

    [Fact]
    public async Task Stop_when_idle_says_so()
    {
        await Run("stop");

        Assert.Contains("You are not doing anything.", terminal.Text);
    }

    [Fact]
    public async Task Focus_takes_over_a_background_activity_and_holds_the_console()
    {
        await Run("study algorithms -b");

        var command = Run("focus");

        Assert.False(command.IsCompleted);
        Assert.Equal(ActivityMode.Focused, service.Current!.Mode);
        Assert.NotNull(terminal.Panel);

        terminal.Press(ConsoleKey.S);
        await command.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        Assert.Null(service.Current);
    }

    [Fact]
    public async Task Focus_when_idle_says_so()
    {
        await Run("focus");

        Assert.Contains("You are not doing anything.", terminal.Text);
        Assert.Null(terminal.Panel);
    }

    [Fact]
    public async Task Activity_shows_the_mode_rates_and_progress()
    {
        await Run("study algorithms -b");
        system.Update(TenSeconds);

        await Run("activity");

        Assert.Contains("Mode: in the background (rewards reduced by 20%)", terminal.Text);
        Assert.Contains("Now: costs $10.00/s, +1.60 hacking xp/s", terminal.Text);
        Assert.Contains("So far: 10s | -$100.00 | +16.00 hacking xp", terminal.Text);
    }

    [Fact]
    public async Task Activity_when_idle_says_so()
    {
        await Run("activity");

        Assert.Contains("You are not doing anything.", terminal.Text);
    }

    [Fact]
    public async Task A_named_university_is_used_and_a_wrong_course_is_explained()
    {
        await Run("study strength");
        Assert.Contains("'strength' is a gym course, not a university course", terminal.Text);

        await Run("study free-course fancy-university -b");
        Assert.Contains("Studying free-course at fancy-university", terminal.Text);
    }

    [Fact]
    public async Task Training_at_the_gym_works_the_same_way()
    {
        await Run("train strength -b");
        system.Update(TenSeconds);

        Assert.Equal(8, state.Player.Skills.GetExperience(SkillKind.Strength), precision: 6);
        Assert.Contains("Training strength at test-gym", terminal.Text);
    }

    [Fact]
    public async Task An_unknown_option_is_rejected_and_starts_nothing()
    {
        await Run("study algorithms -x");

        Assert.Contains("Unknown option '-x'", terminal.Text);
        Assert.Null(service.Current);
    }

    [Fact]
    public async Task Skills_lists_every_skill_with_its_level_and_experience()
    {
        await Run("train strength -b");
        system.Update(TimeSpan.FromSeconds(100));

        await Run("skills");

        foreach (var name in new[] { "hacking", "strength", "defense", "dexterity", "agility", "charisma" })
            Assert.Contains(name, terminal.Text);

        Assert.Contains("80.00", terminal.Text);       // 100 s at 0.8 xp/s
    }
}