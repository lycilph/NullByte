using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using NullByte.Core.Configuration;

namespace NullByte.ConsoleUI.Configuration;

public sealed class LoadedConfiguration
{
    public required GameConfig Game { get; init; }

    public required BalanceConfig Balance { get; init; }
}

public static class ConfigurationLoader
{
    public static LoadedConfiguration Load()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        GameConfig game = configuration
            .GetSection("Game")
            .Get<GameConfig>()
            ?? throw new InvalidOperationException(
                "Game configuration is missing.");

        BalanceConfig balance = configuration
            .GetSection("Balance")
            .Get<BalanceConfig>()
            ?? throw new InvalidOperationException(
                "Balance configuration is missing.");

        Validate(game, nameof(GameConfig));
        Validate(balance, nameof(BalanceConfig));

        Validate(balance.Hacknet, nameof(HacknetBalanceConfig));
        Validate(balance.Player, nameof(PlayerBalanceConfig));

        return new LoadedConfiguration
        {
            Game = game,
            Balance = balance
        };
    }

    private static void Validate(object instance, string name)
    {
        var context = new ValidationContext(instance);

        Validator.ValidateObject(
            instance,
            context,
            validateAllProperties: true);
    }
}