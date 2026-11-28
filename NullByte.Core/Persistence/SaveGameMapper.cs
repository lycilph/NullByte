using NullByte.Core.Domain;
using NullByte.Core.Domain.Activities;
using NullByte.Core.World;

namespace NullByte.Core.Persistence;

/// <summary>
/// A game rebuilt from a save, ready to hand to a new session.
/// </summary>
/// <param name="State">The restored state. Its servers start with no RAM in use.</param>
/// <param name="GameTime">Where the game clock should continue from.</param>
/// <param name="Processes">Scripts to start again once the game loop is running.</param>
public sealed record RestoredGame(GameState State, TimeSpan GameTime, IReadOnlyList<ProcessSave> Processes)
{
    /// <summary>
    /// Things in the save that no longer exist in the game's data (a renamed course, say), and so were left out.
    /// The save is still good: this is for telling the player.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// The activity catalog that the restored activity was built from. A session started from this game should use the same one.
    /// </summary>
    public ActivityCatalog? ActivityCatalog { get; init; }
}

/// <summary>
/// Converts between the live domain model and <see cref="SaveGame"/>.
/// </summary>
public static class SaveGameMapper
{
    /// <summary>
    /// Copies the game into a save. Reads game state, so call it on the game thread.
    /// </summary>
    public static SaveGame Capture(GameState state, TimeSpan gameTime, IReadOnlyList<ProcessSave> processes, DateTimeOffset savedAtUtc)
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
                if (state.FileSystem.TryReadFile(server.Hostname, name, out var content) && content is not null &&
                    state.FileSystem.TryGetFileTimes(server.Hostname, name, out var times))
                    files.Add(new FileSave { ServerId = server.Id.ToString(), Name = name, Content = content, Created = times.Created, Modified = times.Modified });
            }
        }

        var directories = new List<DirectorySave>();
        foreach (var server in servers)
        {
            foreach (var path in state.FileSystem.ListDirectories(server.Hostname))
                directories.Add(new DirectorySave { ServerId = server.Id.ToString(), Path = path });
        }

        return new SaveGame
        {
            Version = SaveGame.CurrentVersion,
            SavedAtUtc = savedAtUtc,
            GameTime = gameTime,
            Player = new PlayerSave
            {
                HomeServerId = player.HomeServerId.ToString(),
                CurrentServerId = player.CurrentServerId.ToString(),
                Money = player.Money,
                Skills = CaptureSkills(player.Skills),
                DiscoveredServerIds = player.DiscoveredServerIds
                    .Select(id => state.Network.GetServer(id))
                    .OrderBy(server => server.Hostname, StringComparer.Ordinal)
                    .Select(server => server.Id.ToString())
                    .ToArray()
            },
            Servers = servers.Select(CaptureServer).ToArray(),

            // Connections have no semantic order in the game, so serialize them
            // deterministically. This makes saves independent of construction order.
            Connections = state.Network.GetConnections()
                .Select(connection => new ConnectionSave
                {
                    FromServerId = state.Network.GetServer(connection.First).Id.ToString(),
                    ToServerId = state.Network.GetServer(connection.Second).Id.ToString()
                })
                .OrderBy(connection => state.Network.GetServer(ServerId.Parse(connection.FromServerId)).Hostname, StringComparer.Ordinal)
                .ThenBy(connection => state.Network.GetServer(ServerId.Parse(connection.ToServerId)).Hostname, StringComparer.Ordinal)
                .ToArray(),

            Botnet = new BotnetSave
            {
                TotalEarned = state.Botnet.TotalEarned,
                Nodes = state.Botnet.Nodes
                    .Select(node => new BotnetNodeSave { Level = node.Level, Ram = node.Ram, Cores = node.Cores })
                    .ToArray()
            },
            Employment = CaptureEmployment(state.Employment),
            Activity = CaptureActivity(state.Activity.Current),
            Files = files,
            Directories = directories,
            Processes = processes.ToArray(),
            Narrative = new NarrativeSave
            {
                Mode = state.Narrative.Mode,
                UnlockedCapabilities = state.Narrative.UnlockedCapabilities.Order(StringComparer.Ordinal).ToArray(),
                CompletedMilestones = state.Narrative.CompletedMilestones.Order(StringComparer.Ordinal).ToArray(),
                DeliveredMessages = state.Narrative.DeliveredMessages.Order(StringComparer.Ordinal).ToArray(),
                DiscoveredDiscoveries = state.Narrative.DiscoveredDiscoveries.Order(StringComparer.Ordinal).ToArray()
            }
        };
    }

    // Every skill is written, so the file shows all of them and is the same shape every time.
    private static Dictionary<string, double> CaptureSkills(PlayerSkills skills)
        => Enum.GetValues<SkillKind>().ToDictionary(SkillName, skills.GetExperience);

    private static Dictionary<string, double> CaptureExperience(IReadOnlyDictionary<SkillKind, double> experience)
        => experience.OrderBy(entry => entry.Key).ToDictionary(entry => SkillName(entry.Key), entry => entry.Value);

    private static string SkillName(SkillKind kind) => kind.ToString().ToLowerInvariant();

    private static EmploymentSave CaptureEmployment(Employment employment) => new()
    {
        Jobs = employment.Jobs
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => new JobSave { Company = entry.Key, Career = entry.Value.Career, RankIndex = entry.Value.RankIndex })
            .ToArray(),
        Reputation = employment.Reputation
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => new ReputationSave { Company = entry.Key, Amount = entry.Value })
            .ToArray()
    };

    private static ActivitySave? CaptureActivity(Activity? activity)
    {
        if (activity is null)
            return null;

        var progress = activity.Progress;

        var saved = new ActivityProgressSave
        {
            Elapsed = progress.Elapsed,
            NetMoney = progress.NetMoney,
            Experience = CaptureExperience(progress.ExperienceGained),
            ReputationGained = progress.ReputationGained,
            Attempts = progress.Attempts,
            Successes = progress.Successes
        };

        return activity switch
        {
            ClassActivity classActivity => new ActivitySave
            {
                Progress = saved,
                Class = new ClassActivitySave { Course = classActivity.Course.Name, Facility = classActivity.Facility.Name }
            },
            JobActivity jobActivity => new ActivitySave
            {
                Progress = saved,
                Job = new JobActivitySave { Company = jobActivity.Company.Name, Career = jobActivity.Career.Name }
            },
            CrimeActivity crimeActivity => new ActivitySave
            {
                Progress = saved,
                Crime = new CrimeActivitySave
                {
                    Crime = crimeActivity.Crime.Name,
                    Repeat = crimeActivity.Repeat,
                    CycleSeconds = crimeActivity.CycleSeconds
                }
            },
            _ => throw new NotSupportedException($"An activity of type {activity.GetType().Name} cannot be saved.")
        };
    }

    private static ServerSave CaptureServer(Server server) => new()
    {
        Id = server.Id.ToString(),
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
        HasRootAccess = server.HasRootAccess,
        HasBackdoor = server.HasBackdoor,
        IsCloudServer = server.IsCloudServer
    };

    /// <param name="save">The save to restore.</param>
    /// <param name="catalog">
    /// The activity catalog that the names in the save are looked up in. If omitted, the one that ships
    /// with the game. Names that are not in it are left out and reported in <see cref="RestoredGame.Warnings"/>.
    /// </param>
    public static RestoredGame Restore(SaveGame save, ActivityCatalog? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(save);

        try
        {
            return RestoreCore(save, catalog ?? ActivityCatalogFactory.CreateDefault());
        }
        catch (Exception exception) when (exception is ArgumentException
                                              or FormatException
                                              or InvalidOperationException
                                              or KeyNotFoundException
                                              or InvalidDataException)
        {
            throw new SaveFormatException($"The save contains invalid data: {exception.Message}", exception);
        }
    }

    private static RestoredGame RestoreCore(SaveGame save, ActivityCatalog catalog)
    {
        if (save.GameTime < TimeSpan.Zero)
            throw new InvalidDataException("The game time is negative.");

        var network = new Network();

        foreach (var server in save.Servers)
        {
            var serverId = ServerId.Parse(server.Id);
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
                hasRootAccess: server.HasRootAccess,
                id: serverId,
                hasBackdoor: server.HasBackdoor,
                isCloudServer: server.IsCloudServer));
        }

        foreach (var connection in save.Connections)
            network.Connect(ServerId.Parse(connection.FromServerId), ServerId.Parse(connection.ToServerId));

        var savedPlayer = save.Player;
        var homeServer = network.GetServer(ServerId.Parse(savedPlayer.HomeServerId));
        var currentServer = network.GetServer(ServerId.Parse(savedPlayer.CurrentServerId));
        var player = new Player(homeServer.Id, savedPlayer.Money)
        {
            CurrentServerId = currentServer.Id
        };

        RestoreSkills(player.Skills, savedPlayer.Skills, "The player");

        var state = new GameState(player, network);
        RestoreNarrative(save.Narrative, state.Narrative);

        foreach (var id in savedPlayer.DiscoveredServerIds)
        {
            var serverId = ServerId.Parse(id);
            RequireServer(network, serverId, "A discovered server");
            player.DiscoverServer(serverId);
        }

        state.Botnet.Restore(
            save.Botnet.Nodes.Select(node => new BotnetNode(node.Level, node.Ram, node.Cores)),
            save.Botnet.TotalEarned);

        foreach (var directory in save.Directories)
        {
            var server = RequireServer(network, ServerId.Parse(directory.ServerId), "A directory server");
            state.FileSystem.RestoreDirectory(server.Hostname, directory.Path);
        }

        foreach (var file in save.Files)
        {
            var server = RequireServer(network, ServerId.Parse(file.ServerId), "A file server");
            state.FileSystem.RestoreFile(server.Hostname, file.Name, file.Content, file.Created, file.Modified);
        }

        foreach (var process in save.Processes)
        {
            RequireServer(network, ServerId.Parse(process.ServerId), $"The host of '{process.Filename}'");

            if (process.Threads < 1)
                throw new InvalidDataException($"'{process.Filename}' has fewer than one thread.");
        }

        var warnings = new List<string>();

        RestoreEmployment(save.Employment, state.Employment, catalog, warnings);
        state.Activity.Current = RestoreActivity(save, state, catalog, warnings);

        return new RestoredGame(state, save.GameTime, save.Processes.ToArray())
        {
            Warnings = warnings,
            ActivityCatalog = catalog
        };
    }

    private static void RestoreNarrative(NarrativeSave saved, NarrativeState narrative)
    {
        ArgumentNullException.ThrowIfNull(saved);
        ArgumentNullException.ThrowIfNull(narrative);

        narrative.Mode = saved.Mode;
        foreach (var capability in saved.UnlockedCapabilities)
        {
            if (string.IsNullOrWhiteSpace(capability))
                throw new InvalidDataException("A narrative capability ID is empty.");

            narrative.UnlockCapability(capability);
        }

        foreach (var milestone in saved.CompletedMilestones)
        {
            if (string.IsNullOrWhiteSpace(milestone))
                throw new InvalidDataException("A narrative milestone ID is empty.");

            narrative.CompleteMilestone(milestone);
        }

        foreach (var message in saved.DeliveredMessages)
        {
            if (string.IsNullOrWhiteSpace(message))
                throw new InvalidDataException("A narrative message ID is empty.");

            narrative.DeliverMessage(message);
        }

        foreach (var discovery in saved.DiscoveredDiscoveries)
        {
            if (string.IsNullOrWhiteSpace(discovery))
                throw new InvalidDataException("A narrative discovery ID is empty.");

            narrative.DiscoverDiscovery(discovery);
        }
    }

    private static void RestoreSkills(PlayerSkills skills, IReadOnlyDictionary<string, double> saved, string who)
    {
        var seen = new HashSet<SkillKind>();

        foreach (var (name, amount) in saved)
        {
            var kind = ParseSkill(name, who);

            if (!seen.Add(kind))
                throw new InvalidDataException($"{who} has experience in '{name}' more than once.");

            skills.AddExperience(kind, amount);
        }
    }

    private static SkillKind ParseSkill(string name, string who)
    {
        foreach (var kind in Enum.GetValues<SkillKind>())
        {
            if (string.Equals(SkillName(kind), name, StringComparison.OrdinalIgnoreCase))
                return kind;
        }

        throw new InvalidDataException($"{who} has experience in an unknown skill '{name}'.");
    }

    private static void RestoreEmployment(EmploymentSave saved, Employment employment, ActivityCatalog catalog, List<string> warnings)
    {
        var reputed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in saved.Reputation)
        {
            if (string.IsNullOrWhiteSpace(entry.Company))
                throw new InvalidDataException("A reputation entry has no company.");

            if (!reputed.Add(entry.Company))
                throw new InvalidDataException($"The reputation with '{entry.Company}' is listed more than once.");

            // Kept even for a company the game no longer has: it is the player's, and harmless.
            employment.AddReputation(entry.Company, entry.Amount);
        }

        var employed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var job in saved.Jobs)
        {
            if (string.IsNullOrWhiteSpace(job.Company))
                throw new InvalidDataException("A job has no company.");

            if (!employed.Add(job.Company))
                throw new InvalidDataException($"The player holds more than one job at '{job.Company}'.");

            if (job.RankIndex < 0)
                throw new InvalidDataException($"The job at '{job.Company}' has a negative rank.");

            // Not finding it is not corruption: the game's data may have changed since the save was made.
            // The player is hired again at the highest rank they qualify for the next time they work there.
            if (!catalog.TryGetCompany(job.Company, out var company))
            {
                warnings.Add($"The company '{job.Company}' no longer exists, so your job there was left out.");
                continue;
            }

            var career = company.Careers.FirstOrDefault(c => string.Equals(c.Name, job.Career, StringComparison.OrdinalIgnoreCase));

            if (career is null || job.RankIndex >= career.Ranks.Count)
            {
                warnings.Add($"The career '{job.Career}' at '{company.Name}' no longer has that position, so your job there was left out.");
                continue;
            }

            employment.SetJob(company.Name, new Job(career.Name, job.RankIndex));
        }
    }

    private static Activity? RestoreActivity(SaveGame save, GameState state, ActivityCatalog catalog, List<string> warnings)
    {
        var saved = save.Activity;

        if (saved is null)
            return null;

        var kinds = (saved.Class is null ? 0 : 1) + (saved.Job is null ? 0 : 1) + (saved.Crime is null ? 0 : 1);

        if (kinds != 1)
            throw new InvalidDataException("The saved activity must be exactly one of a class, a job or a crime.");

        var progress = RestoreProgress(saved.Progress);

        // Whatever the player was doing carries on in the background: being focused is not something a save holds on to.
        Activity? activity;

        if (saved.Class is { } classSave)
            activity = RestoreClass(classSave, catalog, warnings);
        else if (saved.Job is { } jobSave)
            activity = RestoreJob(jobSave, save, state, catalog, warnings);
        else
            activity = RestoreCrime(saved.Crime!, catalog, warnings);

        activity?.RestoreProgress(progress);

        return activity;
    }

    private static Activity? RestoreClass(ClassActivitySave saved, ActivityCatalog catalog, List<string> warnings)
    {
        if (!catalog.TryGetCourse(saved.Course, out var course))
            return NotResumed($"The course '{saved.Course}' no longer exists", warnings);

        if (!catalog.TryGetFacility(saved.Facility, out var facility))
            return NotResumed($"The place '{saved.Facility}' no longer exists", warnings);

        return new ClassActivity(course, facility, ActivityMode.Background);
    }

    private static Activity? RestoreJob(JobActivitySave saved, SaveGame save, GameState state, ActivityCatalog catalog, List<string> warnings)
    {
        // The rank comes from the saved jobs, so the save has to hold the job that the activity is.
        var heldInSave = save.Employment.Jobs.Any(job =>
            string.Equals(job.Company, saved.Company, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(job.Career, saved.Career, StringComparison.OrdinalIgnoreCase));

        if (!heldInSave)
            throw new InvalidDataException($"The player was working at '{saved.Company}' ({saved.Career}) but holds no such job.");

        if (!catalog.TryGetCompany(saved.Company, out var company))
            return NotResumed($"The company '{saved.Company}' no longer exists", warnings);

        var career = company.Careers.FirstOrDefault(c => string.Equals(c.Name, saved.Career, StringComparison.OrdinalIgnoreCase));
        var held = state.Employment.GetJob(company.Name);

        if (career is null || held is null || !string.Equals(held.Career, career.Name, StringComparison.OrdinalIgnoreCase))
            return NotResumed($"Your job at '{saved.Company}' ({saved.Career}) no longer exists", warnings);

        return new JobActivity(company, career, held.RankIndex, ActivityMode.Background);
    }

    private static Activity? RestoreCrime(CrimeActivitySave saved, ActivityCatalog catalog, List<string> warnings)
    {
        if (!double.IsFinite(saved.CycleSeconds) || saved.CycleSeconds < 0)
            throw new InvalidDataException("The time into the saved crime attempt is not valid.");

        if (!catalog.TryGetCrime(saved.Crime, out var crime))
            return NotResumed($"The crime '{saved.Crime}' no longer exists", warnings);

        var activity = new CrimeActivity(crime, saved.Repeat, ActivityMode.Background);

        // If the attempt has got shorter since, the next update simply finishes it.
        activity.SetCycle(saved.CycleSeconds);

        return activity;
    }

    // Records why the activity was left out. Callers return null.
    private static Activity? NotResumed(string why, List<string> warnings)
    {
        warnings.Add($"{why}, so what you were doing was not resumed.");
        return null;
    }

    private static ActivityProgress RestoreProgress(ActivityProgressSave saved)
    {
        if (saved.Elapsed < TimeSpan.Zero)
            throw new InvalidDataException("The saved activity has run for a negative time.");

        if (!double.IsFinite(saved.NetMoney))
            throw new InvalidDataException("The money the saved activity earned is not valid.");

        if (!double.IsFinite(saved.ReputationGained) || saved.ReputationGained < 0)
            throw new InvalidDataException("The reputation the saved activity earned is not valid.");

        if (saved.Attempts < 0 || saved.Successes < 0 || saved.Successes > saved.Attempts)
            throw new InvalidDataException("The attempts of the saved activity are not valid.");

        var experience = new Dictionary<SkillKind, double>();

        foreach (var (name, amount) in saved.Experience)
        {
            var kind = ParseSkill(name, "The saved activity");

            if (!double.IsFinite(amount) || amount < 0)
                throw new InvalidDataException($"The '{name}' experience of the saved activity is not valid.");

            if (!experience.TryAdd(kind, amount))
                throw new InvalidDataException($"The saved activity lists '{name}' experience more than once.");
        }

        return new ActivityProgress(saved.Elapsed, saved.NetMoney, experience, saved.ReputationGained, saved.Attempts, saved.Successes);
    }

    private static Server RequireServer(Network network, ServerId serverId, string what)
    {
        if (!network.TryGetServer(serverId, out var server))
            throw new InvalidDataException($"{what} ('{serverId}') does not exist in the network.");

        return server;
    }
}