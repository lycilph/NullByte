using NullByte.Core.Domain;
using NullByte.Core.Domain.Activities;
using NullByte.Core.Formulas;
using NullByte.Core.Scripting.Activities;
using NullByte.Core.Services;

namespace NullByte.Core.Scripting;

/// <summary>
/// Provides the activities sub-API for one script process. It runs on the game thread, like
/// the script itself, and goes through <see cref="ActivityService"/> exactly as the console does.
/// </summary>
public sealed class NullByteActivitiesApi : INullByteActivitiesApi
{
    private readonly GameState gameState;
    private readonly ActivityService service;
    private readonly SkillFormulas skillFormulas;
    private readonly CancellationToken cancellationToken;

    public NullByteActivitiesApi(
        GameState gameState,
        ActivityService service,
        SkillFormulas skillFormulas,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(skillFormulas);

        this.gameState = gameState;
        this.service = service;
        this.skillFormulas = skillFormulas;
        this.cancellationToken = cancellationToken;
    }

    public ActivityActionResult Work(string company, string career = "")
        => Describe(service.StartWork(company, NullIfBlank(career), ActivityMode.Background));

    public ActivityActionResult Study(string course, string university = "")
        => Describe(service.StartCourse(FacilityKind.University, course, NullIfBlank(university), ActivityMode.Background));

    public ActivityActionResult Train(string skill, string gym = "")
        => Describe(service.StartCourse(FacilityKind.Gym, skill, NullIfBlank(gym), ActivityMode.Background));

    public async Task<CrimeOutcome> Crime(string crime)
    {
        var started = service.StartCrime(crime, repeat: false, ActivityMode.Background);

        if (!started.Succeeded)
            return Outcome(started: false, completed: false, message: started.FailureMessage);

        var attempt = started.Activity!;
        var done = new TaskCompletionSource<CrimeOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Both events come from the game thread, in the order: the attempt ends, then the activity does.
        // So the first one to arrive tells how it went, and the second only matters if the attempt never ended.
        void OnAttempt(CrimeAttempt result)
        {
            if (!ReferenceEquals(result.Activity, attempt))
                return;

            done.TrySetResult(new CrimeOutcome(
                Started: true,
                Completed: true,
                Succeeded: result.Succeeded,
                Chance: result.Chance,
                Money: result.Rewards.Money,
                Experience: ByName(result.Rewards.Experience),
                Message: result.Succeeded ? "The attempt succeeded." : "The attempt failed."));
        }

        void OnEnded(ActivityEndInfo info)
        {
            if (ReferenceEquals(info.Activity, attempt))
                done.TrySetResult(Outcome(started: true, completed: false, message: "The attempt was stopped before it ended."));
        }

        service.CrimeAttempted += OnAttempt;
        service.ActivityEnded += OnEnded;

        try
        {
            return await done.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            service.CrimeAttempted -= OnAttempt;
            service.ActivityEnded -= OnEnded;
        }
    }

    public bool Stop() => service.Stop() is not null;

    public ActivityStatus? GetStatus()
    {
        if (service.Current is not { } activity)
            return null;

        var progress = activity.Progress;

        return new ActivityStatus(
            KindOf(activity),
            activity.Description,
            activity.Mode == ActivityMode.Background,
            progress.Elapsed,
            progress.NetMoney,
            ByName(progress.ExperienceGained),
            progress.ReputationGained,
            progress.Attempts,
            progress.Successes);
    }

    public int GetSkillLevel(string skill)
    {
        var kind = ParseSkill(skill);

        return skillFormulas.CalculateLevel(kind, gameState.Player.Skills.GetExperience(kind));
    }

    public double GetCrimeChance(string crime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(crime);

        if (!service.Catalog.TryGetCrime(crime, out var found))
            throw new ArgumentException($"Unknown crime '{crime}'.", nameof(crime));

        return service.GetCrimeChance(found);
    }

    public double GetReputation(string company)
        => gameState.Employment.GetReputation(GetCompany(company).Name);

    public JobInfo? GetJob(string company)
    {
        var found = GetCompany(company);

        if (gameState.Employment.GetJob(found.Name) is not { } job)
            return null;

        var career = found.Careers.FirstOrDefault(c => string.Equals(c.Name, job.Career, StringComparison.OrdinalIgnoreCase));

        if (career is null || job.RankIndex >= career.Ranks.Count)
            return null;

        return new JobInfo(found.Name, career.Name, career.Ranks[job.RankIndex].Title, job.RankIndex + 1, career.Ranks.Count);
    }

    public IReadOnlyList<string> GetCrimes() => service.Catalog.Crimes.Select(c => c.Name).ToList();

    public IReadOnlyList<string> GetCourses() => service.Catalog.Courses.Select(c => c.Name).ToList();

    public IReadOnlyList<string> GetCompanies() => service.Catalog.Companies.Select(c => c.Name).ToList();

    private static ActivityActionResult Describe(ActivityStartResult result)
    {
        if (!result.Succeeded)
            return new ActivityActionResult(false, result.FailureMessage);

        return new ActivityActionResult(true, result.Notice.Length > 0 ? result.Notice : result.Activity!.Description);
    }

    private static CrimeOutcome Outcome(bool started, bool completed, string message)
        => new(started, completed, Succeeded: false, Chance: 0, Money: 0, new Dictionary<string, double>(), message);

    private Company GetCompany(string company)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(company);

        if (!service.Catalog.TryGetCompany(company, out var found))
            throw new ArgumentException($"Unknown company '{company}'.", nameof(company));

        return found;
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string KindOf(Activity activity) => activity switch
    {
        ClassActivity { Facility.Kind: FacilityKind.University } => "study",
        ClassActivity => "train",
        JobActivity => "work",
        CrimeActivity => "crime",
        _ => "other"
    };

    private static Dictionary<string, double> ByName(IReadOnlyDictionary<SkillKind, double> amounts)
        => amounts.OrderBy(entry => entry.Key).ToDictionary(entry => SkillName(entry.Key), entry => entry.Value);

    private static string SkillName(SkillKind kind) => kind.ToString().ToLowerInvariant();

    private static SkillKind ParseSkill(string skill)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skill);

        foreach (var kind in Enum.GetValues<SkillKind>())
        {
            if (string.Equals(SkillName(kind), skill, StringComparison.OrdinalIgnoreCase))
                return kind;
        }

        throw new ArgumentException(
            $"Unknown skill '{skill}'. The skills are {string.Join(", ", Enum.GetValues<SkillKind>().Select(SkillName))}.",
            nameof(skill));
    }
}