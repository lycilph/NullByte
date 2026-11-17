Here are all 24 files, grouped by layer. New files are shown in full. For files that already existed, I show only the changed hunks and say where they sit.

# 1. NullByte.Core

## ~~Changed: `Domain/Network.cs` (3 small edits)~~ 
Not doing this - instead the the GetNeighbors are now sorted

**Why:** `scan` and `nb.Scan` return neighbors in insertion order. Without a record of the order edges were made, a loaded game could reorder them.

```csharp
// (a) new field, right under the existing `connections` dictionary
    // The same connections as pairs, in the order they were made.
    private readonly List<(string First, string Second)> connectionOrder = [];

// (b) new property, right under `Servers`
    public IReadOnlyList<(string First, string Second)> Connections => connectionOrder;

// (c) end of Connect(): the last two lines changed
        var isNew = connections[first.Hostname].Add(second.Hostname);   // was: connections[...].Add(...)
        connections[second.Hostname].Add(first.Hostname);

        if (isNew)
            connectionOrder.Add((first.Hostname, second.Hostname));
```

## ~~Changed: `Domain/Botnet.cs` (one new method, above `AddNode`)~~

**Why:** `GameState` creates its own `Botnet`, so a load has to fill it in place. `TotalEarned` has a private setter.

```csharp
internal void Restore(IEnumerable<BotnetNode> restoredNodes, double totalEarned)
{
    ArgumentNullException.ThrowIfNull(restoredNodes);

    if (nodes.Count != 0)
        throw new InvalidOperationException("Only an empty botnet can be restored.");
    if (!double.IsFinite(totalEarned) || totalEarned < 0)
        throw new ArgumentOutOfRangeException(nameof(totalEarned), "Total earned must be a finite, non-negative value.");

    nodes.AddRange(restoredNodes);
    TotalEarned = totalEarned;
}
```

## ~~Changed: `Time/RealGameClock.cs` (constructors, after `speedMultiplier`)~~

**Why:** the clock always started at zero. A loaded game must continue from its saved game time. `GameLoop` reads `clock.Elapsed` when it is constructed, so the time has to be set at construction. That also means no catch-up payout from the botnet.

```csharp
public RealGameClock() : this(TimeSpan.Zero) { }

public RealGameClock(TimeSpan initialGameTime)
{
    if (initialGameTime < TimeSpan.Zero)
        throw new ArgumentOutOfRangeException(nameof(initialGameTime), "Initial game time cannot be negative.");

    accumulatedGameTime = initialGameTime;
}
```

## New: `Persistence/SaveGame.cs`

**What:** the file format as plain records. It is kept separate from the domain, so refactoring `Server` can't silently change what's on disk. Servers carry both fixed properties and current state, which makes the save self-contained. `UsedRam` is deliberately absent, because restarting the processes re-derives it.

```csharp
namespace NullByte.Core.Persistence;

public sealed record SaveGame
{
    /// <summary>The format version this build writes, and the newest one it can read.</summary>
    public const int CurrentVersion = 1;

    public required int Version { get; init; }
    public required DateTimeOffset SavedAtUtc { get; init; }
    public required TimeSpan GameTime { get; init; }
    public required PlayerSave Player { get; init; }
    public required IReadOnlyList<ServerSave> Servers { get; init; }
    public required IReadOnlyList<ConnectionSave> Connections { get; init; }
    public required BotnetSave Botnet { get; init; }
    public required IReadOnlyList<FileSave> Files { get; init; }
    public required IReadOnlyList<ProcessSave> Processes { get; init; }
}

public sealed record PlayerSave
{
    public required string HomeHostname { get; init; }
    public required string CurrentHostname { get; init; }
    public required double Money { get; init; }
    public required double HackingExperience { get; init; }
    public required IReadOnlyList<string> DiscoveredHostnames { get; init; }
}

public sealed record ServerSave
{
    public required string Hostname { get; init; }
    public required double MaximumMoney { get; init; }
    public required double Money { get; init; }
    public required double BaseDifficulty { get; init; }
    public required double MinimumSecurity { get; init; }
    public required double Security { get; init; }
    public required int RequiredHackingLevel { get; init; }
    public required double GrowthParameter { get; init; }
    public required double MaximumRam { get; init; }
    public required bool HasRootAccess { get; init; }
}

public sealed record ConnectionSave
{
    public required string From { get; init; }
    public required string To { get; init; }
}

public sealed record BotnetSave
{
    public required double TotalEarned { get; init; }
    public required IReadOnlyList<BotnetNodeSave> Nodes { get; init; }
}

public sealed record BotnetNodeSave
{
    public required int Level { get; init; }
    public required int Ram { get; init; }
    public required int Cores { get; init; }
}

public sealed record FileSave
{
    public required string Hostname { get; init; }
    public required string Name { get; init; }
    public required string Content { get; init; }
}

public sealed record ProcessSave
{
    public required int Pid { get; init; }
    public required string Filename { get; init; }
    public required string Hostname { get; init; }
    public required IReadOnlyList<string> Args { get; init; }
    public required int Threads { get; init; }
}
```
(The doc comments from the real file are trimmed here to save space.)

## New: `Persistence/SaveFormatException.cs`

**What:** the one exception type for "this save can't be used". Its messages are written for the player. `GameHost` catches it to keep the current game.

```csharp
namespace NullByte.Core.Persistence;

public sealed class SaveFormatException : Exception
{
    public SaveFormatException(string message) : base(message) { }
    public SaveFormatException(string message, Exception innerException) : base(message, innerException) { }
}
```

## New: `Persistence/SaveGameMapper.cs`

**What:** converts between the live domain and `SaveGame`.
- `Capture` sorts servers, files and PIDs so the same game always gives the same file.
- `Capture` clamps server money and security. Repeated floating-point arithmetic can leave them about 1e-15 outside what the `Server` constructor accepts. I measured this: with a fractional minimum security of 7.54, roughly 30% of fully weakened servers end up just below it.
- `Restore` validates everything, and wraps any failure as a `SaveFormatException`. It runs before the old game is stopped.
- It builds objects only through existing public or internal APIs (`Server` constructor, `Network.Connect`, `Player.CurrentHostname`, `VirtualFileSystem.WriteFile`).

```csharp
using NullByte.Core.Domain;

namespace NullByte.Core.Persistence;

public sealed record RestoredGame(GameState State, TimeSpan GameTime, IReadOnlyList<ProcessSave> Processes);

public static class SaveGameMapper
{
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

            // Neighbor order depends on the order connections were made, so keep it.
            Connections = state.Network.Connections
                .Select(connection => new ConnectionSave { From = connection.First, To = connection.Second })
                .ToArray(),

            Botnet = new BotnetSave
            {
                TotalEarned = state.Botnet.TotalEarned,
                Nodes = state.Botnet.Nodes
                    .Select(node => new BotnetNodeSave { Level = node.Level, Ram = node.Ram, Cores = node.Cores })
                    .ToArray()
            },
            Files = files,
            Processes = processes.OrderBy(process => process.Pid).ToArray()
        };
    }

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

        var pids = new HashSet<int>();
        foreach (var process in save.Processes)
        {
            RequireServer(network, process.Hostname, $"The host of process {process.Pid}");

            if (process.Pid < 1)
                throw new InvalidDataException($"Process id {process.Pid} is not valid.");
            if (process.Threads < 1)
                throw new InvalidDataException($"Process {process.Pid} has fewer than one thread.");
            if (!pids.Add(process.Pid))
                throw new InvalidDataException($"Process id {process.Pid} appears more than once.");
        }

        return new RestoredGame(state, save.GameTime, save.Processes.OrderBy(process => process.Pid).ToArray());
    }

    private static void RequireServer(Network network, string hostname, string what)
    {
        if (!network.TryGetServer(hostname, out _))
            throw new InvalidDataException($"{what} ('{hostname}') does not exist in the network.");
    }
}
```

## New: `Persistence/SaveMigration.cs`

**What:** the "migration hook" the design doc asks for. A migration upgrades the raw JSON by exactly one version. The migrator chains them from the save's version up to `CurrentVersion`. It refuses saves from a newer build, and it errors rather than guessing when a migration is missing. There are no migrations yet, because v1 is the first format.

```csharp
using System.Text.Json.Nodes;

namespace NullByte.Core.Persistence;

public interface ISaveMigration
{
    /// <summary>The version this migration reads. It produces <c>FromVersion + 1</c>.</summary>
    int FromVersion { get; }

    /// <summary>Rewrites the save in place. The migrator updates the version number.</summary>
    void Migrate(JsonObject save);
}

public sealed class SaveMigrator
{
    private readonly Dictionary<int, ISaveMigration> migrations;
    private readonly int currentVersion;

    public SaveMigrator(IEnumerable<ISaveMigration>? migrations = null, int currentVersion = SaveGame.CurrentVersion)
    {
        this.currentVersion = currentVersion;
        this.migrations = (migrations ?? []).ToDictionary(migration => migration.FromVersion);
    }

    public void Upgrade(JsonObject save)
    {
        ArgumentNullException.ThrowIfNull(save);

        var version = ReadVersion(save);

        if (version > currentVersion)
        {
            throw new SaveFormatException(
                $"This save was written by a newer version of NullByte (save format {version}; " +
                $"this version reads up to {currentVersion}).");
        }

        while (version < currentVersion)
        {
            if (!migrations.TryGetValue(version, out var migration))
                throw new SaveFormatException($"This save is too old to load (no way to upgrade save format {version}).");

            migration.Migrate(save);

            version++;
            save["version"] = version;
        }
    }

    private static int ReadVersion(JsonObject save)
    {
        try
        {
            var version = save["version"]?.GetValue<int>();

            if (version is >= 1)
                return version.Value;
        }
        catch (Exception exception) when (exception is FormatException or InvalidOperationException)
        {
            // Falls through to the error below.
        }

        throw new SaveFormatException("The save has no valid version number.");
    }
}
```
(The `currentVersion` parameter exists so tests can simulate a future format.)

## New: `Persistence/SaveGameSerializer.cs`

**What:** JSON in and out, and the point where migrations run (`Parse` → `Upgrade` → typed deserialize). Three options matter:
- `NewLine = "\n"` gives identical bytes on every OS, so the golden test doesn't depend on the platform.
- `RespectNullableAnnotations` makes a `null` where none is allowed a damaged save, not a later `NullReferenceException`.
- `UnsafeRelaxedJsonEscaping` keeps player scripts readable. The default turned every quote into `\u0022`. This is a local file, not HTML, so it's safe.

```csharp
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NullByte.Core.Persistence;

public sealed class SaveGameSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,

        // Player scripts are full of quotes and non-ASCII text. This is a local file, not HTML, so keep
        // them readable (and diffable) instead of turning every quote into \u0022.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,

        // The same bytes on every platform, so the golden-file test does not depend on the OS.
        NewLine = "\n",

        // A "null" for a property that is not nullable is a damaged save, not a missing value.
        RespectNullableAnnotations = true
    };

    private static readonly JsonNodeOptions NodeOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly SaveMigrator migrator;

    public SaveGameSerializer(SaveMigrator? migrator = null)
    {
        this.migrator = migrator ?? new SaveMigrator();
    }

    public string Serialize(SaveGame save)
    {
        ArgumentNullException.ThrowIfNull(save);
        return JsonSerializer.Serialize(save, Options);
    }

    public SaveGame Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        try
        {
            if (JsonNode.Parse(json, NodeOptions) is not JsonObject root)
                throw new SaveFormatException("The save is not a JSON object.");

            migrator.Upgrade(root);

            return root.Deserialize<SaveGame>(Options)
                ?? throw new SaveFormatException("The save is empty.");
        }
        catch (JsonException exception)
        {
            throw new SaveFormatException($"The save is damaged: {exception.Message}", exception);
        }
    }
}
```

# 2. NullByte.Scripting

## Changed: `Runtime/ScriptProcessManager.cs` (3 places)

**Why:** a restored script must get its old PID back. Your own comment in `AllocatePid` said this would be needed ("when persistence/save-game concerns arrive").

```csharp
// (a) Create(): new optional parameter, and the PID logic at the top of the body
public ScriptProcess Create(string filename, string hostname, IReadOnlyList<string> args,
                            int threads, double ramCost, int? pid = null)
{
    // ...existing argument checks unchanged...

    int assignedPid;                                   // replaces: var pid = AllocatePid();
    if (pid is { } requestedPid)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(requestedPid, 1);

        if (processes.ContainsKey(requestedPid))
            throw new InvalidOperationException($"Process id {requestedPid} is already in use.");

        assignedPid = requestedPid;
        AdvancePidCounter(requestedPid + 1);
    }
    else
    {
        assignedPid = AllocatePid();
    }

    var process = new ScriptProcess(assignedPid, filename, /* …rest unchanged… */);
    // ...
    processes.Add(assignedPid, process);               // was: processes.Add(pid, process);
    return process;
}

// (b) new method, above AllocatePid()
public void AdvancePidCounter(int minimum)
{
    if (minimum > nextPid)
        nextPid = minimum;
}

// (c) AllocatePid(): only the comment changed (the "we will make this more robust later" note
//     now explains that restored PIDs are brought back via AdvancePidCounter)
```
(`AdvancePidCounter` stops a restored script from handing a PID to a child process that belongs to a process still waiting to be restored.)

## Changed: `Runtime/ScriptService.cs`

**Why:** capture the running processes, and restart them under their original PIDs.

```csharp
// (a) new using at the top
using NullByte.Core.Persistence;

// (b) Start() is now a thin wrapper. Its old body became StartCore, which takes a pid.
public Task<ScriptStartResult> Start(string hostname, string filename, IReadOnlyList<string> args, int threads = 1)
    => StartCore(hostname, filename, args, threads, pid: null);

// (c) new: which processes to save (game thread only)
public IReadOnlyList<ProcessSave> CaptureRunningProcesses()
    => processManager.Processes
        .Where(process => process.State == ScriptProcessState.Running)
        .Select(process => new ProcessSave
        {
            Pid = process.Pid,
            Filename = process.Filename,
            Hostname = process.Hostname,
            Args = process.Args.ToArray(),
            Threads = process.Threads
        })
        .ToArray();

// (d) new: start them again, in PID order; failures are returned, not thrown
public async Task<IReadOnlyList<ScriptRestoreResult>> RestoreProcessesAsync(IEnumerable<ProcessSave> saved)
{
    ArgumentNullException.ThrowIfNull(saved);

    var ordered = saved.OrderBy(process => process.Pid).ToList();
    if (ordered.Count == 0)
        return [];

    // A restored script may start others before this loop reaches them. Keep their PIDs
    // out of the range that is still waiting to be restored.
    processManager.AdvancePidCounter(ordered[^1].Pid + 1);

    var results = new List<ScriptRestoreResult>();
    foreach (var process in ordered)
    {
        var result = await StartCore(process.Hostname, process.Filename, process.Args, process.Threads, process.Pid);
        results.Add(new ScriptRestoreResult(process, result));
    }

    return results;
}

// (e) the old Start body, renamed, with `int? pid` added...
private async Task<ScriptStartResult> StartCore(string hostname, string filename,
    IReadOnlyList<string> args, int threads, int? pid)
{ /* unchanged body */ }

// (f) ...and the one line inside it that passes the pid on
process = processManager.Create(filename, hostname, args.ToArray(), threads, ramCost, pid);
```
Everything else in `StartCore` is unchanged (RAM check, compile, and so on). A PID collision inside `Create` is caught by the existing `try/catch`, which releases the reserved RAM.

## New: `Runtime/ScriptRestoreResult.cs`

**What:** pairs each saved process with how its restart went, so the session can report failures.

```csharp
using NullByte.Core.Persistence;

namespace NullByte.Scripting.Runtime;

public sealed record ScriptRestoreResult(ProcessSave Saved, ScriptStartResult Start);
```

# 3. NullByte.Shell

## Changed: `GameSession.cs`

**Why:** the session needs a loaded game's clock time and processes passed in, and a way to take a snapshot.

```csharp
// (a) new using
using NullByte.Core.Persistence;

// (b) GameSessionOptions: the State comment is updated and three members are added
    public GameState? State { get; init; }   // null = new game; a loaded game passes its restored state
    // ...existing Clock / Random / ApiFactory unchanged...

    public TimeSpan InitialGameTime { get; init; } = TimeSpan.Zero;
    public IReadOnlyList<ProcessSave> ProcessesToRestart { get; init; } = [];

    public GameSessionOptions WithSave(RestoredGame restored)
    {
        ArgumentNullException.ThrowIfNull(restored);

        return this with
        {
            State = restored.State,
            InitialGameTime = restored.GameTime,
            ProcessesToRestart = restored.Processes
        };
    }

// (c) new field next to `compiler`, and an extra optional constructor parameter
    private readonly IReadOnlyList<ProcessSave> processesToRestart;

    internal GameSession(GameState state, IGameClock clock, GameLoop loop, GameServices services,
        IGameOutput output, IScriptCompiler compiler,
        IReadOnlyList<ProcessSave>? processesToRestart = null)
    {
        // ...existing assignments...
        this.processesToRestart = processesToRestart ?? [];
    }

// (d) StartAsync(): one new line after `await loop.Started;`
        await RestartSavedProcessesAsync();

// (e) new methods, placed before StopAsync
    public Task<SaveGame> CaptureSaveAsync(DateTimeOffset savedAtUtc)
        => loop.InvokeAsync(() => Task.FromResult(
            SaveGameMapper.Capture(State, Clock.Elapsed, Services.Scripts.CaptureRunningProcesses(), savedAtUtc)));

    private async Task RestartSavedProcessesAsync()
    {
        if (processesToRestart.Count == 0)
            return;

        output.WriteLine($"Restarting {processesToRestart.Count} script(s)...");

        var results = await loop.InvokeAsync(() => Services.Scripts.RestoreProcessesAsync(processesToRestart));

        foreach (var result in results.Where(result => !result.Start.Succeeded))
        {
            output.WriteLine(
                $"  Could not restart {result.Saved.Filename} (pid {result.Saved.Pid}) " +
                $"on {result.Saved.Hostname}: {result.Start.FailureMessage}");
        }
    }

// (f) StopAsync(): your commented-out "M6: KillAll()" line is replaced by a real call
            await KillScriptsAsync();      // was: // M6: Services.Scripts.KillAll();
            loop.RequestStop();

// (g) new helper, above DisposeAsync
    private async Task KillScriptsAsync()
    {
        if (!loop.IsRunning)
            return;

        try
        {
            await loop.InvokeAsync(() => { Services.Scripts.KillAll(); return Task.CompletedTask; });
        }
        catch (InvalidOperationException)
        {
            // The loop stopped between the check and the call: nothing left to kill.
        }
    }

// (h) Create(): the clock now starts from the saved time
        var clock = options.Clock ?? new RealGameClock(options.InitialGameTime)
                                     { SpeedMultiplier = options.Balance.GameSpeedMultiplier };

// (i) Create(): the final return passes the processes along
        return new GameSession(state, clock, gameLoop, services, options.Output, scriptCompiler, options.ProcessesToRestart);
```
The kill in (f) matters because `load` stops the old session. Without it, the old session's scripts stay suspended forever, each holding a loaded assembly. The `IsRunning` guard is there because `StopAsync` can be called twice, once by `Program` and once by `DisposeAsync`.

## New: `Persistence/ISaveStore.cs`

**What:** the storage abstraction (named slots holding JSON text), so tests and a future WPF host can use something other than files. `SaveSlot` holds the slot-name rule, since slot names become file names.

```csharp
namespace NullByte.Shell.Persistence;

public interface ISaveStore
{
    Task WriteAsync(string slot, string json, CancellationToken cancellationToken = default);
    Task<string?> ReadAsync(string slot, CancellationToken cancellationToken = default);   // null = missing
    Task<IReadOnlyList<SaveSlotInfo>> ListAsync(CancellationToken cancellationToken = default);
}

public sealed record SaveSlotInfo(string Name, DateTimeOffset SavedAtUtc);

public static class SaveSlot
{
    public const int MaxLength = 32;

    public static bool IsValidName(string? name)
        => name is { Length: > 0 and <= MaxLength } && name.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}
```

## New: `Persistence/FileSaveStore.cs`

**What:** one `{slot}.json` per slot. It writes to `.tmp` and swaps it in with `File.Replace`, keeping the previous save as `.bak`. A crash mid-write can't leave a half-written save, and you always have one generation back. The slot-name check here is the path-traversal defence.

```csharp
using System.Text;

namespace NullByte.Shell.Persistence;

public sealed class FileSaveStore(string directory) : ISaveStore
{
    private const string Extension = ".json";

    public async Task WriteAsync(string slot, string json, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(json);

        var path = PathFor(slot);
        Directory.CreateDirectory(directory);

        var temporaryPath = path + ".tmp";
        await File.WriteAllTextAsync(temporaryPath, json,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken)
            .ConfigureAwait(false);

        if (File.Exists(path))
            File.Replace(temporaryPath, path, path + ".bak");
        else
            File.Move(temporaryPath, path);
    }

    public async Task<string?> ReadAsync(string slot, CancellationToken cancellationToken = default)
    {
        var path = PathFor(slot);

        return File.Exists(path)
            ? await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false)
            : null;
    }

    public Task<IReadOnlyList<SaveSlotInfo>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directory))
            return Task.FromResult<IReadOnlyList<SaveSlotInfo>>([]);

        IReadOnlyList<SaveSlotInfo> slots = new DirectoryInfo(directory)
            .EnumerateFiles("*" + Extension)
            .Where(file => SaveSlot.IsValidName(Path.GetFileNameWithoutExtension(file.Name)))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(file => new SaveSlotInfo(Path.GetFileNameWithoutExtension(file.Name), file.LastWriteTimeUtc))
            .ToList();

        return Task.FromResult(slots);
    }

    private string PathFor(string slot)
    {
        if (!SaveSlot.IsValidName(slot))
            throw new ArgumentException("Save names may only contain letters, digits, '-' and '_'.", nameof(slot));

        return Path.Combine(directory, slot + Extension);
    }
}
```

## New: `Persistence/SaveManager.cs`

**What:** the orchestrator.
- `SaveAsync` snapshots on the game thread and then serializes and writes elsewhere, so a slow disk never stalls the simulation.
- The semaphore covers snapshot and write together, so an autosave and a manual save can't leave an older snapshot on top of a newer one.
- `LoadAsync` does a full read, parse, migrate and validate without touching the running game.

```csharp
using NullByte.Core.Persistence;

namespace NullByte.Shell.Persistence;

public sealed class SaveManager
{
    private readonly ISaveStore store;
    private readonly SaveGameSerializer serializer;
    private readonly TimeProvider time;
    private readonly SemaphoreSlim gate = new(1, 1);

    public SaveManager(ISaveStore store, SaveGameSerializer serializer, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(serializer);

        this.store = store;
        this.serializer = serializer;
        time = timeProvider ?? TimeProvider.System;
    }

    public async Task SaveAsync(GameSession session, string slot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var snapshot = await session.CaptureSaveAsync(time.GetUtcNow())
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);

            var json = serializer.Serialize(snapshot);

            await store.WriteAsync(slot, json, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<RestoredGame?> LoadAsync(string slot, CancellationToken cancellationToken = default)
    {
        var json = await store.ReadAsync(slot, cancellationToken).ConfigureAwait(false);

        return json is null
            ? null
            : SaveGameMapper.Restore(serializer.Deserialize(json));
    }

    public Task<IReadOnlyList<SaveSlotInfo>> ListAsync(CancellationToken cancellationToken = default)
        => store.ListAsync(cancellationToken);
}
```

## New: `Persistence/AutosaveService.cs`

**What:** a `PeriodicTimer` loop that saves to the `autosave` slot, in real time rather than game time (the debug build runs at 5× speed). A failed autosave prints a message and the loop carries on. Disposing it cancels and awaits the loop, so `GameHost` disposes it before stopping the session.

```csharp
using NullByte.Core.Services;

namespace NullByte.Shell.Persistence;

public sealed class AutosaveService : IAsyncDisposable
{
    public const string Slot = "autosave";

    private readonly SaveManager saves;
    private readonly GameSession session;
    private readonly IGameOutput output;
    private readonly CancellationTokenSource stop = new();
    private readonly Task running;

    public AutosaveService(SaveManager saves, GameSession session, IGameOutput output,
                           TimeSpan interval, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(saves);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);

        this.saves = saves;
        this.session = session;
        this.output = output;

        running = RunAsync(interval, timeProvider ?? TimeProvider.System);
    }

    private async Task RunAsync(TimeSpan interval, TimeProvider time)
    {
        using var timer = new PeriodicTimer(interval, time);

        try
        {
            while (await timer.WaitForNextTickAsync(stop.Token).ConfigureAwait(false))
            {
                try
                {
                    await saves.SaveAsync(session, Slot, stop.Token).ConfigureAwait(false);
                    output.WriteLine("Game autosaved.");
                }
                catch (OperationCanceledException) when (stop.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    output.WriteLine($"Autosave failed: {exception.Message}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Disposed.
        }
    }

    public async ValueTask DisposeAsync()
    {
        await stop.CancelAsync().ConfigureAwait(false);
        await running.ConfigureAwait(false);
        stop.Dispose();
    }
}
```

# 4. NullByte.ConsoleUI

## New: `LoadRequest.cs`

**What:** the message `load` sends to the host. It exists because `load` runs on the game thread of the session it would replace. Disposing that session from inside would deadlock, since the loop would be waiting on itself.

```csharp
namespace NullByte.ConsoleUI;

public sealed class LoadRequest
{
    private string? slot;

    public void Request(string slotName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slotName);
        slot = slotName;
    }

    /// <summary>Returns the requested slot, if any, and clears the request.</summary>
    public string? Take()
    {
        var requested = slot;
        slot = null;
        return requested;
    }
}
```
`Take()` clears the request on purpose. Otherwise a failed load would re-trigger after every later command.

## New: `Commands/Groups/PersistenceCommands.cs`

**What:** `save [name]`, `load [name]` and `saves`, in the same `ICommandGroup` style as your other commands. `save` awaits the manager. `load` only prints and files the request. Both default to the slot `quicksave`.

```csharp
using NullByte.ConsoleUI.Terminal;
using NullByte.Shell;
using NullByte.Shell.Persistence;

namespace NullByte.ConsoleUI.Commands.Groups;

public sealed class PersistenceCommands(
    GameSession session, SaveManager saves, LoadRequest loadRequest, IGameTerminal terminal) : ICommandGroup
{
    private const string DefaultSlot = "quicksave";

    public void Register(CommandRegistry registry)
    {
        registry.Register(new CommandDefinition("save", "save [name]",
            $"Save the game (default name '{DefaultSlot}').", SaveAsync) { MaxArgs = 1 });

        registry.Register(new CommandDefinition("load", "load [name]",
            $"Load a saved game (default name '{DefaultSlot}'). Unsaved progress is lost.", LoadAsync) { MaxArgs = 1 });

        registry.Register(new CommandDefinition("saves", "saves",
            "List saved games, newest first.", ListAsync));
    }

    private async Task SaveAsync(IReadOnlyList<string> arguments)
    {
        var slot = SlotFrom(arguments);
        if (slot is null)
            return;

        try
        {
            await saves.SaveAsync(session, slot);
            terminal.WriteLine($"Game saved to '{slot}'.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            terminal.WriteLine($"Could not save: {exception.Message}");
        }
    }

    private Task LoadAsync(IReadOnlyList<string> arguments)
    {
        var slot = SlotFrom(arguments);
        if (slot is null)
            return Task.CompletedTask;

        // This command runs on the game thread of the session it would replace,
        // so it only asks. The host swaps the session when the command returns.
        terminal.WriteLine($"Loading '{slot}'...");
        loadRequest.Request(slot);

        return Task.CompletedTask;
    }

    private async Task ListAsync(IReadOnlyList<string> arguments)
    {
        var slots = await saves.ListAsync();

        if (slots.Count == 0)
        {
            terminal.WriteLine("No saved games.");
            return;
        }

        foreach (var slot in slots)
            terminal.WriteLine($"  {slot.Name,-34} {slot.SavedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
    }

    private string? SlotFrom(IReadOnlyList<string> arguments)
    {
        var slot = arguments.Count > 0 ? arguments[0] : DefaultSlot;

        if (SaveSlot.IsValidName(slot))
            return slot;

        terminal.WriteLine($"Invalid name. Use letters, digits, '-' and '_' (at most {SaveSlot.MaxLength} characters).");
        return null;
    }
}
```

## Changed: `Commands/CommandSetup.cs`

**Why:** register the new group. The new parameters are optional, so existing callers and tests that call `CommandSetup.Create(session, terminal)` still compile and simply don't get the commands.

```csharp
using NullByte.Shell.Persistence;                       // new using

public static CommandRegistry Create(
    GameSession session,
    IGameTerminal terminal,
    ShutdownRequest? shutdownRequest = null,
    SaveManager? saves = null,                          // new
    LoadRequest? loadRequest = null)                    // new
{
    var registry = new CommandRegistry();

    List<ICommandGroup> groups =                        // was: ICommandGroup[] groups =
    [
        /* ...the same eight groups as before... */
    ];

    // Hosts that cannot save (and tests) simply leave these out.
    if (saves is not null && loadRequest is not null)   // new
        groups.Add(new PersistenceCommands(session, saves, loadRequest, terminal));

    foreach (var group in groups)
        group.Register(registry);

    return registry;
}
```

## New: `GameHost.cs`

**What:** the heart of `load`. It owns the current game as one bundle (`Game`: session, dispatcher, completion catalog, load request, autosave). Most of it is your old `Program.ProcessCommandsAsync`, moved here, plus the swap.

- `StartAsync`: resumes the autosave if there is one, otherwise starts a new game.
- `RunAsync`: your command loop. After each command, `current.LoadRequest.Take()` triggers `LoadAsync`.
- `LoadAsync`: validate first; only then stop the old autosave and session, and start a new game from the save. Input is disabled during the swap, so typed lines can't leak into the new game.
- `DisposeAsync`: on a clean `exit`, it saves to `autosave` before stopping the session.

```csharp
using NullByte.ConsoleUI.Commands;
using NullByte.ConsoleUI.Terminal;
using NullByte.Core.Persistence;
using NullByte.Shell;
using NullByte.Shell.Persistence;

namespace NullByte.ConsoleUI;

public sealed class GameHost : IAsyncDisposable
{
    private static readonly TimeSpan AutosaveInterval = TimeSpan.FromMinutes(5);

    private readonly ThreadSafeConsoleTerminal terminal;
    private readonly SaveManager saves;
    private readonly ShutdownRequest shutdownRequest;
    private readonly GameSessionOptions baseOptions;

    private Game? game;

    // Everything that belongs to one session and goes away with it.
    private sealed record Game(
        GameSession Session, CommandDispatcher Dispatcher, CompletionCatalog Completion,
        LoadRequest LoadRequest, AutosaveService Autosave);

    public GameHost(ThreadSafeConsoleTerminal terminal, SaveManager saves,
                    ShutdownRequest shutdownRequest, GameSessionOptions baseOptions)
    {
        this.terminal = terminal;
        this.saves = saves;
        this.shutdownRequest = shutdownRequest;
        this.baseOptions = baseOptions;
    }

    public async Task StartAsync(bool resumeAutosave)
    {
        if (resumeAutosave)
        {
            var restored = await TryLoadAsync(AutosaveService.Slot, reportMissing: false);
            if (restored is not null)
            {
                terminal.WriteLine("Continuing from your autosave (start with --new for a new game).");
                game = await StartGameAsync(restored);
                return;
            }
        }

        game = await StartGameAsync(restored: null);
        WriteStartupMessage();
    }

    public async Task RunAsync()
    {
        await foreach (var commandLine in terminal.ReadCommandsAsync())
        {
            if (string.IsNullOrWhiteSpace(commandLine))
                continue;

            var current = game ?? throw new InvalidOperationException("The game has not been started.");

            try
            {
                await current.Session.GameThread.InvokeAsync(async () =>
                {
                    try
                    {
                        terminal.SetInputEnabled(false);
                        await current.Dispatcher.DispatchAsync(commandLine);

                        // Update terminal prompt
                        terminal.SetPrompt($"root@{current.Session.State.Player.CurrentHostname} $ ");
                    }
                    finally
                    {
                        terminal.SetInputEnabled(true);
                        // Commands such as scan and connect change what can be completed.
                        current.Completion.Refresh();
                    }
                });
            }
            catch (OperationCanceledException) when (terminal.StoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (InvalidOperationException) when (!current.Session.GameThread.IsRunning)
            {
                return;
            }
            catch (Exception exception)
            {
                terminal.WriteLine($"Command failed: {exception.Message}");
            }

            if (shutdownRequest.IsRequested)
                break;

            if (current.LoadRequest.Take() is { } slot)
                await LoadAsync(slot);
        }
    }

    public async ValueTask DisposeAsync()
    {
        var current = game;
        game = null;

        if (current is null)
            return;

        await current.Autosave.DisposeAsync();

        if (shutdownRequest.IsRequested && current.Session.GameThread.IsRunning)
        {
            try
            {
                await saves.SaveAsync(current.Session, AutosaveService.Slot);
            }
            catch (Exception exception)
            {
                terminal.WriteLine($"Could not save on exit: {exception.Message}");
            }
        }

        await current.Session.DisposeAsync();
    }

    private async Task LoadAsync(string slot)
    {
        // Typed input would otherwise queue up and run in the new game.
        terminal.SetInputEnabled(false);

        try
        {
            // Read and validate first. Only a save that loads cleanly replaces the running game.
            var restored = await TryLoadAsync(slot, reportMissing: true);
            if (restored is null)
            {
                terminal.WriteLine("The current game is unchanged.");
                return;
            }

            var old = game!;
            await old.Autosave.DisposeAsync();
            await old.Session.DisposeAsync();

            game = await StartGameAsync(restored);

            terminal.WriteLine($"Loaded '{slot}'. Game time: {restored.GameTime.TotalSeconds:F3}s");
        }
        finally
        {
            terminal.SetInputEnabled(true);
        }
    }

    private async Task<RestoredGame?> TryLoadAsync(string slot, bool reportMissing)
    {
        try
        {
            var restored = await saves.LoadAsync(slot);

            if (restored is null && reportMissing)
                terminal.WriteLine($"No save named '{slot}'.");

            return restored;
        }
        catch (Exception exception) when (exception is SaveFormatException or IOException or UnauthorizedAccessException)
        {
            terminal.WriteLine($"Could not load '{slot}': {exception.Message}");
            return null;
        }
    }

    private async Task<Game> StartGameAsync(RestoredGame? restored)
    {
        var options = restored is null ? baseOptions : baseOptions.WithSave(restored);
        var session = GameSession.Create(options);
        var loadRequest = new LoadRequest();

        var registry = CommandSetup.Create(session, terminal, shutdownRequest, saves, loadRequest);
        var dispatcher = new CommandDispatcher(registry, terminal);
        var completion = new CompletionCatalog(registry, session.State);
        terminal.SetCompletionProvider(completion);

        await session.StartAsync();

        // Initialize the completion catalog
        await session.GameThread.InvokeAsync(() =>
        {
            completion.Refresh();
            return Task.CompletedTask;
        });

        terminal.SetPrompt($"root@{session.State.Player.CurrentHostname} $ ");

        var autosave = new AutosaveService(saves, session, terminal, AutosaveInterval);

        return new Game(session, dispatcher, completion, loadRequest, autosave);
    }

    private void WriteStartupMessage()
    {
        var clock = game!.Session.Clock;

        terminal.WriteLine("NullByte");
        terminal.WriteLine();
        terminal.WriteLine("Single-threaded simulation started.");
        terminal.WriteLine($"Game thread ready. Game time: {clock.Elapsed.TotalSeconds:F3}s");
        terminal.WriteLine();
        terminal.WriteLine("Type 'help' to see available commands ('debug' for other commands).");
        terminal.WriteLine("Tab completes commands and server names, Up/Down recalls earlier input.");
        terminal.WriteLine("The game autosaves every few minutes; 'save' and 'load' manage your own saves.");
        terminal.WriteLine();
    }
}
```

## Changed (rewritten): `Program.cs`

**Why:** session creation, the command loop and shutdown moved into `GameHost`. `Main` now creates the terminal, the save manager and the host. The `--new` flag controls whether the autosave is resumed. The `Balance` debug override with its TODO is kept as it was. `host.DisposeAsync()` runs while the terminal is still alive, so the exit-time save happens first; it is idempotent, so `await using` can safely dispose it a second time.

```csharp
using System.Text;
using NullByte.ConsoleUI.Terminal;
using NullByte.Core.Persistence;
using NullByte.Shell;
using NullByte.Shell.Persistence;

namespace NullByte.ConsoleUI;

public class Program
{
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.TreatControlCAsInput = true; // Ctrl+c no longer exists the application

        await using var terminal = new ThreadSafeConsoleTerminal(prompt: "root@home $ ", maximumHistoryLines: 2_000);

        var saves = new SaveManager(new FileSaveStore(GetSaveDirectory()), new SaveGameSerializer());
        var shutdownRequest = new ShutdownRequest();

        var sessionOptions = new GameSessionOptions()
        {
            Output = terminal,
            // TODO: remove this (only for debugging)
            Balance = new Core.Configuration.BalanceConfig() { GameSpeedMultiplier = 5.0, AnalyzeDuration = 5.0 }
        };
        await using var host = new GameHost(terminal, saves, shutdownRequest, sessionOptions);

        var terminalTask = terminal.RunAsync();

        try
        {
            await host.StartAsync(resumeAutosave: !args.Contains("--new"));
            await host.RunAsync();
        }
        finally
        {
            // Save and stop the game while the terminal is still running, then stop the terminal.
            await host.DisposeAsync();
            terminal.RequestStop();
            await terminalTask;
        }

        Console.WriteLine("Everything stopped, bye");
        await Task.Delay(500);
    }

    private static string GetSaveDirectory()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NullByte", "saves");
}
```

# 5. Tests

## New: `NullByte.Core.Tests/Persistence/SaveGameTests.cs`

**What:** the unit tests for the format.
- `CreateState()` builds a small played game that exercises every kind of state. Edges are deliberately not alphabetical, and `0.1 + 0.2` and `1/3` check exact double round-trips.
- Round trip: capture, serialize, deserialize, restore, capture again, and compare the JSON. Comparing the captured form checks every field at once.
- Neighbor order and exact file contents.
- Determinism.
- The fractional-security regression.
- Golden-file tests.
- Migration order, a missing migration, and newer-version rejection.
- Garbage and wrongly shaped input, which must surface as `SaveFormatException`.
- Data the game would reject, which must be refused before anything starts.

```csharp
using System.Text.Json.Nodes;
using NullByte.Core.Domain;
using NullByte.Core.Persistence;
using Xunit;

namespace NullByte.Core.Tests.Persistence;

public class SaveGameTests
{
    private static readonly DateTimeOffset SavedAt = new(2026, 11, 17, 10, 30, 0, TimeSpan.Zero);
    private static readonly TimeSpan GameTime = TimeSpan.FromSeconds(754.25);
    private static readonly SaveGameSerializer Serializer = new();

    private static readonly ProcessSave[] Processes =
    [
        new() { Pid = 7, Filename = "hack.cx", Hostname = "n00dles", Args = ["foodnstuff", "two words"], Threads = 2 },
        new() { Pid = 3, Filename = "weaken.cx", Hostname = "home", Args = [], Threads = 1 }
    ];

    /// <summary>A small game that has been played a bit: every kind of state the save has to carry.</summary>
    internal static GameState CreateState()
    {
        var network = new Network();
        network.AddServer(new Server("home", 0, 0, 1, 1, 1, 1, 1, 8, hasRootAccess: true));
        network.AddServer(new Server("n00dles", 50_000, 25_000, 3, 1, 3, 1, 30, 4));
        network.AddServer(new Server("foodnstuff", 100_000, 60_000, 5, 2, 5, 5, 25, 8));
        network.AddServer(new Server("zero-day", 1_000_000, 500_000, 40, 7.54, 66.5, 50, 10, 16));

        // Deliberately not alphabetical: home's neighbors are n00dles, then foodnstuff.
        network.Connect("home", "n00dles");
        network.Connect("home", "foodnstuff");
        network.Connect("n00dles", "foodnstuff");
        network.Connect("foodnstuff", "zero-day");

        var player = new Player("home", startingMoney: 0.1 + 0.2, startingHackingExperience: 1.0 / 3.0)
        {
            CurrentHostname = "n00dles"
        };
        player.DiscoverServer("n00dles");
        player.DiscoverServer("foodnstuff");

        var state = new GameState(player, network);

        network.GetServer("n00dles").GrantRootAccess();
        network.GetServer("n00dles").RemoveMoney(1.0 / 3.0);
        network.GetServer("n00dles").IncreaseSecurity(0.002);

        var node = state.Botnet.AddNode();
        node.ApplyUpgrade(BotnetUpgradeKind.Level);
        node.ApplyUpgrade(BotnetUpgradeKind.Ram);
        node.ApplyUpgrade(BotnetUpgradeKind.Cores);
        state.Botnet.AddNode();
        state.Botnet.RecordEarnings(12.345);

        state.FileSystem.WriteFile("home", "hack.cx", "var t = args[0];\nnb.Print(\"héllo \\\"world\\\" ✓\");\n");
        state.FileSystem.WriteFile("home", "empty.cx", "");
        state.FileSystem.WriteFile("n00dles", "hack.cx", "await nb.Hack();");

        return state;
    }

    private static SaveGame Capture(GameState state) => SaveGameMapper.Capture(state, GameTime, Processes, SavedAt);

    private static string Describe(GameState state) => Serializer.Serialize(Capture(state));

    [Fact]
    public void Restore_reproduces_everything_that_was_saved()
    {
        var original = CreateState();

        var restored = SaveGameMapper.Restore(Serializer.Deserialize(Serializer.Serialize(Capture(original))));

        // Comparing the captured form checks every field in one go, including exact doubles.
        Assert.Equal(Describe(original), Describe(restored.State));
        Assert.Equal(GameTime, restored.GameTime);
        Assert.Equal(new[] { 3, 7 }, restored.Processes.Select(p => p.Pid));

        Assert.Equal(0.1 + 0.2, restored.State.Player.Money);
        Assert.Equal(1.0 / 3.0, restored.State.Player.HackingExperience);
        Assert.Equal("n00dles", restored.State.Player.CurrentHostname);
        Assert.True(restored.State.Network.GetServer("n00dles").HasRootAccess);
        Assert.Equal(2, restored.State.Botnet.NodeCount);
        Assert.Equal(12.345, restored.State.Botnet.TotalEarned);
        Assert.Equal(0.0, restored.State.Network.GetServer("home").UsedRam);
    }

    [Fact]
    public void Neighbors_keep_the_order_scan_shows_them_in()
    {
        var restored = SaveGameMapper.Restore(Serializer.Deserialize(Describe(CreateState())));

        Assert.Equal(
            new[] { "n00dles", "foodnstuff" },
            restored.State.Network.GetNeighbors("home").Select(server => server.Hostname));
        Assert.Equal(
            new[] { "home", "foodnstuff" },
            restored.State.Network.GetNeighbors("n00dles").Select(server => server.Hostname));
    }

    [Fact]
    public void File_contents_survive_exactly()
    {
        var restored = SaveGameMapper.Restore(Serializer.Deserialize(Describe(CreateState())));

        Assert.True(restored.State.FileSystem.TryReadFile("home", "hack.cx", out var content));
        Assert.Equal("var t = args[0];\nnb.Print(\"héllo \\\"world\\\" ✓\");\n", content);
        Assert.True(restored.State.FileSystem.TryReadFile("home", "empty.cx", out var empty));
        Assert.Equal("", empty);
    }

    [Fact]
    public void The_same_game_always_produces_the_same_file()
    {
        Assert.Equal(Describe(CreateState()), Describe(CreateState()));
    }

    [Fact]
    public void A_fully_weakened_server_with_fractional_minimum_security_still_loads()
    {
        // Subtracting down to the minimum can land a hair below it (7.539999999999999 for 7.54),
        // which the Server constructor refuses. The save must clamp instead of becoming unloadable.
        var network = new Network();
        network.AddServer(new Server("home", 0, 0, 1, 1, 1, 1, 1, 8, hasRootAccess: true));
        network.AddServer(new Server("zero-day", 1_000_000, 500_000, 40, 7.54, 66.41759912548187, 50, 10, 16));
        var state = new GameState(new Player("home"), network);

        var server = network.GetServer("zero-day");
        server.DecreaseSecurity(1_000_000);
        Assert.True(server.Security < server.MinimumSecurity, "precondition: rounding pushed security below its minimum");

        var json = Serializer.Serialize(SaveGameMapper.Capture(state, GameTime, [], SavedAt));
        var restored = SaveGameMapper.Restore(Serializer.Deserialize(json));

        Assert.Equal(7.54, restored.State.Network.GetServer("zero-day").Security);
    }

    [Fact]
    public void Golden_file_matches_the_current_format()
    {
        // If this fails you changed the save format. Bump SaveGame.CurrentVersion, add a migration,
        // and keep the old golden text as a test for that migration. Do not just edit the golden text.
        Assert.Equal(Normalize(GoldenSaves.V1), Describe(CreateState()));
    }

    [Fact]
    public void The_golden_file_loads_and_saves_back_unchanged()
    {
        var restored = SaveGameMapper.Restore(Serializer.Deserialize(GoldenSaves.V1));

        var again = Serializer.Serialize(SaveGameMapper.Capture(restored.State, restored.GameTime, restored.Processes, SavedAt));

        Assert.Equal(Normalize(GoldenSaves.V1), again);
    }

    [Fact]
    public void A_save_from_a_newer_version_is_refused_with_a_clear_message()
    {
        var json = JsonNode.Parse(Describe(CreateState()))!.AsObject();
        json["version"] = SaveGame.CurrentVersion + 1;

        var error = Assert.Throws<SaveFormatException>(() => Serializer.Deserialize(json.ToJsonString()));

        Assert.Contains("newer version", error.Message);
    }

    [Fact]
    public void Migrations_run_in_order_up_to_the_current_version()
    {
        var steps = new List<int>();
        var migrator = new SaveMigrator(
            [new AddMoney(1, 100, steps), new AddMoney(2, 20, steps)],
            currentVersion: 3);
        var serializer = new SaveGameSerializer(migrator);

        var loaded = serializer.Deserialize(GoldenSaves.V1);

        Assert.Equal(new[] { 1, 2 }, steps);
        Assert.Equal(3, loaded.Version);
        Assert.Equal(0.1 + 0.2 + 120, loaded.Player.Money);
    }

    [Fact]
    public void A_missing_migration_is_an_error_not_a_guess()
    {
        var serializer = new SaveGameSerializer(new SaveMigrator([], currentVersion: 2));

        Assert.Throws<SaveFormatException>(() => serializer.Deserialize(GoldenSaves.V1));
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{ \"version\": \"one\" }")]
    public void Garbage_is_reported_as_a_damaged_save(string text)
    {
        Assert.Throws<SaveFormatException>(() => Serializer.Deserialize(text));
    }

    [Theory]
    [InlineData("files", "null")]
    [InlineData("processes", "{}")]
    [InlineData("player.money", "\"lots\"")]
    public void Wrongly_shaped_data_is_reported_as_a_damaged_save(string path, string value)
    {
        Assert.Throws<SaveFormatException>(() => Serializer.Deserialize(WithValue(path, value)));
    }

    [Theory]
    [InlineData("player.currentHostname", "\"nowhere\"")]
    [InlineData("player.money", "-5")]
    [InlineData("botnet.nodes[0].ram", "3")]
    [InlineData("files[0].name", "\"../escape.cx\"")]
    [InlineData("processes[0].hostname", "\"nowhere\"")]
    [InlineData("processes[0].pid", "7")]          // duplicates the other process
    [InlineData("gameTime", "\"-00:00:01\"")]
    public void Data_the_game_would_reject_is_refused_before_anything_is_started(string path, string value)
    {
        var save = Serializer.Deserialize(WithValue(path, value));

        Assert.Throws<SaveFormatException>(() => SaveGameMapper.Restore(save));
    }

    private static string WithValue(string path, string jsonValue)
    {
        var root = JsonNode.Parse(Describe(CreateState()))!.AsObject();
        var node = (JsonNode)root;

        var segments = path.Split('.');
        for (var i = 0; i < segments.Length; i++)
        {
            var (name, index) = Parse(segments[i]);
            var parent = node;
            node = index is null ? parent[name]! : parent[name]![index.Value]!;

            if (i == segments.Length - 1)
            {
                var replacement = JsonNode.Parse(jsonValue);
                if (index is null)
                    parent.AsObject()[name] = replacement;
                else
                    parent[name]!.AsArray()[index.Value] = replacement;
            }
        }

        return root.ToJsonString();
    }

    private static (string Name, int? Index) Parse(string segment)
    {
        var open = segment.IndexOf('[');
        return open < 0
            ? (segment, null)
            : (segment[..open], int.Parse(segment[(open + 1)..^1]));
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n");

    private sealed class AddMoney(int from, double amount, List<int> steps) : ISaveMigration
    {
        public int FromVersion => from;

        public void Migrate(JsonObject save)
        {
            steps.Add(from);
            save["player"]!["money"] = save["player"]!["money"]!.GetValue<double>() + amount;
        }
    }
}
```
The comment on the duplicate-PID `[InlineData]` line is added here to explain it. Everything else is as it ran.

## New: `NullByte.Core.Tests/Persistence/GoldenSaves.cs`

**What:** the v1 file, frozen as a raw string literal. I chose a literal over a data file so it needs no `csproj` copy settings. Tests normalize CRLF, because your repo may use it. The rule in the comment matters: when you ship v2, leave this entry untouched and add a `V2`. This one then proves old saves still load. Shown abbreviated, since it is the generated output from `CreateState()`. The full file is in the zip.

```csharp
namespace NullByte.Core.Tests.Persistence;

internal static class GoldenSaves
{
    public const string V1 = """
{
  "version": 1,
  "savedAtUtc": "2026-11-17T10:30:00+00:00",
  "gameTime": "00:12:34.2500000",
  "player": {
    "homeHostname": "home",
    "currentHostname": "n00dles",
    "money": 0.30000000000000004,
    "hackingExperience": 0.3333333333333333,
    "discoveredHostnames": [ "foodnstuff", "home", "n00dles" ]   // (one per line in the real file)
  },
  "servers": [ /* foodnstuff, home, n00dles, zero-day: all ten fields each */ ],
  "connections": [ /* home→n00dles, home→foodnstuff, n00dles→foodnstuff, foodnstuff→zero-day */ ],
  "botnet": { "totalEarned": 12.345, "nodes": [ /* two nodes */ ] },
  "files": [ /* empty.cx, hack.cx on home; hack.cx on n00dles */ ],
  "processes": [ /* pid 3 weaken.cx on home; pid 7 hack.cx on n00dles */ ]
}
""";
}
```
The `//` and `/* */` notes above are only my abbreviation. The real file is plain JSON.

## New: `NullByte.ConsoleUI.Tests/Persistence/SessionSaveLoadTests.cs`

**What:** session-level tests that need the Roslyn compile, so they sit in a project that references Shell. In my harness they all ran. The `TestOutput` and `MemoryStore` helpers are small fakes at the top of the file.
- A script with 2 threads and two arguments comes back with the same PID, arguments and `UsedRam`.
- The clock continues from the saved time.
- A script that can't restart (its file is missing from the save) is reported, and the game still loads.
- `SaveManager` round-trips a slot.
- Autosave writes its slot (with a 50 ms interval).
- `FileSaveStore` keeps the `.bak`, leaves no `.tmp`, and rejects `../outside` and empty names.

```csharp
using NullByte.Core.Persistence;
using NullByte.Core.Scripting;
using NullByte.Core.Services;
using NullByte.Shell;
using NullByte.Shell.Persistence;
using Xunit;

namespace NullByte.ConsoleUI.Tests.Persistence;

public class SessionSaveLoadTests
{
    private const string LoopScript = "while (true) { await nb.Sleep(TimeSpan.FromSeconds(1)); }";

    private static readonly SaveGameSerializer Serializer = new();

    private sealed class TestOutput : IGameOutput
    {
        private readonly List<string> lines = [];
        public IReadOnlyList<string> Lines { get { lock (lines) return lines.ToArray(); } }
        public void WriteLine(string message) { lock (lines) lines.Add(message); }
        public void WriteLine() => WriteLine("");
    }

    private sealed class MemoryStore : ISaveStore
    {
        private readonly Dictionary<string, string> slots = [];

        public Task WriteAsync(string slot, string json, CancellationToken cancellationToken = default)
        { lock (slots) slots[slot] = json; return Task.CompletedTask; }

        public Task<string?> ReadAsync(string slot, CancellationToken cancellationToken = default)
        { lock (slots) return Task.FromResult(slots.GetValueOrDefault(slot)); }

        public Task<IReadOnlyList<SaveSlotInfo>> ListAsync(CancellationToken cancellationToken = default)
        { lock (slots) return Task.FromResult<IReadOnlyList<SaveSlotInfo>>(
            slots.Keys.Select(k => new SaveSlotInfo(k, DateTimeOffset.UnixEpoch)).ToList()); }
    }

    /// <summary>Plays a little: a script running with two threads on home, args and all.</summary>
    private static async Task<(GameSession Session, SaveGame Save, double UsedRam)> StartPlayedGameAsync(TestOutput output)
    {
        var session = GameSession.Create(new GameSessionOptions { Output = output });
        await session.StartAsync();

        var usedRam = await session.GameThread.InvokeAsync(async () =>
        {
            session.State.FileSystem.WriteFile("home", "loop.cx", LoopScript);
            var result = await session.Services.Scripts.Start("home", "loop.cx", ["alpha", "two words"], threads: 2);
            Assert.True(result.Succeeded, result.FailureMessage);
            return session.State.Network.GetServer("home").UsedRam;
        });

        Assert.True(usedRam > 0);

        var save = await session.CaptureSaveAsync(DateTimeOffset.UtcNow);
        return (session, save, usedRam);
    }

    private static RestoredGame RoundTrip(SaveGame save)
        => SaveGameMapper.Restore(Serializer.Deserialize(Serializer.Serialize(save)));

    [Fact]
    public async Task A_running_script_comes_back_with_the_same_pid_threads_arguments_and_ram()
    {
        var output = new TestOutput();
        var (first, save, usedRam) = await StartPlayedGameAsync(output);
        await using var keepAlive = first;

        var processBefore = Assert.Single(save.Processes);

        await using var second = GameSession.Create(new GameSessionOptions { Output = output }.WithSave(RoundTrip(save)));
        await second.StartAsync();

        var running = await second.GameThread.InvokeAsync(() =>
            Task.FromResult(second.Services.Scripts.ProcessesRunning("home")));

        var restarted = Assert.Single(running);
        Assert.Equal(processBefore.Pid, restarted.Pid);
        Assert.Equal("loop.cx", restarted.Filename);
        Assert.Equal(2, restarted.Threads);
        Assert.Equal(new[] { "alpha", "two words" }, restarted.Args);
        Assert.Equal(ScriptProcessState.Running, restarted.State);

        var usedRamAfter = await second.GameThread.InvokeAsync(() =>
            Task.FromResult(second.State.Network.GetServer("home").UsedRam));
        Assert.Equal(usedRam, usedRamAfter);
    }

    [Fact]
    public async Task The_clock_continues_from_the_saved_game_time()
    {
        var output = new TestOutput();
        var (first, save, _) = await StartPlayedGameAsync(output);
        await using var keepAlive = first;

        var later = save with { GameTime = TimeSpan.FromHours(3) };

        await using var second = GameSession.Create(new GameSessionOptions { Output = output }.WithSave(RoundTrip(later)));
        await second.StartAsync();

        Assert.True(second.Clock.Elapsed >= TimeSpan.FromHours(3));
        Assert.True(second.Clock.Elapsed < TimeSpan.FromHours(3) + TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task A_script_that_cannot_restart_is_reported_and_the_game_still_loads()
    {
        var output = new TestOutput();
        var (first, save, _) = await StartPlayedGameAsync(output);
        await using var keepAlive = first;

        // The script file is gone from the save, so its process cannot start again.
        var withoutScript = save with { Files = save.Files.Where(file => file.Name != "loop.cx").ToArray() };

        await using var second = GameSession.Create(new GameSessionOptions { Output = output }.WithSave(RoundTrip(withoutScript)));
        await second.StartAsync();

        Assert.Contains(output.Lines, line => line.Contains("Could not restart loop.cx") && line.Contains("File not found"));

        var running = await second.GameThread.InvokeAsync(() =>
            Task.FromResult(second.Services.Scripts.ProcessesRunning("home")));
        Assert.Empty(running);
        Assert.Equal(0.0, second.State.Network.GetServer("home").UsedRam);
    }

    [Fact]
    public async Task Save_and_load_through_the_manager_round_trips_a_slot()
    {
        var output = new TestOutput();
        var (first, _, _) = await StartPlayedGameAsync(output);
        await using var keepAlive = first;

        var manager = new SaveManager(new MemoryStore(), Serializer);
        await manager.SaveAsync(first, "slot1");

        var restored = await manager.LoadAsync("slot1");
        Assert.NotNull(restored);
        Assert.Equal(new[] { "loop.cx" }, restored!.State.FileSystem.ListFiles("home"));
        Assert.Null(await manager.LoadAsync("missing"));
    }

    [Fact]
    public async Task Autosave_writes_the_autosave_slot_on_its_interval()
    {
        var output = new TestOutput();
        var (first, _, _) = await StartPlayedGameAsync(output);
        await using var keepAlive = first;

        var store = new MemoryStore();
        var manager = new SaveManager(store, Serializer);

        await using (new AutosaveService(manager, first, output, TimeSpan.FromMilliseconds(50)))
        {
            for (var i = 0; i < 100 && await store.ReadAsync(AutosaveService.Slot) is null; i++)
                await Task.Delay(50);
        }

        Assert.NotNull(await store.ReadAsync(AutosaveService.Slot));
        Assert.Contains(output.Lines, line => line == "Game autosaved.");
    }

    [Fact]
    public async Task The_file_store_keeps_the_previous_save_as_a_backup_and_rejects_unsafe_names()
    {
        var directory = Path.Combine(Path.GetTempPath(), "nullbyte-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileSaveStore(directory);

            Assert.Empty(await store.ListAsync());
            Assert.Null(await store.ReadAsync("one"));

            await store.WriteAsync("one", "first");
            await store.WriteAsync("one", "second");

            Assert.Equal("second", await store.ReadAsync("one"));
            Assert.Equal("first", await File.ReadAllTextAsync(Path.Combine(directory, "one.json.bak")));
            Assert.Equal(new[] { "one" }, (await store.ListAsync()).Select(slot => slot.Name));
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));

            await Assert.ThrowsAsync<ArgumentException>(() => store.WriteAsync("../outside", "x"));
            await Assert.ThrowsAsync<ArgumentException>(() => store.WriteAsync("", "x"));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
```

# Summary and a few notes

| Project | New | Changed |
|---|---|---|
| Core | `Persistence/` ×5 files | `Network`, `Botnet`, `RealGameClock` |
| Scripting | `ScriptRestoreResult` | `ScriptProcessManager`, `ScriptService` |
| Shell | `Persistence/` ×4 files | `GameSession` |
| ConsoleUI | `GameHost`, `LoadRequest`, `PersistenceCommands` | `Program`, `CommandSetup` |
| Tests | 3 files | none |

- **Transcription.** The blocks above are reproduced by hand from my working files, and I trimmed some doc comments to save space. The zip has the exact files that built and passed. For the changed files I'd review `M7-changes-to-existing-files.diff`, which is a real diff of the original against the new version.
- **Tests.** `NullByte.Core.Tests` and `NullByte.ConsoleUI.Tests` contain only the new test files. Drop them into your existing test projects. The session tests need a project that references Shell. I assumed `ConsoleUI.Tests` does (it should, since ConsoleUI does), but that's unverified because the bundle excluded your test projects.
- **Optional.** If you'd like `load` to complete save names with Tab, that needs a new completion kind and a refreshed slot list in `CompletionCatalog`. I left it out of M7.