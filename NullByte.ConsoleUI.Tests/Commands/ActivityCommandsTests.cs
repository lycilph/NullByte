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
        companies: [],
        crimes: []);

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
        system = new ActivitySystem(state, formulas, service);

        var registry = new CommandRegistry();
        new ActivityCommands(service, state.Player, new SkillFormulas(balance), terminal).Register(registry);
        dispatcher = new CommandDispatcher(registry, terminal);
    }

    private Task<bool> Run(string commandLine) => dispatcher.DispatchAsync(commandLine);

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