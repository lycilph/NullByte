using NullByte.Core.Configuration;
using NullByte.Core.Formulas;
using NullByte.Core.World;

namespace NullByte.Core.Tests.World;

public sealed class InitialGameStateFactoryIntegrationTests
{
    [Fact]
    public void Create_CreatesValidExpandedWorld()
    {
        // Act
        var gameState = InitialGameStateFactory.Create();

        // Assert
        Assert.NotNull(gameState);
        Assert.NotNull(gameState.Player);
        Assert.NotNull(gameState.Network);

        // M2 calls for a small network of roughly 10-15 servers.
        Assert.InRange(gameState.Network.Servers.Count, 10, 15);

        // The player must start somewhere that actually exists.
        var currentServer = gameState.Network.GetServer(gameState.Player.CurrentHostname);

        Assert.NotNull(currentServer);

        foreach (var server in gameState.Network.Servers)
        {
            Assert.False(string.IsNullOrWhiteSpace(server.Hostname));

            Assert.True(server.MaximumMoney >= 0);
            Assert.InRange(server.Money, 0, server.MaximumMoney);

            Assert.True(server.MinimumSecurity > 0);
            Assert.True(server.Security >= server.MinimumSecurity);

            Assert.True(server.RequiredHackingLevel >= 1);
            Assert.True(server.GrowthParameter > 0);
            Assert.True(server.MaximumRam >= 0);
            Assert.InRange(server.UsedRam, 0, server.MaximumRam);
        }
    }

    [Fact]
    public void Create_AllServersAreReachableFromStartingServer()
    {
        // Arrange
        var gameState = InitialGameStateFactory.Create();

        var startServerId = gameState.Player.CurrentHostname;

        // Act
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Queue<string>();

        visited.Add(startServerId);
        pending.Enqueue(startServerId);

        while (pending.Count > 0)
        {
            var serverId = pending.Dequeue();

            foreach (var neighbor in gameState.Network.GetNeighbors(serverId))
            {
                if (visited.Add(neighbor.Id))
                    pending.Enqueue(neighbor.Id);
            }
        }

        // Assert
        Assert.Equal(gameState.Network.Servers.Count, visited.Count);
    }

    [Fact]
    public void Create_ServerLevelRequirementsAreReachableThroughNormalProgression()
    {
        // Arrange
        var gameState = InitialGameStateFactory.Create();

        var requiredLevels = gameState.Network.Servers
            .Select(server => server.RequiredHackingLevel)
            .Distinct()
            .OrderBy(level => level)
            .ToArray();

        var balance = new BalanceConfig();
        var formulas = new HackingFormulas(balance);

        var server = gameState.Network.GetServer("server-n00dles");
        var experience = formulas.CalculateHackingExperience(server);

        // Act
        var hackingLevel = formulas.CalculateHackingLevel(gameState.Player.HackingExperience);
        var reachedLevels = new HashSet<int>{hackingLevel};

        const int maximumHackCount = 110_000;

        for (var hack = 0; hack < maximumHackCount; hack++)
        {
            gameState.Player.AddHackingExperience(experience);

            hackingLevel = formulas.CalculateHackingLevel(gameState.Player.HackingExperience);
            reachedLevels.Add(hackingLevel);
        }

        // Assert
        foreach (var requiredLevel in requiredLevels)
            Assert.Contains(requiredLevel, reachedLevels);
    }

    [Fact]
    public void Create_HighestServerLevelIsReachableWithinReasonableNumberOfHacks()
    {
        // Arrange
        var gameState = InitialGameStateFactory.Create();
        var balance = new BalanceConfig();
        var formulas = new HackingFormulas(balance);

        var highestRequiredLevel = gameState.Network.Servers.Max(server => server.RequiredHackingLevel);

        const int maximumReasonableHackCount = 110_000;

        var server = gameState.Network.GetServer("server-n00dles");
        var experience = formulas.CalculateHackingExperience(server);

        // Act
        var hackCount = 0;
        var hackingLevel = formulas.CalculateHackingLevel(gameState.Player.HackingExperience);

        while (hackingLevel < highestRequiredLevel &&
               hackCount < maximumReasonableHackCount)
        {
            gameState.Player.AddHackingExperience(experience);
            hackingLevel = formulas.CalculateHackingLevel(gameState.Player.HackingExperience);

            hackCount++;
        }

        // Assert
        Assert.True(
            hackingLevel >= highestRequiredLevel,
            $"The highest server requires hacking level {highestRequiredLevel}, " +
            $"but after {hackCount} successful hacks the player only reached " +
            $"level {hackingLevel}.");
    }
}