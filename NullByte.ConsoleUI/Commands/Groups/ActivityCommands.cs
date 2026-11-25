using NullByte.ConsoleUI.Terminal;
using NullByte.Core.Domain;
using NullByte.Core.Domain.Activities;
using NullByte.Core.Formulas;
using NullByte.Core.Services;

namespace NullByte.ConsoleUI.Commands.Groups;

/// <summary>
/// The commands for what the player does besides hacking: <c>study</c>, <c>train</c>,
/// <c>work</c>, <c>crime</c>, <c>stop</c>, <c>focus</c>, <c>activity</c> and <c>skills</c>.
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
        service.JobPromoted += OnJobPromoted;
        service.CrimeAttempted += OnCrimeAttempted;
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
            Completions =
            [
                new ArgumentCompletion { ArgumentIndex = 0, Kind = CompletionKind.UniversityCourse },
                new ArgumentCompletion { ArgumentIndex = 1, Kind = CompletionKind.University }
            ]
        });

        registry.Register(new CommandDefinition(
            "train",
            "train [skill] [gym] [-b]",
            "Train at a gym (no arguments: list the courses).",
            arguments => StartCourseAsync(FacilityKind.Gym, arguments))
        {
            MinArgs = 0, MaxArgs = 3,
            Completions =
            [
                new ArgumentCompletion { ArgumentIndex = 0, Kind = CompletionKind.GymCourse },
                new ArgumentCompletion { ArgumentIndex = 1, Kind = CompletionKind.Gym }
            ]
        });

        registry.Register(new CommandDefinition(
            "work",
            "work [company] [career] [-b]",
            "Work for a company (no arguments: list them).",
            WorkAsync)
        {
            MinArgs = 0, MaxArgs = 3,
            Completions =
            [
                new ArgumentCompletion { ArgumentIndex = 0, Kind = CompletionKind.Company },
                new ArgumentCompletion { ArgumentIndex = 1, Kind = CompletionKind.Career }
            ]
        });

        registry.Register(new CommandDefinition(
           "crime",
           "crime [name] [-b]",
           "Commit a crime, again and again until you stop (no arguments: list them).",
           CrimeAsync)
        {
            MinArgs = 0, MaxArgs = 2,
            Completions = [new ArgumentCompletion { ArgumentIndex = 0, Kind = CompletionKind.Crime }]
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

        if (request.Target is null)
        {
            ListCourses(kind);
            return;
        }

        var mode = request.Background ? ActivityMode.Background : ActivityMode.Focused;

        await BeginAsync(service.StartCourse(kind, request.Target, request.Detail, mode), mode);
    }

    private async Task WorkAsync(IReadOnlyList<string> arguments)
    {
        if (!ActivityArguments.TryParse(arguments, out var request, out var error))
        {
            terminal.WriteLine(error);
            return;
        }

        if (request.Target is null)
        {
            ListCompanies();
            return;
        }

        var mode = request.Background ? ActivityMode.Background : ActivityMode.Focused;

        await BeginAsync(service.StartWork(request.Target, request.Detail, mode), mode);
    }

    private async Task CrimeAsync(IReadOnlyList<string> arguments)
    {
        if (!ActivityArguments.TryParse(arguments, out var request, out var error))
        {
            terminal.WriteLine(error);
            return;
        }

        if (request.Detail is not null)
        {
            terminal.WriteLine("Usage: crime [name] [-b]");
            return;
        }

        if (request.Target is null)
        {
            ListCrimes();
            return;
        }

        var mode = request.Background ? ActivityMode.Background : ActivityMode.Focused;

        await BeginAsync(service.StartCrime(request.Target, repeat: true, mode), mode);
    }

    // What happens after any activity was started: say so, then hold the console or let it run on the side.
    private async Task BeginAsync(ActivityStartResult result, ActivityMode mode)
    {
        if (!result.Succeeded)
        {
            terminal.WriteLine(result.FailureMessage);
            return;
        }

        var activity = result.Activity!;

        if (result.Notice.Length > 0)
            terminal.WriteLine(result.Notice);

        terminal.WriteLine(activity is CrimeActivity crime
            ? $"{DescribeCrime(crime)}."
            : $"{activity.Description} ({ActivityFormatter.FormatRates(service.GetRates(activity))}).");

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

        if (activity is JobActivity job)
            WriteJobDetails(job);

        terminal.WriteLine(activity.Mode == ActivityMode.Focused
            ? "Mode: focused"
            : $"Mode: in the background (rewards reduced by {RewardReductionText()})");

        if (activity is CrimeActivity crime)
            WriteCrimeDetails(crime);
        else
            terminal.WriteLine($"Now: {ActivityFormatter.FormatRates(service.GetRates(activity))}");

        terminal.WriteLine($"So far: {ActivityFormatter.FormatProgress(progress)}");

        return Task.CompletedTask;
    }
    
    private void WriteJobDetails(JobActivity job)
    {
        terminal.WriteLine($"Position: {job.Rank.Title} (rank {job.RankIndex + 1} of {job.Career.Ranks.Count})");
        terminal.WriteLine($"Reputation at {job.Company.Name}: {service.Employment.GetReputation(job.Company.Name):N2}");

        terminal.WriteLine(job.IsTopRank
            ? "Next rank: you are at the top."
            : $"Next rank: {job.Career.Ranks[job.RankIndex + 1].Title}, needs {ActivityFormatter.FormatRequirements(job.Career.Ranks[job.RankIndex + 1])}");
    }

    private void WriteCrimeDetails(CrimeActivity crime)
    {
        terminal.WriteLine($"Attempts: {crime.Attempts} ({crime.Successes} succeeded)");
        terminal.WriteLine($"Chance now: {ActivityFormatter.FormatChance(service.GetCrimeChance(crime.Crime))}");
        terminal.WriteLine($"On success: {ActivityFormatter.FormatCrimeRewards(service.GetCrimeRewards(crime.Crime, crime.Mode, succeeded: true))}");
    }

    private string DescribeCrime(CrimeActivity crime)
        => $"{crime.Description} ({ActivityFormatter.FormatDuration(TimeSpan.FromSeconds(crime.Crime.DurationSeconds))} per attempt, " +
           $"{ActivityFormatter.FormatChance(service.GetCrimeChance(crime.Crime))} chance, " +
           $"{ActivityFormatter.FormatCrimeRewards(service.GetCrimeRewards(crime.Crime, crime.Mode, succeeded: true))} on success)";

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

        // A crime has attempts of a known length, so it shows a bar that fills and starts over.
        Func<double?> progress = activity is CrimeActivity crimeActivity
            ? () => crimeActivity.CycleProgress
            : () => null;

        var panel = new LivePanel(
            activity.Description,
            Progress: progress,
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

    // Raised on the game thread, so it can arrive while the focused panel is showing.
    private void OnJobPromoted(JobPromotion promotion)
    {
        var job = promotion.Job;

        terminal.WriteLine($"Promoted! You are now {job.Rank.Title} at {job.Company.Name} ({job.Career.Name}): " +
                           $"{ActivityFormatter.FormatRates(service.GetRates(job))}.");
    }

    // Raised on the game thread. A line for every attempt would bury the console while the player is doing something else.
    private void OnCrimeAttempted(CrimeAttempt attempt)
    {
        if (attempt.Activity.Mode == ActivityMode.Focused)
            terminal.WriteLine(ActivityFormatter.FormatAttempt(attempt));
    }

    private void ListCrimes()
    {
        terminal.WriteLine("Crimes (with your chance of success now):");

        foreach (var crime in service.Catalog.Crimes)
        {
            var duration = ActivityFormatter.FormatDuration(TimeSpan.FromSeconds(crime.DurationSeconds));
            var chance = ActivityFormatter.FormatChance(service.GetCrimeChance(crime));
            var rewards = ActivityFormatter.FormatCrimeRewards(service.GetCrimeRewards(crime, ActivityMode.Focused, succeeded: true));

            terminal.WriteLine($"  {crime.Name,-18} {duration,6} {chance,5}  {rewards}");
        }

        terminal.WriteLine();
        terminal.WriteLine("Usage: crime <name> [-b]   (repeats until you stop)");
    }

    private void ListCompanies()
    {
        var employment = service.Employment;

        terminal.WriteLine("Companies:");

        foreach (var company in service.Catalog.Companies)
        {
            terminal.WriteLine($"  {company.Name} (reputation {employment.GetReputation(company.Name):N2})");

            var held = employment.GetJob(company.Name);

            foreach (var career in company.Careers)
            {
                var isHeld = held is not null && string.Equals(held.Career, career.Name, StringComparison.OrdinalIgnoreCase);

                if (!isHeld)
                {
                    terminal.WriteLine($"    {career.Name,-10} entry: {career.Ranks[0].Title} ({ActivityFormatter.FormatRequirements(career.Ranks[0])})");
                }
                else if (held!.RankIndex + 1 < career.Ranks.Count)
                {
                    var next = career.Ranks[held.RankIndex + 1];
                    terminal.WriteLine($"    {career.Name,-10} you are {career.Ranks[held.RankIndex].Title}; next: {next.Title} ({ActivityFormatter.FormatRequirements(next)})");
                }
                else
                {
                    terminal.WriteLine($"    {career.Name,-10} you are {career.Ranks[held.RankIndex].Title} (top rank)");
                }
            }
        }

        terminal.WriteLine();
        terminal.WriteLine("Usage: work <company> [career] [-b]");
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


    // "20%": built by hand, because {x:P0} puts a space before the % in some cultures.
    private string RewardReductionText()
        => $"{(int)Math.Round((1 - service.GetRewardFactor(ActivityMode.Background)) * 100)}%";

    private string BackgroundNotice()
        => $"Rewards are reduced by {RewardReductionText()} in the background. Use 'focus' to take over or 'stop' to end it.";

    private const string NotBusy = "You are not doing anything.";
}