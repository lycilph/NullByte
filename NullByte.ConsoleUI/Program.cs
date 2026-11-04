using Microsoft.Extensions.Logging;
using NullByte.ConsoleUI.Configuration;

namespace NullByte.ConsoleUI;

public class Program
{
    static async Task Main()
    {
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .SetMinimumLevel(LogLevel.Information)
                .AddSimpleConsole(options =>
                {
                    options.SingleLine = true;
                    options.TimestampFormat = "HH:mm:ss ";
                });
        });

        ILogger logger = loggerFactory.CreateLogger("NullByte");

        try
        {
            logger.LogInformation("Starting NullByte...");

            LoadedConfiguration configuration = ConfigurationLoader.Load();

            Console.WriteLine();
            Console.WriteLine("==================================");
            Console.WriteLine("          NULLBYTE v0.1");
            Console.WriteLine("==================================");
            Console.WriteLine();
            Console.WriteLine("Simulation configuration loaded.");
            Console.WriteLine(
                $"Time multiplier: {configuration.Game.TimeMultiplier:F2}");
            Console.WriteLine(
                $"Starting money: ${configuration.Balance.Player.StartingMoney:N2}");
            Console.WriteLine();
            Console.WriteLine("Welcome to NullByte.");
            Console.WriteLine();

            logger.LogInformation("NullByte started successfully.");

            Console.ReadKey();
        }
        catch (Exception exception)
        {
            logger.LogCritical(exception, "Failed to start NullByte.");

            Environment.ExitCode = 1;
        }
    }
}