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