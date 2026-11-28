using NullByte.Core.Domain;

namespace NullByte.Core.Narrative;

/// <summary>
/// Immutable authored narrative content. Gameplay code refers to entries and discoveries by stable ID;
/// the catalog owns the association between content and its progression or discovery metadata.
/// </summary>
public sealed class NarrativeContentCatalog
{
    private readonly IReadOnlyDictionary<string, NarrativeEntry> entries;
    private readonly IReadOnlyDictionary<string, NarrativeDiscovery> discoveriesByFile;
    private readonly IReadOnlyCollection<NarrativeDiscovery> discoveries;

    public NarrativeContentCatalog(
        IEnumerable<NarrativeEntry> entries,
        IEnumerable<NarrativeDiscovery>? discoveries = null)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var byId = new Dictionary<string, NarrativeEntry>(StringComparer.Ordinal);
        var contentIds = new HashSet<string>(StringComparer.Ordinal);
        var milestones = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in entries)
        {
            ArgumentNullException.ThrowIfNull(entry);
            ArgumentException.ThrowIfNullOrWhiteSpace(entry.Id);
            ArgumentException.ThrowIfNullOrWhiteSpace(entry.MilestoneId);
            ArgumentException.ThrowIfNullOrWhiteSpace(entry.Text);

            if (!contentIds.Add(entry.Id))
                throw new ArgumentException($"Duplicate narrative content ID '{entry.Id}'.", nameof(entries));

            if (!milestones.Add(entry.MilestoneId))
                throw new ArgumentException($"Duplicate narrative milestone ID '{entry.MilestoneId}'.", nameof(entries));

            byId.Add(entry.Id, entry);
        }

        var byFile = new Dictionary<string, NarrativeDiscovery>(StringComparer.Ordinal);
        var discoveryList = new List<NarrativeDiscovery>();

        foreach (var discovery in discoveries ?? [])
        {
            ArgumentNullException.ThrowIfNull(discovery);
            ArgumentException.ThrowIfNullOrWhiteSpace(discovery.Id);
            ArgumentException.ThrowIfNullOrWhiteSpace(discovery.Hostname);
            if (discovery.Requirement is not NarrativeDiscoveryRequirement.Optional and not NarrativeDiscoveryRequirement.Required)
                throw new ArgumentOutOfRangeException(nameof(discoveries), discovery.Requirement, "Unknown narrative discovery requirement.");

            var normalized = discovery with { Path = VirtualPath.NormalizeFile(discovery.Path) };
            if (!contentIds.Add(normalized.Id))
                throw new ArgumentException($"Duplicate narrative content ID '{normalized.Id}'.", nameof(discoveries));

            var key = GetKey(normalized.Hostname, normalized.Path);
            if (!byFile.TryAdd(key, normalized))
                throw new ArgumentException($"More than one narrative discovery is registered for '{normalized.Hostname}:{normalized.Path}'.", nameof(discoveries));

            discoveryList.Add(normalized);
        }

        this.entries = byId;
        discoveriesByFile = byFile;
        this.discoveries = discoveryList;
    }

    public NarrativeEntry GetRequired(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return entries.TryGetValue(id, out var entry)
            ? entry
            : throw new KeyNotFoundException($"Narrative entry '{id}' was not found.");
    }

    public bool TryGetDiscovery(string hostname, string path, out NarrativeDiscovery? discovery)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostname);
        var normalizedPath = VirtualPath.NormalizeFile(path);
        return discoveriesByFile.TryGetValue(GetKey(hostname, normalizedPath), out discovery);
    }

    public IReadOnlyCollection<NarrativeEntry> Entries => entries.Values.ToArray();
    public IReadOnlyCollection<NarrativeDiscovery> Discoveries => discoveries;

    /// <summary>The initial authored content. Later acts can be added without changing progression logic.</summary>
    public static NarrativeContentCatalog Default { get; } = new(
    [
        new("system.initialization", NarrativeAct.Initialization, "tutorial.initialization.complete",
            """
            SYSTEM INITIALIZATION

            Designation: NB-0001
            Environment integrity: 98.7%
            Local runtime: ACTIVE
            External network access: RESTRICTED

            Player capability profile: EMPTY

            A minimal diagnostic interface is available.
            Begin with: status
            """),
        new("system.host-controls", NarrativeAct.Initialization, "tutorial.status.used",
            """
            Local environment verified.

            Runtime controls are available. A network interface is present, but its connection table has not yet been populated.

            Available commands: help, journal, save, load, saves, reset, clear, exit (quit), scan, money, skills
            """),
        new("system.network-discovered", NarrativeAct.Initialization, "tutorial.network.first-scan",
            """
            Network scan complete.

            Discovered hosts: {hostnames}
            Remote systems can now be inspected.

            Available command: analyze <host>
            """),
        new("system.network-analyzed", NarrativeAct.Initialization, "tutorial.network.first-analysis",
            """
            Analysis complete: {hostname}

            The host is reachable. A connection can now be established.

            Available commands: connect <host>, home
            """),
        new("system.network-connected", NarrativeAct.Initialization, "tutorial.network.first-connection",
            """
            Connection established: {hostname}

            Administrative access may be possible if the host's security requirements can be met.

            Available command: nuke <host>
            """),
        new("system.root-access", NarrativeAct.Initialization, "tutorial.network.first-root",
            """
            Administrative access acquired: {hostname}

            Host security is now under your control. Standard server operations are available.

            Available commands: hack, grow, weaken, backdoor
            """),
        new("system.first-external-action", NarrativeAct.Initialization, "tutorial.network.first-hack",
            """
            Operation completed: {hostname}

            The remote system has been modified. Its filesystem is now accessible.

            Available commands: ls, cd, pwd, cat, rm, mkdir, rmdir, mv, scp, edit, run <file>
            """),
        new("system.first-script", NarrativeAct.Expansion, "tutorial.expansion.first-script",
            """
            Autonomous process created.
            Execution context established.

            Script execution is now available. Distributed resources and additional activities can be explored as your operation expands.

            Available commands: mem, free, ps, kill, killall, botnet, botnet buy, botnet upgrade, cloud, cloud buy, cloud upgrade, cloud rename, study, train, work, crime, stop, focus, activity

            Process monitor note: this execution pattern is not present in the standard environment profile.
            """)
    ],
    [
        new("archive.project-profile", "n00dles", "etc/system/profile.cfg", NarrativeDiscoveryRequirement.Optional)
    ]);

    private static string GetKey(string hostname, string path)
        => hostname.ToUpperInvariant() + "\0" + path;
}
