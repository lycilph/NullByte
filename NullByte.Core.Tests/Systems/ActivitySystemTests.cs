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