using NullByte.Core.Domain;
using NullByte.Core.Domain.Activities;
using NullByte.Core.Formulas;
using NullByte.Core.Services;
using NullByte.Core.Tests.Activities;

namespace NullByte.Core.Tests.Systems;

public sealed class ActivitySystemTests
{
    private readonly ActivityTestWorld world = new();

    private static readonly TimeSpan TenSeconds = TimeSpan.FromSeconds(10);

    [Fact]
    public void A_crime_pays_when_the_attempt_ends_not_before()
    {
        var money = world.Player.Money;
        var crime = (CrimeActivity)world.Service.StartCrime("sure-thing").Activity!;
        CrimeAttempt? attempt = null;
        world.Service.CrimeAttempted += a => attempt = a;

        world.System.Update(TimeSpan.FromSeconds(1));

        Assert.Equal(money, world.Player.Money);
        Assert.Equal(0.5, crime.CycleProgress, precision: 6);
        Assert.Null(attempt);

        world.System.Update(TimeSpan.FromSeconds(1));

        Assert.Equal(money + 100, world.Player.Money, precision: 6);
        Assert.Equal(4, world.Player.Skills.GetExperience(SkillKind.Strength), precision: 6);
        Assert.Equal(2, world.Player.Skills.GetExperience(SkillKind.Agility), precision: 6);
        Assert.Equal(0, crime.CycleProgress, precision: 6);
        Assert.NotNull(attempt);
        Assert.True(attempt!.Succeeded);
        Assert.Equal(1.0, attempt.Chance, precision: 9);
        Assert.Same(crime, attempt.Activity);
    }

    [Fact]
    public void The_roll_must_be_below_the_chance_to_succeed()
    {
        var crime = world.Service.StartCrime("coin-flip").Activity!;            // a chance of 50% at level 1
        world.Random.Enqueue(0.49, 0.5);
        var outcomes = new List<bool>();
        world.Service.CrimeAttempted += a => outcomes.Add(a.Succeeded);

        world.System.Update(TimeSpan.FromSeconds(4));
        world.System.Update(TimeSpan.FromSeconds(4));

        Assert.Equal(new[] { true, false }, outcomes);
        Assert.Equal(2, crime.Attempts);
        Assert.Equal(1, crime.Successes);
    }

    [Fact]
    public void A_failed_attempt_pays_no_money_and_gives_a_quarter_of_the_experience()
    {
        var money = world.Player.Money;
        world.Service.StartCrime("coin-flip");
        world.Random.Enqueue(0.9);
        CrimeAttempt? attempt = null;
        world.Service.CrimeAttempted += a => attempt = a;

        world.System.Update(TimeSpan.FromSeconds(4));

        Assert.Equal(money, world.Player.Money);
        Assert.Equal(2, world.Player.Skills.GetExperience(SkillKind.Dexterity), precision: 6);
        Assert.False(attempt!.Succeeded);
        Assert.Equal(0, attempt.Rewards.Money);
        Assert.NotNull(world.Service.Current);                                   // it goes on
    }

    [Fact]
    public void In_the_background_the_rewards_are_reduced_but_not_the_chance()
    {
        var money = world.Player.Money;
        world.Service.StartCrime("sure-thing", mode: ActivityMode.Background);
        CrimeAttempt? attempt = null;
        world.Service.CrimeAttempted += a => attempt = a;

        world.System.Update(TimeSpan.FromSeconds(2));

        Assert.Equal(money + 80, world.Player.Money, precision: 6);
        Assert.Equal(3.2, world.Player.Skills.GetExperience(SkillKind.Strength), precision: 6);
        Assert.Equal(1.6, world.Player.Skills.GetExperience(SkillKind.Agility), precision: 6);
        Assert.Equal(1.0, attempt!.Chance, precision: 9);
    }

    [Fact]
    public void The_mode_at_the_end_of_an_attempt_decides_its_rewards()
    {
        var money = world.Player.Money;
        world.Service.StartCrime("sure-thing");

        world.System.Update(TimeSpan.FromSeconds(2));                            // focused: $100
        world.Service.SetMode(ActivityMode.Background);
        world.System.Update(TimeSpan.FromSeconds(2));                            // background: $80

        Assert.Equal(money + 180, world.Player.Money, precision: 6);
    }

    [Fact]
    public void Several_attempts_can_end_in_one_update_and_the_left_over_time_carries_on()
    {
        var money = world.Player.Money;
        var crime = (CrimeActivity)world.Service.StartCrime("sure-thing").Activity!;

        world.System.Update(TimeSpan.FromSeconds(7));                            // 3 attempts of 2 s, and 1 s into the 4th

        Assert.Equal(3, crime.Attempts);
        Assert.Equal(money + 300, world.Player.Money, precision: 6);
        Assert.Equal(0.5, crime.CycleProgress, precision: 6);

        world.System.Update(TimeSpan.FromSeconds(1));

        Assert.Equal(4, crime.Attempts);
    }

    [Fact]
    public void Every_attempt_is_rolled_with_the_skills_the_player_has_by_then()
    {
        // Dexterity is 15.8 xp: level 1, and the 8 xp of a success takes it to level 2.
        var needed = new SkillFormulas(world.Balance).CalculateExperienceForLevel(SkillKind.Dexterity, 2);
        world.Player.Skills.AddExperience(SkillKind.Dexterity, needed - 1);
        world.Service.StartCrime("coin-flip");
        var chances = new List<double>();
        world.Service.CrimeAttempted += a => chances.Add(a.Chance);

        world.System.Update(TimeSpan.FromSeconds(8));                            // two attempts in one update

        Assert.Equal(2, chances.Count);
        Assert.Equal(0.5, chances[0], precision: 9);
        Assert.Equal(1.0, chances[1], precision: 9);
    }

    [Fact]
    public void A_crime_that_is_not_repeated_ends_after_one_attempt_and_spends_only_that_time()
    {
        var money = world.Player.Money;
        var crime = (CrimeActivity)world.Service.StartCrime("sure-thing", repeat: false).Activity!;
        ActivityEndInfo? ended = null;
        world.Service.ActivityEnded += info => ended = info;

        world.System.Update(TimeSpan.FromSeconds(5));

        Assert.Null(world.Service.Current);
        Assert.Equal(money + 100, world.Player.Money, precision: 6);
        Assert.Equal(1, crime.Attempts);
        Assert.Equal(TimeSpan.FromSeconds(2), crime.Elapsed);
        Assert.NotNull(ended);
        Assert.Same(crime, ended!.Activity);
        Assert.Equal(ActivityEndReason.Completed, ended.Reason);
    }

    [Fact]
    public void The_crime_remembers_its_attempts_and_what_they_gave()
    {
        var crime = world.Service.StartCrime("sure-thing").Activity!;

        world.System.Update(TimeSpan.FromSeconds(6));

        var progress = crime.Progress;
        Assert.Equal(3, progress.Attempts);
        Assert.Equal(3, progress.Successes);
        Assert.Equal(300, progress.NetMoney, precision: 6);
        Assert.Equal(12, progress.ExperienceGained[SkillKind.Strength], precision: 6);
        Assert.Equal(TimeSpan.FromSeconds(6), progress.Elapsed);
    }

    [Fact]
    public void A_crime_never_ends_for_lack_of_money_and_earns_no_reputation()
    {
        world.Player.TrySpendMoney(world.Player.Money);
        world.Service.StartCrime("sure-thing");

        world.System.Update(TimeSpan.FromHours(1));

        Assert.NotNull(world.Service.Current);
        Assert.Equal(1800 * 100, world.Player.Money, precision: 3);
        Assert.Empty(world.State.Employment.Reputation);
    }

    [Fact]
    public void The_totals_are_up_to_date_when_the_attempts_are_announced()
    {
        var crime = world.Service.StartCrime("sure-thing").Activity!;
        var seen = new List<int>();
        world.Service.CrimeAttempted += a => seen.Add(a.Activity.Attempts);

        world.System.Update(TimeSpan.FromSeconds(6));                            // three attempts in one update

        Assert.Equal(new[] { 3, 3, 3 }, seen);
        Assert.Equal(3, crime.Attempts);
    }

    [Fact]
    public void A_job_pays_a_salary_and_gives_experience_and_reputation_to_the_company()
    {
        var money = world.Player.Money;
        world.Service.StartWork("testcorp", "software");

        world.System.Update(TenSeconds);

        Assert.Equal(money + 40, world.Player.Money, precision: 6);
        Assert.Equal(5, world.Player.Skills.GetExperience(SkillKind.Hacking), precision: 6);
        Assert.Equal(10, world.State.Employment.GetReputation("testcorp"), precision: 6);
        Assert.Equal(0, world.State.Employment.GetReputation("hardcorp"));
    }

    [Fact]
    public void A_background_job_gives_eighty_percent_of_every_reward()
    {
        var money = world.Player.Money;
        world.Service.StartWork("testcorp", "software", ActivityMode.Background);

        world.System.Update(TenSeconds);

        Assert.Equal(money + 32, world.Player.Money, precision: 6);
        Assert.Equal(4, world.Player.Skills.GetExperience(SkillKind.Hacking), precision: 6);
        Assert.Equal(8, world.State.Employment.GetReputation("testcorp"), precision: 6);
    }

    [Fact]
    public void The_job_remembers_the_reputation_it_has_earned()
    {
        var job = world.Service.StartWork("testcorp", "software").Activity!;

        world.System.Update(TenSeconds);

        Assert.Equal(10, job.ReputationGained, precision: 6);
        Assert.Equal(10, job.Progress.ReputationGained, precision: 6);
        Assert.Equal(40, job.NetMoney, precision: 6);
    }

    [Fact]
    public void A_job_never_ends_for_lack_of_money()
    {
        world.Player.TrySpendMoney(world.Player.Money);
        world.Service.StartWork("testcorp", "software");

        world.System.Update(TimeSpan.FromHours(1));

        Assert.NotNull(world.Service.Current);
    }

    [Fact]
    public void The_player_is_promoted_when_skill_and_reputation_are_both_enough()
    {
        world.GiveHackingLevel(3);
        var job = (JobActivity)world.Service.StartWork("testcorp", "software").Activity!;
        JobPromotion? promotion = null;
        world.Service.JobPromoted += p => promotion = p;

        world.System.Update(TenSeconds);                       // reputation 10: enough for Developer

        Assert.Equal(1, job.RankIndex);
        Assert.Equal("Developer", job.Rank.Title);
        Assert.Equal(new Job("software", 1), world.State.Employment.GetJob("testcorp"));
        Assert.NotNull(promotion);
        Assert.Same(job, promotion!.Job);

        var money = world.Player.Money;
        world.System.Update(TimeSpan.FromSeconds(1));          // the new salary is $10/s

        Assert.Equal(money + 10, world.Player.Money, precision: 6);
    }

    [Fact]
    public void Reputation_alone_does_not_promote()
    {
        var job = (JobActivity)world.Service.StartWork("testcorp", "software").Activity!;
        var promoted = false;
        world.Service.JobPromoted += _ => promoted = true;

        world.System.Update(TimeSpan.FromSeconds(50));          // plenty of reputation, hacking level still 1

        Assert.Equal(0, job.RankIndex);
        Assert.False(promoted);
    }

    [Fact]
    public void Skill_alone_does_not_promote()
    {
        world.GiveHackingLevel(10);
        var job = (JobActivity)world.Service.StartWork("testcorp", "software").Activity!;

        world.System.Update(TimeSpan.FromSeconds(5));          // reputation 5, needs 10

        Assert.Equal(0, job.RankIndex);
    }

    [Fact]
    public void Several_ranks_can_be_earned_at_once_and_are_announced_once()
    {
        world.GiveHackingLevel(10);
        var job = (JobActivity)world.Service.StartWork("testcorp", "software").Activity!;
        var promotions = 0;
        world.Service.JobPromoted += _ => promotions++;

        world.System.Update(TimeSpan.FromSeconds(100));        // reputation 100 in one update

        Assert.Equal(2, job.RankIndex);
        Assert.True(job.IsTopRank);
        Assert.Equal(1, promotions);

        world.System.Update(TimeSpan.FromSeconds(100));        // nothing above the top rank: no error, no more promotions

        Assert.Equal(2, job.RankIndex);
        Assert.Equal(1, promotions);
    }

    [Fact]
    public void Reputation_and_rank_are_kept_when_the_player_stops_and_comes_back()
    {
        world.GiveHackingLevel(3);
        world.Service.StartWork("testcorp", "software");
        world.System.Update(TenSeconds);                       // promoted to Developer
        world.Service.Stop();

        var again = world.Service.StartWork("testcorp");

        Assert.Equal("Back at work as Developer.", again.Notice);
        Assert.Equal(10, world.State.Employment.GetReputation("testcorp"), precision: 6);
    }

    [Fact]
    public void A_course_earns_no_reputation()
    {
        world.Service.StartCourse(FacilityKind.University, "algorithms");

        world.System.Update(TenSeconds);

        Assert.Empty(world.State.Employment.Reputation);
    }

    [Fact]
    public void A_progress_snapshot_does_not_change_when_the_activity_moves_on()
    {
        var activity = world.Service.StartCourse(FacilityKind.University, "algorithms").Activity!;
        world.System.Update(TenSeconds);

        var snapshot = activity.Progress;
        world.System.Update(TenSeconds);

        Assert.Equal(TenSeconds, snapshot.Elapsed);
        Assert.Equal(20, snapshot.ExperienceGained[SkillKind.Hacking], precision: 6);
        Assert.Equal(TimeSpan.FromSeconds(20), activity.Progress.Elapsed);
        Assert.Equal(40, activity.Progress.ExperienceGained[SkillKind.Hacking], precision: 6);
    }

    [Fact]
    public void Nothing_happens_when_the_player_is_idle()
    {
        var money = world.Player.Money;

        world.System.Update(TenSeconds);

        Assert.Equal(money, world.Player.Money);
        Assert.Equal(0, world.Player.HackingExperience);
    }

    [Fact]
    public void A_focused_course_costs_money_and_gives_full_experience()
    {
        var money = world.Player.Money;
        world.Service.StartCourse(FacilityKind.University, "algorithms");

        world.System.Update(TenSeconds);

        Assert.Equal(money - 100, world.Player.Money, precision: 6);
        Assert.Equal(20, world.Player.Skills.GetExperience(SkillKind.Hacking), precision: 6);
    }

    [Fact]
    public void A_background_course_gives_less_experience_but_costs_the_same()
    {
        var money = world.Player.Money;
        world.Service.StartCourse(FacilityKind.University, "algorithms", mode: ActivityMode.Background);

        world.System.Update(TenSeconds);

        Assert.Equal(money - 100, world.Player.Money, precision: 6);
        Assert.Equal(16, world.Player.Skills.GetExperience(SkillKind.Hacking), precision: 6);
    }

    [Fact]
    public void Switching_to_the_background_changes_the_rate_from_the_next_update()
    {
        world.Service.StartCourse(FacilityKind.University, "algorithms");

        world.System.Update(TenSeconds);                        // focused: 20
        world.Service.SetMode(ActivityMode.Background);
        world.System.Update(TenSeconds);                        // background: 16

        Assert.Equal(36, world.Player.Skills.GetExperience(SkillKind.Hacking), precision: 6);
    }

    [Fact]
    public void A_gym_course_trains_its_own_skill()
    {
        world.Service.StartCourse(FacilityKind.Gym, "strength");

        world.System.Update(TenSeconds);

        Assert.Equal(10, world.Player.Skills.GetExperience(SkillKind.Strength), precision: 6);
        Assert.Equal(0, world.Player.Skills.GetExperience(SkillKind.Hacking));
    }

    [Fact]
    public void The_activity_remembers_what_it_has_earned()
    {
        var activity = world.Service.StartCourse(FacilityKind.University, "algorithms").Activity!;

        world.System.Update(TenSeconds);
        world.System.Update(TimeSpan.FromSeconds(5));

        Assert.Equal(TimeSpan.FromSeconds(15), activity.Elapsed);
        Assert.Equal(-150, activity.NetMoney, precision: 6);
        Assert.Equal(30, activity.ExperienceGained[SkillKind.Hacking], precision: 6);
    }

    [Fact]
    public void A_course_the_player_can_no_longer_afford_ends_and_gives_nothing_for_that_update()
    {
        world.Player.TrySpendMoney(world.Player.Money - 15);    // $15 left
        var activity = world.Service.StartCourse(FacilityKind.University, "algorithms").Activity!;
        ActivityEndInfo? ended = null;
        world.Service.ActivityEnded += info => ended = info;

        world.System.Update(TimeSpan.FromSeconds(1));           // $10: still affordable
        world.System.Update(TimeSpan.FromSeconds(1));           // $10 again, but only $5 left

        Assert.Null(world.Service.Current);
        Assert.Equal(5, world.Player.Money, precision: 6);
        Assert.Equal(2, world.Player.Skills.GetExperience(SkillKind.Hacking), precision: 6);   // only the first second
        Assert.NotNull(ended);
        Assert.Same(activity, ended!.Activity);
        Assert.Equal(ActivityEndReason.CannotAfford, ended.Reason);
    }

    [Fact]
    public void A_free_course_never_ends_for_lack_of_money()
    {
        world.Player.TrySpendMoney(world.Player.Money);
        world.Service.StartCourse(FacilityKind.University, "free-course");

        world.System.Update(TimeSpan.FromHours(1));

        Assert.NotNull(world.Service.Current);
        Assert.Equal(3600, world.Player.Skills.GetExperience(SkillKind.Charisma), precision: 6);
    }

    [Fact]
    public void A_zero_delta_does_nothing()
    {
        var money = world.Player.Money;
        world.Service.StartCourse(FacilityKind.University, "algorithms");

        world.System.Update(TimeSpan.Zero);

        Assert.Equal(money, world.Player.Money);
    }

    [Fact]
    public void Studying_raises_the_hacking_level()
    {
        var before = new HackingFormulas(world.Balance).CalculateHackingLevel(world.Player.HackingExperience);
        world.Service.StartCourse(FacilityKind.University, "free-course");     // charisma only
        world.Service.Stop();
        world.Service.StartCourse(FacilityKind.University, "algorithms");

        world.System.Update(TimeSpan.FromSeconds(90));                          // 180 xp for $900

        var after = new HackingFormulas(world.Balance).CalculateHackingLevel(world.Player.HackingExperience);
        Assert.True(after > before);
    }
}