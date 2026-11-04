using System.ComponentModel.DataAnnotations;
using NullByte.Core.Configuration;

namespace NullByte.Core.Tests.Configuration;

public class ConfigurationValidationTests
{
    [Fact]
    public void DefaultGameConfig_IsValid()
    {
        var config = new GameConfig();
        var context = new ValidationContext(config);

        var exception = Record.Exception(() =>
            Validator.ValidateObject(
                config,
                context,
                validateAllProperties: true));

        Assert.Null(exception);
    }

    [Fact]
    public void GameConfig_WithInvalidTimeMultiplier_Throws()
    {
        var config = new GameConfig
        {
            TimeMultiplier = -1.0
        };

        var context = new ValidationContext(config);

        Assert.Throws<ValidationException>(() =>
            Validator.ValidateObject(
                config,
                context,
                validateAllProperties: true));
    }

    [Fact]
    public void BalanceConfig_DefaultValues_AreValid()
    {
        var config = new BalanceConfig();

        Validator.ValidateObject(
            config.Hacknet,
            new ValidationContext(config.Hacknet),
            validateAllProperties: true);

        Validator.ValidateObject(
            config.Player,
            new ValidationContext(config.Player),
            validateAllProperties: true);
    }
}