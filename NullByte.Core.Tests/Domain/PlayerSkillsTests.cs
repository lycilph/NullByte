using NullByte.Core.Domain;

namespace NullByte.Core.Tests.Domain;

public sealed class PlayerSkillsTests
{
    [Fact]
    public void Every_skill_starts_at_zero()
    {
        var skills = new PlayerSkills();

        foreach (var kind in Enum.GetValues<SkillKind>())
            Assert.Equal(0, skills.GetExperience(kind));
    }

    [Fact]
    public void Experience_is_added_to_one_skill_only()
    {
        var skills = new PlayerSkills();

        skills.AddExperience(SkillKind.Charisma, 5);
        skills.AddExperience(SkillKind.Charisma, 2.5);

        Assert.Equal(7.5, skills.GetExperience(SkillKind.Charisma));
        Assert.Equal(0, skills.GetExperience(SkillKind.Hacking));
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Invalid_amounts_are_rejected_and_change_nothing(double amount)
    {
        var skills = new PlayerSkills();
        skills.AddExperience(SkillKind.Strength, 3);

        Assert.Throws<ArgumentOutOfRangeException>(() => skills.AddExperience(SkillKind.Strength, amount));
        Assert.Equal(3, skills.GetExperience(SkillKind.Strength));
    }

    [Fact]
    public void Unknown_skill_is_rejected()
    {
        var skills = new PlayerSkills();

        Assert.Throws<ArgumentOutOfRangeException>(() => skills.GetExperience((SkillKind)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => skills.AddExperience((SkillKind)99, 1));
    }

    [Fact]
    public void Player_hacking_experience_is_accessed_through_skills()
    {
        var player = new Player(ServerId.New(), startingMoney: 0, startingHackingExperience: 123);

        player.Skills.AddExperience(SkillKind.Hacking, 7);

        Assert.Equal(130, player.Skills.GetExperience(SkillKind.Hacking));
        Assert.Equal(130, player.Skills.GetExperience(SkillKind.Hacking));
    }
}