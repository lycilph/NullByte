using NullByte.Core.Domain;
using NullByte.Core.Services;

namespace NullByte.Core.Narrative;

/// <summary>
/// Converts successful gameplay events into one-time tutorial milestones.
/// All methods and subscribed events are expected to run on the game thread.
/// </summary>
public sealed class NarrativeCoordinator : IDisposable
{
    private readonly GameState state;
    private readonly NetworkService network;
    private readonly ServerActions actions;
    private readonly NarrativeContentCatalog content;

    public NarrativeCoordinator(GameState state, NetworkService network, ServerActions actions, NarrativeContentCatalog? content = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(actions);

        this.state = state;
        this.network = network;
        this.actions = actions;
        this.content = content ?? NarrativeContentCatalog.Default;
        network.ServersDiscovered += OnServersDiscovered;
        network.ServerAnalyzed += OnServerAnalyzed;
        network.ServerConnected += OnServerConnected;
        actions.RootAccessGained += OnRootAccessGained;
        actions.HackCompleted += OnHackCompleted;
    }

    public event Action<NarrativeMessage>? MessagePublished;

    /// <summary>Records a registered discovery after the player successfully reads its file.</summary>
    public bool FileRead(string hostname, string path)
    {
        if (!content.TryGetDiscovery(hostname, path, out var discovery) || discovery is null)
            return false;

        return state.Narrative.DiscoverDiscovery(discovery.Id);
    }

    public void Dispose()
    {
        network.ServersDiscovered -= OnServersDiscovered;
        network.ServerAnalyzed -= OnServerAnalyzed;
        network.ServerConnected -= OnServerConnected;
        actions.RootAccessGained -= OnRootAccessGained;
        actions.HackCompleted -= OnHackCompleted;
    }

    public void Initialize()
    {
        if (state.Narrative.IsFreeplay || state.Narrative.CompletedMilestones.Contains("tutorial.initialization.complete"))
            return;

        Complete("system.initialization", null, "player.inspect");
    }

    /// <summary>
    /// Returns concise tutorial guidance for capabilities the player has already unlocked.
    /// This is deliberately derived from capability state rather than from terminal history,
    /// so the essential instructions remain available after save/load.
    /// </summary>
    public IReadOnlyList<NarrativeMessage> GetTutorialGuide()
    {
        var messages = new List<NarrativeMessage>();

        AddIfAvailable(messages, "journal.status", "STATUS\nInspect the local runtime and player state with: status\nAvailable commands: status, money, skills", "player.inspect");
        AddIfAvailable(messages, "journal.host", "HOST CONTROLS\nUse help to see available commands. save and load manage your saves. exit leaves the game.\nAvailable commands: help, journal, save, load, saves, reset, clear, exit (quit)", "host.controls");
        AddIfAvailable(messages, "journal.scan", "NETWORK DISCOVERY\nUse scan to discover hosts connected to your current server.\nAvailable command: scan", "network.scan");
        AddIfAvailable(messages, "journal.analyze", "NETWORK ANALYSIS\nUse analyze <host> to inspect a discovered server before connecting.\nAvailable command: analyze <host>", "network.analyze");
        AddIfAvailable(messages, "journal.connect", "NETWORK CONNECTION\nUse connect <host> to move between discovered hosts. Use home to return to your home server.\nAvailable commands: connect <host>, home", "network.connect");
        AddIfAvailable(messages, "journal.root", "ACCESS\nUse nuke <host> when your hacking level is sufficient to gain root access.\nAvailable command: nuke <host>", "server.root-access");
        AddIfAvailable(messages, "journal.operations", "SERVER OPERATIONS\nOn a rooted server, hack, grow and weaken change its resources and security.\nAvailable commands: hack, grow, weaken", "server.operations");
        AddIfAvailable(messages, "journal.files", "FILES\nUse ls, cd and pwd to navigate. Use cat to read files. edit can create or change files.\nAvailable commands: ls, cd, pwd, cat, rm, mkdir, rmdir, mv, scp, edit (vscode if enabled)", "files.navigation");
        AddIfAvailable(messages, "journal.scripts", "AUTOMATION\nCreate a script with edit, then run <file> to start it. Use ps to inspect processes and kill to stop them.\nAvailable commands: run <file>, mem, free, ps, kill, killall", "scripts.execute");
        AddIfAvailable(messages, "journal.expansion", "EXPANSION\nYour operational profile now includes distributed resources and optional human systems. Use help to explore the commands that are currently available.\nAvailable commands: botnet, botnet buy, botnet upgrade, cloud, cloud buy, cloud upgrade, cloud rename, study, train, work, crime, stop, focus, activity", "botnet.inspect");

        return messages;
    }

    private void AddIfAvailable(List<NarrativeMessage> messages, string id, string text, string capability)
    {
        if (state.Narrative.HasCapability(capability))
            messages.Add(new NarrativeMessage(id, text));
    }

    public void StatusUsed()
    {
        Complete("system.host-controls", null, "host.controls", "network.scan", "player.profile");
    }

    private void OnServersDiscovered(IReadOnlyList<string> hostnames)
    {
        if (hostnames.Count == 0)
            return;

        Complete("system.network-discovered", new Dictionary<string, string>
        {
            ["hostnames"] = string.Join(", ", hostnames)
        }, "network.analyze");
    }

    private void OnServerAnalyzed(string hostname)
    {
        Complete("system.network-analyzed", new Dictionary<string, string> { ["hostname"] = hostname },
            "network.connect", "network.inspect");
    }

    private void OnServerConnected(string hostname)
    {
        Complete("system.network-connected", new Dictionary<string, string> { ["hostname"] = hostname },
            "server.root-access");
    }

    private void OnRootAccessGained(string hostname)
    {
        Complete("system.root-access", new Dictionary<string, string> { ["hostname"] = hostname },
            "server.operations", "server.backdoor");
    }

    public void ScriptStarted()
    {
        Complete("system.first-script", null,
            "scripts.inspect", "scripts.control", "botnet.inspect", "botnet.purchase", "botnet.upgrade",
            "cloud.inspect", "cloud.purchase", "cloud.manage", "activities.learn", "activities.work",
            "activities.crime", "activities.manage");
    }

    private void OnHackCompleted(string hostname, bool succeeded)
    {
        if (!succeeded)
            return;

        Complete("system.first-external-action", new Dictionary<string, string> { ["hostname"] = hostname },
            "files.navigation", "files.read", "files.manage", "scripts.execute");
    }

    private void Complete(string entryId, IReadOnlyDictionary<string, string>? values, params string[] capabilities)
    {
        if (state.Narrative.IsFreeplay)
            return;

        var entry = content.GetRequired(entryId);
        if (state.Narrative.CompletedMilestones.Contains(entry.MilestoneId))
            return;

        foreach (var capability in capabilities)
            state.Narrative.UnlockCapability(capability);

        state.Narrative.CompleteMilestone(entry.MilestoneId);
        if (!state.Narrative.DeliverMessage(entry.Id))
            return;

        var handlers = MessagePublished;
        if (handlers is null)
            return;

        var message = new NarrativeMessage(entry.Id, entry.Render(values));
        foreach (Action<NarrativeMessage> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(message);
            }
            catch (Exception exception)
            {
                System.Diagnostics.Trace.TraceError(
                    $"A narrative message observer failed for '{entry.Id}': {exception}");
            }
        }
    }
}
