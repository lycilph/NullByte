using NullByte.ConsoleUI.Terminal;
using NullByte.Core.Domain;
using NullByte.Core.Domain.Activities;
using NullByte.Core.Formulas;
using NullByte.Core.Services;

namespace NullByte.ConsoleUI.Commands.Groups;

/// <summary>
/// The commands for what the player does besides hacking: <c>study</c>, <c>train</c>,
/// <c>stop</c>, <c>focus</c>, <c>activity</c> and <c>skills</c>.
/// </summary>
/// <remarks>
/// <para>
/// A focused activity owns the console: the command waits until the activity
/// ends or the player presses a hotkey (stop, or send it to the background).
/// A background activity runs on its own and the console stays free.
/// </para>
/// <para>
/// Commands run on the game thread. Hotkeys are pressed on the terminal's
/// thread, so they only signal; the command does the work when it resumes.
/// </para>
/// </remarks>
public sealed class ActivityCommands : ICommandGroup
{
    private enum FocusDecision { Ended, Stop, Background }

    private readonly ActivityService service;
    private readonly Player player;
    private readonly SkillFormulas skillFormulas;
    private readonly IGameTerminal terminal;

    public ActivityCommands(ActivityService service, Player player, SkillFormulas skillFormulas, IGameTerminal terminal)
    {
        this.service = service ?? throw new ArgumentNullException(nameof(service));
        this.player = player ?? throw new ArgumentNullException(nameof(player));
        this.skillFormulas = skillFormulas ?? throw new ArgumentNullException(nameof(skillFormulas));
        this.terminal = terminal ?? throw new ArgumentNullException(nameof(terminal));

        service.ActivityEnded += OnActivityEnded;
    }

    public void Register(CommandRegistry registry)
    {
        registry.Register(new CommandDefinition(
            "study",
            "study [course] [university] [-b]",
            "Take a university course (no arguments: list them).",
            arguments => StartCourseAsync(FacilityKind.University, arguments))
        {
            MinArgs = 0, MaxArgs = 3,
            Completions = CompletionsFor(FacilityKind.University)
        });

        registry.Register(new CommandDefinition(
            "train",
            "train [skill] [gym] [-b]",
            "Train at a gym (no arguments: list the courses).",
            arguments => StartCourseAsync(FacilityKind.Gym, arguments))
        {
            MinArgs = 0, MaxArgs = 3,
            Completions = CompletionsFor(FacilityKind.Gym)
        });

        registry.Register(new CommandDefinition(
            "stop",
            "stop",
            "Stop what you are doing.",
            StopAsync));

        registry.Register(new CommandDefinition(
            "focus",
            "focus",
            "Give your full attention to what you are doing in the background.",
            FocusAsync));

        registry.Register(new CommandDefinition(
            "activity",
            "activity",
            "Show what you are doing right now.",
            ShowActivityAsync));

        registry.Register(new CommandDefinition(
            "skills",
            "skills",
            "Show your skills.",
            ShowSkillsAsync));
    }

    private async Task StartCourseAsync(FacilityKind kind, IReadOnlyList<string> arguments)
    {
        if (!ActivityArguments.TryParse(arguments, out var request, out var error))
        {
            terminal.WriteLine(error);
            return;
        }

        if (request.Course is null)
        {
            ListCourses(kind);
            return;
        }

        var mode = request.Background ? ActivityMode.Background : ActivityMode.Focused;
        var result = service.StartCourse(kind, request.Course, request.Facility, mode);

        if (!result.Succeeded)
        {
            terminal.WriteLine(result.FailureMessage);
            return;
        }

        var activity = result.Activity!;
        terminal.WriteLine($"{activity.Description} ({ActivityFormatter.FormatRates(service.GetRates(activity))}).");

        if (mode == ActivityMode.Background)
            terminal.WriteLine(BackgroundNotice());
        else
            await RunFocusedAsync(activity);
    }

    private Task StopAsync(IReadOnlyList<string> arguments)
    {
        if (service.Stop() is { } stopped)
            terminal.WriteLine($"Stopped. {ActivityFormatter.FormatSummary(stopped)}");
        else
            terminal.WriteLine(NotBusy);

        return Task.CompletedTask;
    }

    private async Task FocusAsync(IReadOnlyList<string> arguments)
    {
        if (service.SetMode(ActivityMode.Focused) is not { } activity)
        {
            terminal.WriteLine(NotBusy);
            return;
        }

        await RunFocusedAsync(activity);
    }

    private Task ShowActivityAsync(IReadOnlyList<string> arguments)
    {
        if (service.Current is not { } activity)
        {
            terminal.WriteLine(NotBusy);
            return Task.CompletedTask;
        }

        var progress = activity.Progress;

        terminal.WriteLine(activity.Description);
        terminal.WriteLine(activity.Mode == ActivityMode.Focused
            ? "Mode: focused"
            : $"Mode: in the background (rewards reduced by {RewardReductionText()})");
        terminal.WriteLine($"Now: {ActivityFormatter.FormatRates(service.GetRates(activity))}");
        terminal.WriteLine($"So far: {ActivityFormatter.FormatProgress(progress)}");

        return Task.CompletedTask;
    }

    private Task ShowSkillsAsync(IReadOnlyList<string> arguments)
    {
        terminal.WriteLine($"{"Skill",-10} {"Level",6} {"Experience",14}");

        foreach (var kind in Enum.GetValues<SkillKind>())
        {
            var experience = player.Skills.GetExperience(kind);
            var level = skillFormulas.CalculateLevel(kind, experience);

            terminal.WriteLine($"{ActivityFormatter.FormatSkill(kind),-10} {level,6} {experience,14:N2}");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Holds the console until the activity ends or the player presses a hotkey, then does what they chose.
    /// </summary>
    private async Task RunFocusedAsync(Activity activity)
    {
        var decision = new TaskCompletionSource<FocusDecision>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnEnded(ActivityEndInfo info)
        {
            if (ReferenceEquals(info.Activity, activity))
                decision.TrySetResult(FocusDecision.Ended);
        }

        var panel = new LivePanel(
            activity.Description,
            Progress: () => null,
            Detail: () => ActivityFormatter.FormatProgress(activity.Progress),
            Hotkeys:
            [
                new Hotkey(ConsoleKey.S, () => decision.TrySetResult(FocusDecision.Stop), "[S] stop"),
                new Hotkey(ConsoleKey.C, () => decision.TrySetResult(FocusDecision.Stop), Modifiers: ConsoleModifiers.Control),
                new Hotkey(ConsoleKey.B, () => decision.TrySetResult(FocusDecision.Background), "[B] background"),
                new Hotkey(ConsoleKey.Escape, () => decision.TrySetResult(FocusDecision.Background))
            ]);

        FocusDecision chosen;
        service.ActivityEnded += OnEnded;

        try
        {
            using (terminal.ShowLivePanel(panel))
                chosen = await decision.Task.WaitAsync(terminal.StoppingToken);
        }
        finally
        {
            service.ActivityEnded -= OnEnded;
        }

        // Back on the game thread. The activity may have ended in the meantime, so check it is still the current one.
        switch (chosen)
        {
            case FocusDecision.Stop when service.Current == activity:
                if (service.Stop() is { } stopped)
                    terminal.WriteLine($"Stopped. {ActivityFormatter.FormatSummary(stopped)}");
                break;

            case FocusDecision.Background when service.Current == activity:
                service.SetMode(ActivityMode.Background);
                terminal.WriteLine($"{activity.Description} continues in the background.");
                terminal.WriteLine(BackgroundNotice());
                break;
        }
    }

    // Raised on the game thread. Only activities that end by themselves need telling: the player ended the others.
    private void OnActivityEnded(ActivityEndInfo info)
    {
        if (info.Reason != ActivityEndReason.CannotAfford)
            return;

        terminal.WriteLine($"You can no longer afford it: {info.Activity.Description} has ended.");
        terminal.WriteLine(ActivityFormatter.FormatSummary(info.Activity));
    }

    private void ListCourses(FacilityKind kind)
    {
        var catalog = service.Catalog;
        var usage = kind == FacilityKind.University ? "study <course> [university] [-b]" : "train <skill> [gym] [-b]";
        var facilities = catalog.Facilities.Where(f => f.Kind == kind).ToList();

        if (facilities.Count == 0)
        {
            terminal.WriteLine($"There is no {kind.ToString().ToLowerInvariant()} to go to.");
            return;
        }

        terminal.WriteLine($"Courses (prices at {facilities[0].Name}):");

        foreach (var course in catalog.Courses.Where(c => c.Kind == kind))
        {
            var rates = service.GetRates(new ClassActivity(course, facilities[0], ActivityMode.Focused));
            terminal.WriteLine($"  {course.Name,-18} {ActivityFormatter.FormatRates(rates)}");
        }

        terminal.WriteLine();
        terminal.WriteLine(kind == FacilityKind.University ? "Universities:" : "Gyms:");

        foreach (var facility in facilities)
            terminal.WriteLine($"  {facility.Name,-22} cost x{facility.CostMultiplier:0.##}, experience x{facility.ExperienceMultiplier:0.##}");

        terminal.WriteLine();
        terminal.WriteLine($"Usage: {usage}");
    }

    private IReadOnlyList<ArgumentCompletion> CompletionsFor(FacilityKind kind) =>
    [
        new ArgumentCompletion
        {
            ArgumentIndex = 0,
            Values = service.Catalog.Courses.Where(c => c.Kind == kind).Select(c => c.Name).ToList()
        },
        new ArgumentCompletion
        {
            ArgumentIndex = 1,
            Values = service.Catalog.Facilities.Where(f => f.Kind == kind).Select(f => f.Name).ToList()
        }
    ];

    // "20%": built by hand, because {x:P0} puts a space before the % in some cultures.
    private string RewardReductionText()
        => $"{(int)Math.Round((1 - service.GetRewardFactor(ActivityMode.Background)) * 100)}%";

    private string BackgroundNotice()
        => $"Rewards are reduced by {RewardReductionText()} in the background. Use 'focus' to take over or 'stop' to end it.";

    private const string NotBusy = "You are not doing anything.";
}