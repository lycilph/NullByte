using NullByte.Core.Configuration;
using NullByte.Core.Formulas;

namespace NullByte.Core.Tests.Formulas;

public sealed class FormulasTests
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

        var formulas = new Formulas(balance);

        Assert.Equal(42, formulas.Network.CalculateAnalyzeDuration().TotalSeconds);
        Assert.Equal(
            2.0,
            formulas.Skills.CalculateLevel(NullByte.Core.Domain.SkillKind.Hacking, 5_000)
                / (double)new SkillFormulas(balance).CalculateLevel(NullByte.Core.Domain.SkillKind.Hacking, 5_000));
        Assert.Equal(
            3.0,
            formulas.Botnet.CalculateProduction(1, 1, 1)
                / new BotnetFormulas(new BalanceConfig()).CalculateProduction(1, 1, 1),
            precision: 10);
    }

    [Fact]
    public void Creates_distinct_domain_formula_objects()
    {
        var formulas = new Formulas(new BalanceConfig());

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
