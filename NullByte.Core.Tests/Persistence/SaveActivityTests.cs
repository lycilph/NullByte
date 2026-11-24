using NullByte.Core.Configuration;
using NullByte.Core.Domain;
using NullByte.Core.Domain.Activities;
using NullByte.Core.Formulas;
using NullByte.Core.Persistence;
using NullByte.Core.Services;
using NullByte.Core.Systems;
using NullByte.Core.Tests.Activities;

namespace NullByte.Core.Tests.Persistence;

public sealed class SaveActivityTests
{
    private readonly ActivityTestWorld world = new();
    private readonly SaveGameSerializer serializer = new();

    private SaveGame Capture() => SaveGameMapper.Capture(world.State, TimeSpan.FromMinutes(5), [], DateTimeOffset.UnixEpoch);

    // Through the real JSON, as a save on disk would be.
    private RestoredGame RoundTrip(ActivityCatalog? catalog = null)
        => SaveGameMapper.Restore(serializer.Deserialize(serializer.Serialize(Capture())), catalog ?? ActivityTestWorld.Catalog);

    private static (ActivityService Service, ActivitySystem System) Resume(RestoredGame restored)
    {
        var formulas = new ActivityFormulas(new BalanceConfig());
        var service = new ActivityService(restored.State, ActivityTestWorld.Catalog, formulas);

        return (service, new ActivitySystem(restored.State, formulas, service, new ScriptedRandom()));
    }

    private static readonly TimeSpan TenSeconds = TimeSpan.FromSeconds(10);

    // ---------- skills ----------

    [Fact]
    public void Every_skill_is_saved_and_restored()
    {
        var amounts = new Dictionary<SkillKind, double>
        {
            [SkillKind.Hacking] = 11.5,
            [SkillKind.Strength] = 22,
            [SkillKind.Defense] = 33,
            [SkillKind.Dexterity] = 44.25,
            [SkillKind.Agility] = 55,
            [SkillKind.Charisma] = 66
        };

        foreach (var (skill, amount) in amounts)
            world.Player.Skills.AddExperience(skill, amount);

        var restored = RoundTrip();

        foreach (var (skill, amount) in amounts)
            Assert.Equal(amount, restored.State.Player.Skills.GetExperience(skill));
    }

    [Fact]
    public void The_file_names_all_six_skills_and_has_no_separate_hacking_experience()
    {
        var json = serializer.Serialize(Capture());

        foreach (var name in new[] { "hacking", "strength", "defense", "dexterity", "agility", "charisma" })
            Assert.Contains($"\"{name}\":", json);

        Assert.DoesNotContain("hackingExperience", json);
    }

    [Fact]
    public void The_same_game_always_gives_the_same_file()
    {
        world.Service.StartWork("testcorp", "software");
        world.System.Update(TenSeconds);

        Assert.Equal(serializer.Serialize(Capture()), serializer.Serialize(Capture()));
    }

    // ---------- idle ----------

    [Fact]
    public void An_idle_player_has_no_activity_in_the_save_and_none_after_loading()
    {
        var json = serializer.Serialize(Capture());

        Assert.Contains("\"activity\": null", json);
        Assert.Null(RoundTrip().State.Activity.Current);
    }

    // ---------- classes ----------

    [Fact]
    public void A_course_is_resumed_in_the_background_with_what_it_had_earned()
    {
        world.Service.StartCourse(FacilityKind.University, "algorithms", "fancy-university");
        world.System.Update(TenSeconds);                       // focused: -$300, +40 hacking xp

        var restored = RoundTrip();

        var course = Assert.IsType<ClassActivity>(restored.State.Activity.Current);
        Assert.Equal("algorithms", course.Course.Name);
        Assert.Equal("fancy-university", course.Facility.Name);
        Assert.Equal(ActivityMode.Background, course.Mode);
        Assert.Equal(TenSeconds, course.Elapsed);
        Assert.Equal(-300, course.NetMoney, precision: 6);
        Assert.Equal(40, course.ExperienceGained[SkillKind.Hacking], precision: 6);
    }

    [Fact]
    public void A_resumed_course_carries_on_where_it_stopped()
    {
        world.Service.StartCourse(FacilityKind.University, "algorithms", "fancy-university");
        world.System.Update(TenSeconds);
        var restored = RoundTrip();
        var (_, system) = Resume(restored);
        var money = restored.State.Player.Money;

        system.Update(TenSeconds);                             // background now: 0.8 * 4 xp/s

        var course = restored.State.Activity.Current!;
        Assert.Equal(money - 300, restored.State.Player.Money, precision: 6);
        Assert.Equal(TimeSpan.FromSeconds(20), course.Elapsed);
        Assert.Equal(-600, course.NetMoney, precision: 6);
        Assert.Equal(72, course.ExperienceGained[SkillKind.Hacking], precision: 6);
    }

    [Fact]
    public void Only_the_kind_of_activity_that_it_is_appears_in_the_file()
    {
        world.Service.StartCourse(FacilityKind.Gym, "strength");

        var json = serializer.Serialize(Capture());

        Assert.Contains("\"class\":", json);
        Assert.DoesNotContain("\"job\":", json);
        Assert.DoesNotContain("\"crime\":", json);
    }

    // ---------- jobs ----------

    [Fact]
    public void A_job_is_resumed_at_the_rank_the_player_had_reached()
    {
        world.GiveHackingLevel(3);
        world.Service.StartWork("testcorp", "software");
        world.System.Update(TenSeconds);                       // reputation 10: promoted to Developer

        var restored = RoundTrip();

        var job = Assert.IsType<JobActivity>(restored.State.Activity.Current);
        Assert.Equal("testcorp", job.Company.Name);
        Assert.Equal("software", job.Career.Name);
        Assert.Equal(1, job.RankIndex);
        Assert.Equal("Developer", job.Rank.Title);
        Assert.Equal(ActivityMode.Background, job.Mode);
        Assert.Equal(10, job.ReputationGained, precision: 6);
        Assert.Equal(new Job("software", 1), restored.State.Employment.GetJob("testcorp"));
        Assert.Equal(10, restored.State.Employment.GetReputation("testcorp"), precision: 6);
    }

    [Fact]
    public void A_resumed_job_pays_the_salary_of_the_saved_rank()
    {
        world.GiveHackingLevel(3);
        world.Service.StartWork("testcorp", "software");
        world.System.Update(TenSeconds);
        var restored = RoundTrip();
        var (_, system) = Resume(restored);
        var money = restored.State.Player.Money;

        system.Update(TimeSpan.FromSeconds(1));                // Developer: $10/s, 0.8 in the background

        Assert.Equal(money + 8, restored.State.Player.Money, precision: 6);
    }

    [Fact]
    public void Jobs_and_reputation_are_kept_for_every_company_even_when_the_player_is_not_working()
    {
        world.Service.StartWork("testcorp", "software");
        world.System.Update(TenSeconds);
        world.Service.Stop();
        world.State.Employment.SetJob("hardcorp", new Job("software", 0));
        world.State.Employment.AddReputation("hardcorp", 3.5);

        var restored = RoundTrip();

        Assert.Null(restored.State.Activity.Current);
        Assert.Equal(new Job("software", 0), restored.State.Employment.GetJob("testcorp"));
        Assert.Equal(new Job("software", 0), restored.State.Employment.GetJob("hardcorp"));
        Assert.Equal(10, restored.State.Employment.GetReputation("testcorp"), precision: 6);
        Assert.Equal(3.5, restored.State.Employment.GetReputation("hardcorp"));
    }

    // ---------- crimes ----------

    [Fact]
    public void A_crime_is_resumed_part_way_through_its_attempt()
    {
        world.Service.StartCrime("sure-thing");
        world.System.Update(TimeSpan.FromSeconds(5));          // two attempts, and 1 s into the third

        var restored = RoundTrip();

        var crime = Assert.IsType<CrimeActivity>(restored.State.Activity.Current);
        Assert.Equal("sure-thing", crime.Crime.Name);
        Assert.True(crime.Repeat);
        Assert.Equal(ActivityMode.Background, crime.Mode);
        Assert.Equal(2, crime.Attempts);
        Assert.Equal(2, crime.Successes);
        Assert.Equal(0.5, crime.CycleProgress, precision: 6);
    }

    [Fact]
    public void A_resumed_crime_finishes_the_attempt_it_was_in()
    {
        world.Service.StartCrime("sure-thing");
        world.System.Update(TimeSpan.FromSeconds(5));
        var restored = RoundTrip();
        var (_, system) = Resume(restored);
        var money = restored.State.Player.Money;

        system.Update(TimeSpan.FromSeconds(1));                // the second second of the third attempt

        Assert.Equal(3, restored.State.Activity.Current!.Attempts);
        Assert.Equal(money + 80, restored.State.Player.Money, precision: 6);     // background: 0.8 * $100
    }

    [Fact]
    public void A_crime_that_was_not_repeating_is_still_not_repeating()
    {
        world.Service.StartCrime("sure-thing", repeat: false);

        var crime = (CrimeActivity)RoundTrip().State.Activity.Current!;

        Assert.False(crime.Repeat);
    }

    // ---------- the game's data has changed since the save ----------

    private static ActivityCatalog CatalogWithout(Func<Course, bool>? dropCourse = null, Func<Company, bool>? dropCompany = null, Func<Crime, Crime>? changeCrime = null)
        => new(
            ActivityTestWorld.Catalog.Facilities,
            ActivityTestWorld.Catalog.Courses.Where(c => !(dropCourse?.Invoke(c) ?? false)).ToArray(),
            ActivityTestWorld.Catalog.Companies.Where(c => !(dropCompany?.Invoke(c) ?? false)).ToArray(),
            ActivityTestWorld.Catalog.Crimes.Select(c => changeCrime?.Invoke(c) ?? c).ToArray());

    [Fact]
    public void A_course_that_no_longer_exists_is_not_resumed_and_the_player_is_told()
    {
        world.Service.StartCourse(FacilityKind.University, "algorithms");
        world.Player.AddMoney(1);

        var restored = RoundTrip(CatalogWithout(dropCourse: c => c.Name == "algorithms"));

        Assert.Null(restored.State.Activity.Current);
        var warning = Assert.Single(restored.Warnings);
        Assert.Contains("'algorithms' no longer exists", warning);
        Assert.Contains("not resumed", warning);
        Assert.Equal(world.Player.Money, restored.State.Player.Money);                 // everything else is untouched
    }

    [Fact]
    public void A_company_that_no_longer_exists_loses_the_job_and_the_activity_but_keeps_the_reputation()
    {
        world.Service.StartWork("testcorp", "software");
        world.System.Update(TenSeconds);

        var restored = RoundTrip(CatalogWithout(dropCompany: c => c.Name == "testcorp"));

        Assert.Null(restored.State.Activity.Current);
        Assert.Null(restored.State.Employment.GetJob("testcorp"));
        Assert.Equal(10, restored.State.Employment.GetReputation("testcorp"), precision: 6);
        Assert.Equal(2, restored.Warnings.Count);
        Assert.Contains(restored.Warnings, w => w.Contains("company 'testcorp' no longer exists, so your job there was left out"));
        Assert.Contains(restored.Warnings, w => w.Contains("not resumed"));
    }

    [Fact]
    public void A_rank_that_no_longer_exists_is_left_out_and_the_player_is_hired_again_by_working_there()
    {
        world.State.Employment.SetJob("testcorp", new Job("software", 2));              // Lead
        var shorter = CatalogWithout();                                                  // same data...
        var software = ActivityTestWorld.Catalog.Companies[0].Careers[0];
        var trimmed = new Company("testcorp", [new CareerPath("software", software.Ranks.Take(1).ToArray())]);
        var catalog = new ActivityCatalog(shorter.Facilities, shorter.Courses, [trimmed, shorter.Companies[1]], shorter.Crimes);

        var restored = RoundTrip(catalog);

        Assert.Null(restored.State.Employment.GetJob("testcorp"));
        Assert.Contains(restored.Warnings, w => w.Contains("career 'software' at 'testcorp' no longer has that position"));
    }

    [Fact]
    public void A_crime_whose_attempt_has_got_shorter_finishes_its_attempt_at_the_next_update()
    {
        world.Service.StartCrime("coin-flip");                                           // 4 s attempts
        world.System.Update(TimeSpan.FromSeconds(3));
        var quicker = CatalogWithout(changeCrime: c => c.Name == "coin-flip" ? c with { DurationSeconds = 2 } : c);

        var restored = RoundTrip(quicker);

        var crime = (CrimeActivity)restored.State.Activity.Current!;
        Assert.Equal(1.0, crime.CycleProgress);                                          // 3 s into a 2 s attempt, shown as full
        Assert.Empty(restored.Warnings);
    }

    [Fact]
    public void A_normal_save_has_no_warnings_and_remembers_the_catalog_it_was_restored_with()
    {
        world.Service.StartWork("testcorp", "software");

        var restored = RoundTrip();

        Assert.Empty(restored.Warnings);
        Assert.Same(ActivityTestWorld.Catalog, restored.ActivityCatalog);
    }

    [Fact]
    public void Restoring_without_a_catalog_uses_the_one_that_ships_with_the_game()
    {
        var restored = SaveGameMapper.Restore(serializer.Deserialize(serializer.Serialize(Capture())));

        Assert.NotNull(restored.ActivityCatalog);
        Assert.NotEmpty(restored.ActivityCatalog!.Companies);
    }

    // ---------- bad saves are refused ----------

    private static SaveGame WithActivity(SaveGame save, ActivitySave activity) => save with { Activity = activity };

    private static ActivityProgressSave Progress(
        double elapsed = 10, double netMoney = 0, double reputation = 0, int attempts = 0, int successes = 0,
        Dictionary<string, double>? experience = null)
        => new()
        {
            Elapsed = TimeSpan.FromSeconds(elapsed),
            NetMoney = netMoney,
            Experience = experience ?? new Dictionary<string, double>(),
            ReputationGained = reputation,
            Attempts = attempts,
            Successes = successes
        };

    public static TheoryData<string, Func<SaveGame, SaveGame>, string> BadSaves => new()
    {
        { "unknown skill", s => s with { Player = s.Player with { Skills = new Dictionary<string, double> { ["magic"] = 1 } } }, "unknown skill 'magic'" },
        { "negative experience", s => s with { Player = s.Player with { Skills = new Dictionary<string, double> { ["hacking"] = -1 } } }, "invalid data" },
        { "the same skill twice", s => s with { Player = s.Player with { Skills = new Dictionary<string, double> { ["hacking"] = 1, ["Hacking"] = 2 } } }, "more than once" },
        { "two kinds of activity", s => WithActivity(s, new ActivitySave { Progress = Progress(), Class = new ClassActivitySave { Course = "algorithms", Facility = "plain-university" }, Crime = new CrimeActivitySave { Crime = "sure-thing", Repeat = true, CycleSeconds = 0 } }), "exactly one" },
        { "no kind of activity", s => WithActivity(s, new ActivitySave { Progress = Progress() }), "exactly one" },
        { "a course at the wrong kind of place", s => WithActivity(s, new ActivitySave { Progress = Progress(), Class = new ClassActivitySave { Course = "strength", Facility = "plain-university" } }), "invalid data" },
        { "more successes than attempts", s => WithActivity(s, new ActivitySave { Progress = Progress(attempts: 1, successes: 2), Crime = new CrimeActivitySave { Crime = "sure-thing", Repeat = true, CycleSeconds = 0 } }), "attempts" },
        { "negative time", s => WithActivity(s, new ActivitySave { Progress = Progress(elapsed: -1), Crime = new CrimeActivitySave { Crime = "sure-thing", Repeat = true, CycleSeconds = 0 } }), "negative time" },
        { "unknown skill in the progress", s => WithActivity(s, new ActivitySave { Progress = Progress(experience: new() { ["magic"] = 1 }), Crime = new CrimeActivitySave { Crime = "sure-thing", Repeat = true, CycleSeconds = 0 } }), "unknown skill 'magic'" },
        { "negative reputation in the progress", s => WithActivity(s, new ActivitySave { Progress = Progress(reputation: -1), Crime = new CrimeActivitySave { Crime = "sure-thing", Repeat = true, CycleSeconds = 0 } }), "reputation" },
        { "negative time into the attempt", s => WithActivity(s, new ActivitySave { Progress = Progress(), Crime = new CrimeActivitySave { Crime = "sure-thing", Repeat = true, CycleSeconds = -1 } }), "attempt" },
        { "working without the job", s => WithActivity(s, new ActivitySave { Progress = Progress(), Job = new JobActivitySave { Company = "testcorp", Career = "software" } }), "holds no such job" },
        { "reputation listed twice", s => s with { Employment = new EmploymentSave { Jobs = [], Reputation = [new ReputationSave { Company = "testcorp", Amount = 1 }, new ReputationSave { Company = "TESTCORP", Amount = 2 }] } }, "more than once" },
        { "negative reputation", s => s with { Employment = new EmploymentSave { Jobs = [], Reputation = [new ReputationSave { Company = "testcorp", Amount = -1 }] } }, "invalid data" },
        { "two jobs at one company", s => s with { Employment = new EmploymentSave { Jobs = [new JobSave { Company = "testcorp", Career = "software", RankIndex = 0 }, new JobSave { Company = "testcorp", Career = "security", RankIndex = 0 }], Reputation = [] } }, "more than one job" },
        { "a negative rank", s => s with { Employment = new EmploymentSave { Jobs = [new JobSave { Company = "testcorp", Career = "software", RankIndex = -1 }], Reputation = [] } }, "negative rank" },
    };

    [Theory]
    [MemberData(nameof(BadSaves))]
    public void A_save_with_invalid_data_is_refused(string _, Func<SaveGame, SaveGame> damage, string expected)
    {
        var save = damage(Capture());

        var exception = Assert.Throws<SaveFormatException>(() => SaveGameMapper.Restore(save, ActivityTestWorld.Catalog));

        Assert.Contains(expected, exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}