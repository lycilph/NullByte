using NullByte.Core.Domain;
using NullByte.Core.Domain.Activities;
using NullByte.Core.Formulas;

namespace NullByte.Core.Services;

/// <summary>
/// Starts, stops and re-focuses the player's activity. One activity runs at a
/// time.
/// </summary>
/// <remarks>
/// Like the other services this is intended to be used on the game thread. The
/// activity itself advances in <see cref="Systems.ActivitySystem"/>.
/// </remarks>
public sealed class ActivityService
{
    private readonly GameState gameState;
    private readonly ActivityCatalog catalog;
    private readonly ActivityFormulas formulas;

    public ActivityService(GameState gameState, ActivityCatalog catalog, ActivityFormulas formulas)
    {
        this.gameState = gameState
            ?? throw new ArgumentNullException(nameof(gameState));

        this.catalog = catalog
            ?? throw new ArgumentNullException(nameof(catalog));

        this.formulas = formulas
            ?? throw new ArgumentNullException(nameof(formulas));
    }

    /// <summary>
    /// Raised on the game thread when the current activity ends, whether the
    /// player stopped it or it ended by itself. Handlers must not throw.
    /// </summary>
    public event Action<ActivityEndInfo>? ActivityEnded;

    /// <summary>Gets everything that can be done.</summary>
    public ActivityCatalog Catalog => catalog;

    /// <summary>Gets the current activity, or null if the player is idle.</summary>
    public Activity? Current => gameState.Activity.Current;

    /// <summary>
    /// Starts a course.
    /// </summary>
    /// <param name="kind">Which kind of course this is: a university course or a gym course.</param>
    /// <param name="courseName">The course to take.</param>
    /// <param name="facilityName">
    /// The facility to take it at, or null for the first facility of that kind
    /// in the catalog (the catalog lists the cheapest first).
    /// </param>
    /// <param name="mode">Whether to start focused or in the background.</param>
    public ActivityStartResult StartCourse(
        FacilityKind kind,
        string courseName,
        string? facilityName = null,
        ActivityMode mode = ActivityMode.Focused)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(courseName);

        if (Current is { } running)
            return ActivityStartResult.Failed($"You are already busy: {running.Description}. Stop that first.");

        if (!catalog.TryGetCourse(courseName, out var course))
            return ActivityStartResult.Failed($"Unknown course '{courseName}'. Available: {ListCourses(kind)}.");

        if (course.Kind != kind)
            return ActivityStartResult.Failed($"'{course.Name}' is a {Word(course.Kind)} course, not a {Word(kind)} course.");

        TrainingFacility facility;

        if (facilityName is null)
        {
            var first = catalog.Facilities.FirstOrDefault(f => f.Kind == kind);

            if (first is null)
                return ActivityStartResult.Failed($"There is no {Word(kind)} to go to.");

            facility = first;
        }
        else if (!catalog.TryGetFacility(facilityName, out facility!))
        {
            return ActivityStartResult.Failed($"Unknown {Word(kind)} '{facilityName}'. Available: {ListFacilities(kind)}.");
        }
        else if (facility.Kind != kind)
        {
            return ActivityStartResult.Failed($"'{facility.Name}' is a {Word(facility.Kind)}, not a {Word(kind)}.");
        }

        var activity = new ClassActivity(course, facility, mode);
        var rates = formulas.CalculateRates(activity);

        if (-rates.MoneyPerSecond > gameState.Player.Money)
            return ActivityStartResult.Failed($"Not enough money. {activity.Description} costs ${-rates.MoneyPerSecond:N2} per second.");

        gameState.Activity.Current = activity;

        return new ActivityStartResult(true, string.Empty, activity);
    }

    /// <summary>
    /// Stops the current activity.
    /// </summary>
    /// <returns>The activity that was stopped, or null if there was none.</returns>
    public Activity? Stop() => End(ActivityEndReason.Stopped);

    /// <summary>
    /// Focuses the current activity, or moves it to the background.
    /// </summary>
    /// <returns>The activity, or null if there is none.</returns>
    public Activity? SetMode(ActivityMode mode)
    {
        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown activity mode.");

        var activity = Current;

        if (activity is not null)
            activity.Mode = mode;

        return activity;
    }

    /// <summary>Ends the current activity for the given reason and tells the listeners.</summary>
    internal Activity? End(ActivityEndReason reason)
    {
        var activity = Current;

        if (activity is null)
            return null;

        gameState.Activity.Current = null;
        ActivityEnded?.Invoke(new ActivityEndInfo(activity, reason));

        return activity;
    }

    private string ListCourses(FacilityKind kind)
        => string.Join(", ", catalog.Courses.Where(c => c.Kind == kind).Select(c => c.Name));

    private string ListFacilities(FacilityKind kind)
        => string.Join(", ", catalog.Facilities.Where(f => f.Kind == kind).Select(f => f.Name));

    private static string Word(FacilityKind kind) => kind.ToString().ToLowerInvariant();
}