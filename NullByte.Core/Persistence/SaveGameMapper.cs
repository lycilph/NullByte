using NullByte.Core.Domain;

namespace NullByte.Core.Persistence;

/// <summary>
/// A game rebuilt from a save, ready to hand to a new session.
/// </summary>
/// <param name="State">The restored state. Its servers start with no RAM in use.</param>
/// <param name="GameTime">Where the game clock should continue from.</param>
/// <param name="Processes">Scripts to start again once the game loop is running.</param>
public sealed record RestoredGame(GameState State, TimeSpan GameTime, IReadOnlyList<ProcessSave> Processes);

/// <summary>
/// Converts between the live domain model and <see cref="SaveGame"/>.
/// </summary>
public static class SaveGameMapper
{
    /// <summary>
    /// Copies the game into a save. Reads game state, so call it on the game thread.
    /// </summary>
    public static SaveGame Capture(
        GameState state, TimeSpan gameTime, IReadOnlyList<ProcessSave> processes, DateTimeOffset savedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(processes);

        var player = state.Player;

        // Sorted so the same game always produces the same file.
        var servers = state.Network.Servers
            .OrderBy(server => server.Hostname, StringComparer.Ordinal)
            .ToArray();

        var files = new List<FileSave>();
        foreach (var server in servers)
        {
            foreach (var name in state.FileSystem.ListFiles(server.Hostname))
            {
                if (state.FileSystem.TryReadFile(server.Hostname, name, out var content) && content is not null)
                    files.Add(new FileSave { Hostname = server.Hostname, Name = name, Content = content });
            }
        }

        return new SaveGame
        {
            Version = SaveGame.CurrentVersion,
            SavedAtUtc = savedAtUtc,
            GameTime = gameTime,
            Player = new PlayerSave
            {
                HomeHostname = player.HomeHostname,
                CurrentHostname = player.CurrentHostname,
                Money = player.Money,
                HackingExperience = player.HackingExperience,
                DiscoveredHostnames = player.DiscoveredHostnames.Order(StringComparer.Ordinal).ToArray()
            },
            Servers = servers.Select(CaptureServer).ToArray(),

            // Connections have no semantic order in the game, so serialize them
            // deterministically. This makes saves independent of construction order.
            Connections = state.Network.GetConnections()
                .Select(connection => new ConnectionSave
                {
                    From = connection.First,
                    To = connection.Second
                })
                .OrderBy(connection => connection.From, StringComparer.Ordinal)
                .ThenBy(connection => connection.To, StringComparer.Ordinal)
                .ToArray(),

            Botnet = new BotnetSave
            {
                TotalEarned = state.Botnet.TotalEarned,
                Nodes = state.Botnet.Nodes
                    .Select(node => new BotnetNodeSave { Level = node.Level, Ram = node.Ram, Cores = node.Cores })
                    .ToArray()
            },
            Files = files,
            Processes = processes.ToArray()          // was: sorted by Pid
        };
    }

    /// <summary>
    /// Rebuilds a game from a save, validating as it goes. Throws
    /// <see cref="SaveFormatException"/> if the data is not something the game would accept,
    /// so a bad save can be refused before the running game is touched.
    /// </summary>
    public static RestoredGame Restore(SaveGame save)
    {
        ArgumentNullException.ThrowIfNull(save);

        try
        {
            return RestoreCore(save);
        }
        catch (Exception exception) when (exception is ArgumentException
                                              or InvalidOperationException
                                              or KeyNotFoundException
                                              or InvalidDataException)
        {
            throw new SaveFormatException($"The save contains invalid data: {exception.Message}", exception);
        }
    }

    private static ServerSave CaptureServer(Server server) => new()
    {
        Hostname = server.Hostname,
        MaximumMoney = server.MaximumMoney,

        // Clamp: repeated floating-point adds/subtracts can leave these a few ulps outside the range
        // the Server constructor accepts, which would make a legitimate save unloadable.
        Money = Math.Clamp(server.Money, 0, server.MaximumMoney),
        Security = Math.Max(server.Security, server.MinimumSecurity),

        BaseDifficulty = server.BaseDifficulty,
        MinimumSecurity = server.MinimumSecurity,
        RequiredHackingLevel = server.RequiredHackingLevel,
        GrowthParameter = server.GrowthParameter,
        MaximumRam = server.MaximumRam,
        HasRootAccess = server.HasRootAccess
    };

    private static RestoredGame RestoreCore(SaveGame save)
    {
        if (save.GameTime < TimeSpan.Zero)
            throw new InvalidDataException("The game time is negative.");

        var network = new Network();

        foreach (var server in save.Servers)
        {
            network.AddServer(new Server(
                hostname: server.Hostname,
                maximumMoney: server.MaximumMoney,
                startingMoney: server.Money,
                baseDifficulty: server.BaseDifficulty,
                minimumSecurity: server.MinimumSecurity,
                startingSecurity: server.Security,
                requiredHackingLevel: server.RequiredHackingLevel,
                growthParameter: server.GrowthParameter,
                maximumRam: server.MaximumRam,
                hasRootAccess: server.HasRootAccess));
        }

        foreach (var connection in save.Connections)
            network.Connect(connection.From, connection.To);

        var savedPlayer = save.Player;
        var player = new Player(savedPlayer.HomeHostname, savedPlayer.Money, savedPlayer.HackingExperience)
        {
            CurrentHostname = savedPlayer.CurrentHostname
        };

        foreach (var hostname in savedPlayer.DiscoveredHostnames)
        {
            RequireServer(network, hostname, "A discovered server");
            player.DiscoverServer(hostname);
        }

        // The GameState constructor checks that the home and current servers exist.
        var state = new GameState(player, network);

        state.Botnet.Restore(
            save.Botnet.Nodes.Select(node => new BotnetNode(node.Level, node.Ram, node.Cores)),
            save.Botnet.TotalEarned);

        foreach (var file in save.Files)
            state.FileSystem.WriteFile(file.Hostname, file.Name, file.Content);

        foreach (var process in save.Processes)
        {
            RequireServer(network, process.Hostname, $"The host of '{process.Filename}'");

            if (process.Threads < 1)
                throw new InvalidDataException($"'{process.Filename}' has fewer than one thread.");
        }

        return new RestoredGame(state, save.GameTime, save.Processes.ToArray());
    }

    private static void RequireServer(Network network, string hostname, string what)
    {
        if (!network.TryGetServer(hostname, out _))
            throw new InvalidDataException($"{what} ('{hostname}') does not exist in the network.");
    }
}