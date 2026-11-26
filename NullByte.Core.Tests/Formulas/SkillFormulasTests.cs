using NullByte.Core.Configuration;
using NullByte.Core.Domain;
using NullByte.Core.Formulas;

namespace NullByte.Core.Tests.Formulas;

public sealed class SkillFormulasTests
{
    private readonly BalanceConfig balance = new();

    [Fact]
    public void Level_is_never_below_one()
    {
        var skills = new SkillFormulas(balance);

        foreach (var kind in Enum.GetValues<SkillKind>())
            Assert.Equal(1, skills.CalculateLevel(kind, 0));
    }

    [Fact]
    public void Each_skill_uses_its_own_multiplier()
    {
        var skills = new SkillFormulas(new BalanceConfig { StrengthSkillMultiplier = 2.0, CharismaSkillMultiplier = 0.5 });

        var strength = skills.CalculateLevel(SkillKind.Strength, 5_000);
        var agility = skills.CalculateLevel(SkillKind.Agility, 5_000);
        var charisma = skills.CalculateLevel(SkillKind.Charisma, 5_000);

        Assert.True(strength > agility);
        Assert.True(charisma < agility);
    }

    [Theory]
    [InlineData(SkillKind.Hacking, 2)]
    [InlineData(SkillKind.Hacking, 100)]
    [InlineData(SkillKind.Strength, 50)]
    [InlineData(SkillKind.Charisma, 300)]
    public void Experience_for_level_reaches_that_level_and_is_close_to_the_threshold(SkillKind kind, int level)
    {
        var skills = new SkillFormulas(new BalanceConfig { StrengthSkillMultiplier = 2.0, CharismaSkillMultiplier = 0.5 });

        var experience = skills.CalculateExperienceForLevel(kind, level);

        Assert.True(skills.CalculateLevel(kind, experience) >= level);
        Assert.True(skills.CalculateLevel(kind, experience * (1 - 1e-9)) < level);
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Invalid_experience_is_rejected(double experience)
    {
        var skills = new SkillFormulas(balance);

        Assert.Throws<ArgumentOutOfRangeException>(() => skills.CalculateLevel(SkillKind.Hacking, experience));
    }

    [Fact]
    public void Level_below_one_is_rejected()
    {
        var skills = new SkillFormulas(balance);

        Assert.Throws<ArgumentOutOfRangeException>(() => skills.CalculateExperienceForLevel(SkillKind.Hacking, 0));
    }
}