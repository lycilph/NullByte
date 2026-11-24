using NullByte.Core.Configuration;
using NullByte.Core.Formulas;

namespace NullByte.Core.Tests.Formulas;

public sealed class FormulaSetTests
{
    [Fact]
    public void Creates_all_formula_domains_from_the_same_balance_configuration()
    {
        var balance = new BalanceConfig
        {
            HackingSkillMultiplier = 2.0,
            BotnetProductionMultiplier = 3.0,
            AnalyzeDuration = 42
        };

        var formulas = new FormulaSet(balance);

        Assert.Equal(42, formulas.Network.CalculateAnalyzeDuration().TotalSeconds);
        Assert.Equal(
            new SkillFormulas(balance).CalculateExperienceForLevel(
                NullByte.Core.Domain.SkillKind.Hacking,
                10),
            formulas.Skills.CalculateExperienceForLevel(
                NullByte.Core.Domain.SkillKind.Hacking,
                10));
        Assert.Equal(
            new BotnetFormulas(balance).CalculateProduction(1, 1, 1),
            formulas.Botnet.CalculateProduction(1, 1, 1));
    }

    [Fact]
    public void Creates_distinct_domain_formula_objects()
    {
        var formulas = new FormulaSet(new BalanceConfig());

        Assert.NotSame(formulas.Skills, formulas.Hacking);
        Assert.NotSame(formulas.Skills, formulas.Activities);
        Assert.NotSame(formulas.Skills, formulas.Botnet);
        Assert.NotSame(formulas.Skills, formulas.Network);
        Assert.NotSame(formulas.Hacking, formulas.Activities);
        Assert.NotSame(formulas.Hacking, formulas.Botnet);
        Assert.NotSame(formulas.Hacking, formulas.Network);
        Assert.NotSame(formulas.Activities, formulas.Botnet);
        Assert.NotSame(formulas.Activities, formulas.Network);
        Assert.NotSame(formulas.Botnet, formulas.Network);
    }
}
