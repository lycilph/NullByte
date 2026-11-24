using NullByte.Core.Domain.Activities;
using NullByte.Core.Services;
using NullByte.Core.Tests.Activities;

namespace NullByte.Core.Tests.Services;

public sealed class ActivityServiceTests
{
    private readonly ActivityTestWorld world = new();

    [Fact]
    public void Starting_a_crime_makes_it_the_current_activity_and_it_repeats_by_default()
    {
        var result = world.Service.StartCrime("sure-thing");

        var crime = Assert.IsType<CrimeActivity>(result.Activity);
        Assert.True(result.Succeeded);
        Assert.True(crime.Repeat);
        Assert.Equal(ActivityMode.Focused, crime.Mode);
        Assert.Equal("Committing sure-thing", crime.Description);
        Assert.Same(crime, world.Service.Current);
    }

    [Fact]
    public void A_crime_can_be_started_once_and_in_the_background()
    {
        var crime = (CrimeActivity)world.Service.StartCrime("SURE-THING", repeat: false, ActivityMode.Background).Activity!;

        Assert.False(crime.Repeat);
        Assert.Equal(ActivityMode.Background, crime.Mode);
    }

    [Fact]
    public void An_unknown_crime_is_explained_and_a_crime_cannot_start_while_busy()
    {
        var unknown = world.Service.StartCrime("nope");

        Assert.False(unknown.Succeeded);
        Assert.Contains("Unknown crime 'nope'. Available: sure-thing, coin-flip, long-shot, mixed", unknown.FailureMessage);

        world.Service.StartCourse(FacilityKind.University, "algorithms");
        var busy = world.Service.StartCrime("sure-thing");

        Assert.False(busy.Succeeded);
        Assert.Contains("already busy", busy.FailureMessage);
    }

    [Fact]
    public void A_crime_needs_no_money()
    {
        world.Player.TrySpendMoney(world.Player.Money);

        Assert.True(world.Service.StartCrime("sure-thing").Succeeded);
    }

    [Fact]
    public void The_chance_and_rewards_of_a_crime_come_from_the_formulas()
    {
        Assert.True(world.Service.Catalog.TryGetCrime("coin-flip", out var crime));

        Assert.Equal(0.5, world.Service.GetCrimeChance(crime), precision: 9);
        Assert.Equal(400, world.Service.GetCrimeRewards(crime, ActivityMode.Background, succeeded: true).Money, precision: 9);
    }

    [Fact]
    public void Starting_work_hires_the_player_at_the_entry_rank()
    {
        var result = world.Service.StartWork("testcorp", "software");

        var job = Assert.IsType<JobActivity>(result.Activity);
        Assert.True(result.Succeeded);
        Assert.Equal("Hired as Intern.", result.Notice);
        Assert.Equal("Working at testcorp (software)", job.Description);
        Assert.Equal(0, job.RankIndex);
        Assert.Equal(new Job("software", 0), world.State.Employment.GetJob("testcorp"));
        Assert.Same(job, world.Service.Current);
    }

    [Fact]
    public void A_company_with_one_career_needs_no_career_name_and_names_are_not_case_sensitive()
    {
        world.GiveHackingLevel(5);

        var result = world.Service.StartWork("HARDCORP");

        Assert.True(result.Succeeded);
        Assert.Equal("Hired as Hard Intern.", result.Notice);
    }

    [Fact]
    public void Several_careers_need_a_choice()
    {
        var result = world.Service.StartWork("testcorp");

        Assert.False(result.Succeeded);
        Assert.Contains("several careers: software, security", result.FailureMessage);
        Assert.Null(world.Service.Current);
    }

    [Theory]
    [InlineData("nope", null, "Unknown company 'nope'")]
    [InlineData("testcorp", "nope", "Unknown career 'nope' at testcorp. Available: software, security")]
    public void Unknown_names_are_explained(string company, string? career, string expected)
    {
        var result = world.Service.StartWork(company, career);

        Assert.False(result.Succeeded);
        Assert.Contains(expected, result.FailureMessage);
    }

    [Fact]
    public void A_player_who_does_not_qualify_is_told_what_is_missing()
    {
        var result = world.Service.StartWork("hardcorp");

        Assert.False(result.Succeeded);
        Assert.Equal("You do not qualify for Hard Intern at hardcorp (software): you need hacking level 5 (you have 1).", result.FailureMessage);
        Assert.Null(world.State.Employment.GetJob("hardcorp"));
        Assert.Null(world.Service.Current);
    }

    [Fact]
    public void The_player_is_hired_at_the_highest_rank_they_qualify_for()
    {
        world.GiveHackingLevel(3);
        world.State.Employment.AddReputation("testcorp", 10);

        var result = world.Service.StartWork("testcorp", "software");

        Assert.Equal("Hired as Developer.", result.Notice);
        Assert.Equal(1, ((JobActivity)result.Activity!).RankIndex);
    }

    [Fact]
    public void A_held_rank_is_resumed_and_a_promotion_that_is_due_is_taken()
    {
        world.State.Employment.SetJob("testcorp", new Job("software", 1));

        var resumed = world.Service.StartWork("testcorp");   // career omitted: the held one is used

        Assert.Equal("Back at work as Developer.", resumed.Notice);
        Assert.Equal(1, ((JobActivity)resumed.Activity!).RankIndex);

        world.Service.Stop();
        world.GiveHackingLevel(10);
        world.State.Employment.AddReputation("testcorp", 100);

        var promoted = world.Service.StartWork("testcorp");

        Assert.Equal("Promoted to Lead.", promoted.Notice);
        Assert.Equal(new Job("software", 2), world.State.Employment.GetJob("testcorp"));
    }

    [Fact]
    public void Another_career_at_the_same_company_replaces_the_job_but_keeps_the_reputation()
    {
        world.Service.StartWork("testcorp", "software");
        world.State.Employment.AddReputation("testcorp", 7);
        world.Service.Stop();

        var result = world.Service.StartWork("testcorp", "security");

        Assert.Equal("Hired as Trainee.", result.Notice);
        Assert.Equal(new Job("security", 0), world.State.Employment.GetJob("testcorp"));
        Assert.Equal(7, world.State.Employment.GetReputation("testcorp"));
    }

    [Fact]
    public void A_failed_application_leaves_the_current_job_as_it_was()
    {
        world.Service.StartWork("testcorp", "software");
        world.Service.Stop();

        var result = world.Service.StartWork("testcorp", "nope");

        Assert.False(result.Succeeded);
        Assert.Equal(new Job("software", 0), world.State.Employment.GetJob("testcorp"));
    }

    [Fact]
    public void Work_cannot_start_while_busy()
    {
        world.Service.StartCourse(FacilityKind.University, "algorithms");

        var result = world.Service.StartWork("testcorp", "software");

        Assert.False(result.Succeeded);
        Assert.Contains("already busy", result.FailureMessage);
    }

    [Fact]
    public void Work_can_start_in_the_background()
    {
        var result = world.Service.StartWork("testcorp", "software", ActivityMode.Background);

        Assert.Equal(ActivityMode.Background, result.Activity!.Mode);
    }

    [Fact]
    public void Rates_and_the_reward_factor_come_from_the_formulas()
    {
        var activity = world.Service.StartCourse(FacilityKind.University, "algorithms").Activity!;

        Assert.Equal(-10, world.Service.GetRates(activity).MoneyPerSecond);
        Assert.Equal(0.8, world.Service.GetRewardFactor(ActivityMode.Background));
    }

    [Fact]
    public void Starting_a_course_makes_it_the_current_activity()
    {
        var result = world.Service.StartCourse(FacilityKind.University, "algorithms");

        Assert.True(result.Succeeded);
        Assert.Same(result.Activity, world.Service.Current);
        Assert.Same(result.Activity, world.State.Activity.Current);
        Assert.Equal(ActivityMode.Focused, result.Activity!.Mode);
    }

    [Fact]
    public void Without_a_facility_the_first_one_of_that_kind_is_used()
    {
        var activity = (ClassActivity)world.Service.StartCourse(FacilityKind.University, "algorithms").Activity!;

        Assert.Equal("plain-university", activity.Facility.Name);
        Assert.Equal("Studying algorithms at plain-university", activity.Description);
    }

    [Fact]
    public void A_named_facility_and_the_background_mode_are_respected()
    {
        var result = world.Service.StartCourse(FacilityKind.University, "FREE-COURSE", "fancy-university", ActivityMode.Background);

        var activity = (ClassActivity)result.Activity!;
        Assert.Equal("fancy-university", activity.Facility.Name);
        Assert.Equal(ActivityMode.Background, activity.Mode);
    }

    [Fact]
    public void Training_at_a_gym_is_described_as_training()
    {
        var result = world.Service.StartCourse(FacilityKind.Gym, "strength");

        Assert.Equal("Training strength at test-gym", result.Activity!.Description);
    }

    [Theory]
    [InlineData(FacilityKind.University, "no-such-course", null, "Unknown course 'no-such-course'")]
    [InlineData(FacilityKind.University, "strength", null, "'strength' is a gym course, not a university course")]
    [InlineData(FacilityKind.Gym, "algorithms", null, "'algorithms' is a university course, not a gym course")]
    [InlineData(FacilityKind.University, "algorithms", "nowhere", "Unknown university 'nowhere'")]
    [InlineData(FacilityKind.University, "algorithms", "test-gym", "'test-gym' is a gym, not a university")]
    public void Invalid_requests_fail_with_a_helpful_message_and_start_nothing(FacilityKind kind, string course, string? facility, string expected)
    {
        var result = world.Service.StartCourse(kind, course, facility);

        Assert.False(result.Succeeded);
        Assert.Contains(expected, result.FailureMessage);
        Assert.Null(world.Service.Current);
    }

    [Fact]
    public void The_unknown_course_message_lists_the_courses_of_that_kind_only()
    {
        var message = world.Service.StartCourse(FacilityKind.University, "nope").FailureMessage;

        Assert.Contains("algorithms", message);
        Assert.Contains("free-course", message);
        Assert.DoesNotContain("strength", message);
    }

    [Fact]
    public void Only_one_activity_runs_at_a_time()
    {
        world.Service.StartCourse(FacilityKind.University, "algorithms");

        var second = world.Service.StartCourse(FacilityKind.Gym, "strength");

        Assert.False(second.Succeeded);
        Assert.Contains("already busy", second.FailureMessage);
        Assert.Equal("algorithms", ((ClassActivity)world.Service.Current!).Course.Name);
    }

    [Fact]
    public void A_course_the_player_cannot_afford_for_one_second_is_not_started()
    {
        world.Player.TrySpendMoney(world.Player.Money - 5);   // $5 left, algorithms costs $10/s

        var result = world.Service.StartCourse(FacilityKind.University, "algorithms");

        Assert.False(result.Succeeded);
        Assert.Contains("Not enough money", result.FailureMessage);
        Assert.Null(world.Service.Current);
    }

    [Fact]
    public void A_free_course_can_be_started_with_no_money()
    {
        world.Player.TrySpendMoney(world.Player.Money);

        Assert.True(world.Service.StartCourse(FacilityKind.University, "free-course").Succeeded);
    }

    [Fact]
    public void Stopping_ends_the_activity_returns_it_and_raises_the_event()
    {
        var started = world.Service.StartCourse(FacilityKind.University, "algorithms").Activity;
        ActivityEndInfo? ended = null;
        world.Service.ActivityEnded += info => ended = info;

        var stopped = world.Service.Stop();

        Assert.Same(started, stopped);
        Assert.Null(world.Service.Current);
        Assert.NotNull(ended);
        Assert.Same(started, ended!.Activity);
        Assert.Equal(ActivityEndReason.Stopped, ended.Reason);
    }

    [Fact]
    public void Stopping_when_idle_returns_null_and_raises_nothing()
    {
        var raised = false;
        world.Service.ActivityEnded += _ => raised = true;

        Assert.Null(world.Service.Stop());
        Assert.False(raised);
    }

    [Fact]
    public void The_mode_can_be_switched_while_an_activity_runs()
    {
        world.Service.StartCourse(FacilityKind.University, "algorithms");

        var background = world.Service.SetMode(ActivityMode.Background);
        Assert.Equal(ActivityMode.Background, world.Service.Current!.Mode);
        Assert.Same(world.Service.Current, background);

        world.Service.SetMode(ActivityMode.Focused);
        Assert.Equal(ActivityMode.Focused, world.Service.Current!.Mode);
    }

    [Fact]
    public void Switching_the_mode_when_idle_returns_null()
    {
        Assert.Null(world.Service.SetMode(ActivityMode.Background));
    }

    [Fact]
    public void A_new_activity_can_start_after_the_last_one_was_stopped()
    {
        world.Service.StartCourse(FacilityKind.University, "algorithms");
        world.Service.Stop();

        Assert.True(world.Service.StartCourse(FacilityKind.Gym, "strength").Succeeded);
    }
}