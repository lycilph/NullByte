using System.Reflection;
using NullByte.Core.Domain;
using NullByte.Core.Domain.Activities;
using NullByte.Core.Formulas;
using NullByte.Core.Scripting;
using NullByte.Core.Scripting.Activities;
using NullByte.Core.Services;
using NullByte.Core.Tests.Activities;

namespace NullByte.Core.Tests.Scripting;

public sealed class NullByteActivitiesApiTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan TenSeconds = TimeSpan.FromSeconds(10);

    private readonly ActivityTestWorld world = new();
    private readonly NullByteActivitiesApi api;

    public NullByteActivitiesApiTests()
    {
        api = new NullByteActivitiesApi(world.State, world.Service, new SkillFormulas(world.Balance));
    }

    // ---------- starting things ----------

    [Fact]
    public void Work_starts_a_job_in_the_background_and_says_how()
    {
        var result = api.Work("testcorp", "software");

        Assert.True(result.Succeeded);
        Assert.Equal("Hired as Intern.", result.Message);
        Assert.Equal(ActivityMode.Background, world.Service.Current!.Mode);
    }

    [Fact]
    public void A_job_started_by_a_script_pays_the_background_rewards()
    {
        var money = world.Player.Money;
        api.Work("testcorp", "software");

        world.System.Update(TenSeconds);

        Assert.Equal(money + 32, world.Player.Money, precision: 6);
        Assert.Equal(8, world.State.Employment.GetReputation("testcorp"), precision: 6);
    }

    [Fact]
    public void The_career_may_be_left_out_when_the_company_has_one()
    {
        world.GiveHackingLevel(5);

        Assert.True(api.Work("hardcorp").Succeeded);
    }

    [Fact]
    public void Study_and_train_start_in_the_background_and_describe_what_began()
    {
        var study = api.Study("algorithms");
        world.Service.Stop();
        var train = api.Train("strength");

        Assert.Equal(new ActivityActionResult(true, "Studying algorithms at plain-university"), study);
        Assert.Equal(new ActivityActionResult(true, "Training strength at test-gym"), train);
        Assert.Equal(ActivityMode.Background, world.Service.Current!.Mode);
    }

    [Fact]
    public void A_named_university_is_used()
    {
        var result = api.Study("algorithms", "fancy-university");

        Assert.Equal("Studying algorithms at fancy-university", result.Message);
    }

    [Theory]
    [InlineData("work", "nope", "Unknown company 'nope'")]
    [InlineData("work", "hardcorp", "you need hacking level 5")]
    [InlineData("study", "strength", "'strength' is a gym course, not a university course")]
    [InlineData("train", "algorithms", "'algorithms' is a university course, not a gym course")]
    [InlineData("study", "nope", "Unknown course 'nope'")]
    public void A_start_that_cannot_happen_is_reported_not_thrown(string what, string name, string expected)
    {
        var result = what switch
        {
            "work" => api.Work(name),
            "study" => api.Study(name),
            _ => api.Train(name)
        };

        Assert.False(result.Succeeded);
        Assert.Contains(expected, result.Message);
        Assert.Null(world.Service.Current);
    }

    [Fact]
    public void Only_one_thing_at_a_time_is_the_same_rule_as_at_the_console()
    {
        api.Study("algorithms");

        var result = api.Work("testcorp", "software");

        Assert.False(result.Succeeded);
        Assert.Contains("already busy", result.Message);
    }

    [Fact]
    public void A_blank_name_is_a_mistake_in_the_script()
    {
        Assert.Throws<ArgumentException>(() => api.Study(""));
        Assert.Throws<ArgumentException>(() => api.Work("  "));
        Assert.Throws<ArgumentException>(() => api.Crime(" ").GetAwaiter().GetResult());
    }

    // ---------- stop and status ----------

    [Fact]
    public void Stop_says_whether_there_was_anything_to_stop()
    {
        Assert.False(api.Stop());

        api.Study("algorithms");

        Assert.True(api.Stop());
        Assert.Null(world.Service.Current);
        Assert.False(api.Stop());
    }

    [Fact]
    public void Status_is_null_when_idle()
    {
        Assert.Null(api.GetStatus());
    }

    [Fact]
    public void Status_shows_what_a_course_has_earned_so_far()
    {
        api.Study("algorithms");
        world.System.Update(TenSeconds);

        var status = api.GetStatus()!;

        Assert.Equal("study", status.Kind);
        Assert.Equal("Studying algorithms at plain-university", status.Description);
        Assert.True(status.InBackground);
        Assert.Equal(TenSeconds, status.Elapsed);
        Assert.Equal(-100, status.NetMoney, precision: 6);
        Assert.Equal(16, status.Experience["hacking"], precision: 6);
        Assert.Equal(0, status.Reputation);
    }

    [Fact]
    public void Status_covers_jobs_and_crimes_too_and_notices_when_the_player_takes_over()
    {
        api.Work("testcorp", "software");
        world.System.Update(TenSeconds);
        var job = api.GetStatus()!;
        world.Service.Stop();

        _ = api.Crime("sure-thing");                              // not awaited: the attempt is still running
        world.System.Update(TimeSpan.FromSeconds(1));
        var crime = api.GetStatus()!;
        world.Service.SetMode(ActivityMode.Focused);              // the player focuses on it from the console

        Assert.Equal("work", job.Kind);
        Assert.Equal(8, job.Reputation, precision: 6);
        Assert.Equal("crime", crime.Kind);
        Assert.Equal(0, crime.Attempts);
        Assert.False(api.GetStatus()!.InBackground);
    }

    [Fact]
    public void Status_experience_is_by_lowercase_skill_name()
    {
        api.Train("strength");
        world.System.Update(TenSeconds);

        var status = api.GetStatus()!;

        Assert.Equal("train", status.Kind);
        Assert.Equal(new[] { "strength" }, status.Experience.Keys.ToArray());
    }

    // ---------- crimes ----------

    [Fact]
    public async Task A_crime_waits_for_the_attempt_and_says_how_it_went()
    {
        var money = world.Player.Money;

        var task = api.Crime("sure-thing");

        Assert.False(task.IsCompleted);
        Assert.Equal(ActivityMode.Background, world.Service.Current!.Mode);

        world.System.Update(TimeSpan.FromSeconds(1));
        Assert.False(task.IsCompleted);

        world.System.Update(TimeSpan.FromSeconds(1));
        var outcome = await task.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        Assert.True(outcome.Started);
        Assert.True(outcome.Completed);
        Assert.True(outcome.Succeeded);
        Assert.Equal(1.0, outcome.Chance, precision: 9);
        Assert.Equal(80, outcome.Money, precision: 6);                       // background: 0.8 * $100
        Assert.Equal(3.2, outcome.Experience["strength"], precision: 6);
        Assert.Equal(money + 80, world.Player.Money, precision: 6);
        Assert.Null(world.Service.Current);                                  // one attempt, then it is over
    }

    [Fact]
    public async Task A_failed_attempt_is_a_completed_outcome_that_did_not_succeed()
    {
        world.Random.Enqueue(0.9);
        var task = api.Crime("coin-flip");

        world.System.Update(TimeSpan.FromSeconds(4));
        var outcome = await task.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        Assert.True(outcome.Completed);
        Assert.False(outcome.Succeeded);
        Assert.Equal(0.5, outcome.Chance, precision: 9);
        Assert.Equal(0, outcome.Money);
        Assert.Equal(1.6, outcome.Experience["dexterity"], precision: 6);   // 8 * 0.25 * 0.8
    }

    [Fact]
    public async Task A_crime_that_cannot_start_returns_at_once_with_the_reason()
    {
        api.Study("algorithms");

        var busy = await api.Crime("sure-thing").WaitAsync(Timeout, TestContext.Current.CancellationToken);
        world.Service.Stop();
        var unknown = await api.Crime("nope").WaitAsync(Timeout, TestContext.Current.CancellationToken);

        Assert.False(busy.Started);
        Assert.Contains("already busy", busy.Message);
        Assert.False(unknown.Started);
        Assert.Contains("Unknown crime 'nope'", unknown.Message);
    }

    [Fact]
    public async Task A_crime_the_player_stops_is_reported_as_not_completed()
    {
        var task = api.Crime("sure-thing");
        world.System.Update(TimeSpan.FromSeconds(1));

        world.Service.Stop();                                                // the player stops it from the console
        var outcome = await task.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        Assert.True(outcome.Started);
        Assert.False(outcome.Completed);
        Assert.False(outcome.Succeeded);
        Assert.Contains("stopped", outcome.Message);
    }

    [Fact]
    public async Task A_script_that_is_killed_while_waiting_gets_a_cancellation_and_the_attempt_carries_on()
    {
        using var killed = new CancellationTokenSource();
        var cancellable = new NullByteActivitiesApi(world.State, world.Service, new SkillFormulas(world.Balance), killed.Token);
        var task = cancellable.Crime("sure-thing");

        killed.Cancel();

        var cancelled = false;
        try { await task.WaitAsync(Timeout, TestContext.Current.CancellationToken); }
        catch (OperationCanceledException) { cancelled = true; }

        Assert.True(cancelled);
        Assert.NotNull(world.Service.Current);                               // the attempt itself carries on

        world.System.Update(TimeSpan.FromSeconds(2));                        // ... and ending it later must not trip over the dead script

        Assert.Null(world.Service.Current);
    }

    // How many handlers an event of the service has. A script that comes and goes must not leave any behind.
    private int Listeners(string eventName)
    {
        var field = typeof(ActivityService).GetField(eventName, BindingFlags.NonPublic | BindingFlags.Instance)!;

        return (field.GetValue(world.Service) as Delegate)?.GetInvocationList().Length ?? 0;
    }

    [Fact]
    public async Task Nothing_is_left_listening_when_a_crime_ends_is_stopped_is_cancelled_or_never_starts()
    {
        // ends
        var finished = api.Crime("sure-thing");
        Assert.Equal(1, Listeners("CrimeAttempted"));
        Assert.Equal(1, Listeners("ActivityEnded"));
        world.System.Update(TimeSpan.FromSeconds(2));
        await finished.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        // stopped
        var stopped = api.Crime("sure-thing");
        world.Service.Stop();
        await stopped.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        // cancelled
        using var killed = new CancellationTokenSource();
        var cancellable = new NullByteActivitiesApi(world.State, world.Service, new SkillFormulas(world.Balance), killed.Token);
        var cancelled = cancellable.Crime("sure-thing");
        killed.Cancel();
        try { await cancelled.WaitAsync(Timeout, TestContext.Current.CancellationToken); } catch (OperationCanceledException) { }
        world.Service.Stop();

        // never starts
        await api.Crime("nope").WaitAsync(Timeout, TestContext.Current.CancellationToken);

        Assert.Equal(0, Listeners("CrimeAttempted"));
        Assert.Equal(0, Listeners("ActivityEnded"));
    }

    [Fact]
    public async Task Two_crimes_one_after_the_other_each_get_their_own_outcome()
    {
        var first = api.Crime("sure-thing");
        world.System.Update(TimeSpan.FromSeconds(2));
        var one = await first.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        var second = api.Crime("coin-flip");
        world.System.Update(TimeSpan.FromSeconds(4));
        var two = await second.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        Assert.True(one.Succeeded);
        Assert.Equal(1.0, one.Chance, precision: 9);
        Assert.Equal(0.5, two.Chance, precision: 9);
    }

    // ---------- questions ----------

    [Fact]
    public void Skill_levels_follow_the_experience_and_ignore_case()
    {
        Assert.Equal(1, api.GetSkillLevel("hacking"));

        world.GiveHackingLevel(7);

        Assert.True(api.GetSkillLevel("Hacking") >= 7);
        Assert.Equal(1, api.GetSkillLevel("CHARISMA"));
    }

    [Fact]
    public void An_unknown_skill_names_the_skills()
    {
        var exception = Assert.Throws<ArgumentException>(() => api.GetSkillLevel("magic"));

        Assert.Contains("Unknown skill 'magic'. The skills are hacking, strength, defense, dexterity, agility, charisma.", exception.Message);
    }

    [Fact]
    public void The_chance_of_a_crime_is_what_the_player_has_now()
    {
        Assert.Equal(0.5, api.GetCrimeChance("coin-flip"), precision: 9);

        world.Player.Skills.AddExperience(SkillKind.Dexterity, new SkillFormulas(world.Balance).CalculateExperienceForLevel(SkillKind.Dexterity, 2));

        Assert.Equal(1.0, api.GetCrimeChance("coin-flip"), precision: 9);
        Assert.Throws<ArgumentException>(() => api.GetCrimeChance("nope"));
    }

    [Fact]
    public void Reputation_and_job_are_per_company_and_an_unknown_company_is_a_mistake()
    {
        Assert.Equal(0, api.GetReputation("testcorp"));
        Assert.Null(api.GetJob("testcorp"));

        api.Work("testcorp", "software");
        world.System.Update(TenSeconds);

        Assert.Equal(8, api.GetReputation("testcorp"), precision: 6);
        Assert.Equal(new JobInfo("testcorp", "software", "Intern", 1, 3), api.GetJob("testcorp"));
        Assert.Null(api.GetJob("hardcorp"));
        Assert.Throws<ArgumentException>(() => api.GetReputation("nope"));
        Assert.Throws<ArgumentException>(() => api.GetJob("nope"));
    }

    [Fact]
    public void The_names_of_everything_can_be_listed_in_the_order_of_the_catalog()
    {
        Assert.Equal(new[] { "sure-thing", "coin-flip", "long-shot", "mixed" }, api.GetCrimes().ToArray());
        Assert.Equal(new[] { "algorithms", "free-course", "strength" }, api.GetCourses().ToArray());
        Assert.Equal(new[] { "testcorp", "hardcorp" }, api.GetCompanies().ToArray());
    }

    // ---------- the shape of the API ----------

    [Fact]
    public void Every_method_of_the_api_declares_a_ram_cost()
    {
        foreach (var method in typeof(INullByteActivitiesApi).GetMethods())
        {
            var cost = method.GetCustomAttribute<RamCostAttribute>();

            Assert.NotNull(cost);
            Assert.True(cost!.Cost > 0, $"{method.Name} should not be free");
        }
    }

    [Fact]
    public void Starting_something_costs_more_than_asking_about_it()
    {
        double Cost(string name) => typeof(INullByteActivitiesApi).GetMethod(name)!.GetCustomAttribute<RamCostAttribute>()!.Cost;

        foreach (var start in new[] { "Work", "Study", "Train", "Crime" })
        {
            Assert.True(Cost(start) > Cost("Stop"));
            Assert.True(Cost("Stop") > Cost("GetJob"));
            Assert.True(Cost("GetJob") > Cost("GetSkillLevel"));
        }
    }

    [Fact]
    public void The_nb_object_has_an_activities_property()
    {
        Assert.NotNull(typeof(INullByteApi).GetProperty(nameof(INullByteApi.Activities)));
    }
}