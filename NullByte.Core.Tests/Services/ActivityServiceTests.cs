using NullByte.Core.Domain.Activities;
using NullByte.Core.Services;
using NullByte.Core.Tests.Activities;

namespace NullByte.Core.Tests.Services;

public sealed class ActivityServiceTests
{
    private readonly ActivityTestWorld world = new();

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