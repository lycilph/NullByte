using NullByte.Core.Domain;
using NullByte.Core.Domain.Activities;

namespace NullByte.ConsoleUI.Tests.Commands;

/// <summary>Starting and listing courses at the universities and the gym.</summary>
public sealed class StudyAndTrainCommandTests : ActivityCommandsTestBase
{
    [Fact]
    public async Task Study_without_arguments_lists_the_courses_and_universities()
    {
        await Run("study");

        Assert.Contains("algorithms", Terminal.Text);
        Assert.Contains("costs $10.00/s, +2.00 hacking xp/s", Terminal.Text);
        Assert.Contains("fancy-university", Terminal.Text);
        Assert.DoesNotContain("strength", Terminal.Text);
        Assert.Null(Service.Current);
    }

    [Fact]
    public async Task Train_without_arguments_lists_the_gym_courses_only()
    {
        await Run("train");

        Assert.Contains("strength", Terminal.Text);
        Assert.Contains("test-gym", Terminal.Text);
        Assert.DoesNotContain("algorithms", Terminal.Text);
    }

    [Fact]
    public async Task Starting_in_the_background_returns_at_once_and_keeps_the_activity_running()
    {
        await Run("study algorithms -b");

        Assert.Equal(ActivityMode.Background, Service.Current!.Mode);
        Assert.Null(Terminal.Panel);
        Assert.Contains("Studying algorithms at plain-university", Terminal.Text);
        Assert.Contains("reduced by 20%", Terminal.Text);
    }

    [Fact]
    public async Task A_named_university_is_used_and_a_wrong_course_is_explained()
    {
        await Run("study strength");
        Assert.Contains("'strength' is a gym course, not a university course", Terminal.Text);

        await Run("study free-course fancy-university -b");
        Assert.Contains("Studying free-course at fancy-university", Terminal.Text);
    }

    [Fact]
    public async Task Training_at_the_gym_works_the_same_way()
    {
        await Run("train strength -b");
        Advance(TenSeconds);

        Assert.Equal(8, State.Player.Skills.GetExperience(SkillKind.Strength), precision: 6);
        Assert.Contains("Training strength at test-gym", Terminal.Text);
    }

    [Fact]
    public async Task An_unknown_option_is_rejected_and_starts_nothing()
    {
        await Run("study algorithms -x");

        Assert.Contains("Unknown option '-x'", Terminal.Text);
        Assert.Null(Service.Current);
    }
}
