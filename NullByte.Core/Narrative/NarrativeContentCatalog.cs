namespace NullByte.Core.Narrative;

/// <summary>
/// Immutable authored narrative content. Gameplay code refers to entries by stable ID;
/// the catalog owns the association between an entry and its progression milestone.
/// </summary>
public sealed class NarrativeContentCatalog
{
    private readonly IReadOnlyDictionary<string, NarrativeEntry> entries;

    public NarrativeContentCatalog(IEnumerable<NarrativeEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var byId = new Dictionary<string, NarrativeEntry>(StringComparer.Ordinal);
        var milestones = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in entries)
        {
            ArgumentNullException.ThrowIfNull(entry);
            ArgumentException.ThrowIfNullOrWhiteSpace(entry.Id);
            ArgumentException.ThrowIfNullOrWhiteSpace(entry.MilestoneId);
            ArgumentException.ThrowIfNullOrWhiteSpace(entry.Text);

            if (!byId.TryAdd(entry.Id, entry))
                throw new ArgumentException($"Duplicate narrative entry ID '{entry.Id}'.", nameof(entries));

            if (!milestones.Add(entry.MilestoneId))
                throw new ArgumentException($"Duplicate narrative milestone ID '{entry.MilestoneId}'.", nameof(entries));
        }

        this.entries = byId;
    }

    public NarrativeEntry GetRequired(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return entries.TryGetValue(id, out var entry)
            ? entry
            : throw new KeyNotFoundException($"Narrative entry '{id}' was not found.");
    }

    public IReadOnlyCollection<NarrativeEntry> Entries => entries.Values.ToArray();

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
    ]);
}
