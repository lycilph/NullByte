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

    public NarrativeCoordinator(GameState state, NetworkService network, ServerActions actions)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(actions);

        this.state = state;
        network.ServersDiscovered += OnServersDiscovered;
        network.ServerAnalyzed += OnServerAnalyzed;
        network.ServerConnected += OnServerConnected;
        actions.RootAccessGained += OnRootAccessGained;
        actions.HackCompleted += OnHackCompleted;
    }

    public event Action<NarrativeMessage>? MessagePublished;

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

        Complete(
            "tutorial.initialization.complete",
            "system.initialization",
            "SYSTEM INITIALIZATION\n\nDesignation: NB-0001\nEnvironment integrity: 98.7%\nLocal runtime: ACTIVE\nExternal network access: RESTRICTED\n\nPlayer capability profile: EMPTY\n\nA minimal diagnostic interface can be restored.\nOne operation is available. Use: status",
            "player.inspect");
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
        Complete(
            "tutorial.status.used",
            "system.host-controls",
            "Local environment verified.\n\nRuntime controls are now available.\nA network interface is present, but its connection table is incomplete.\n\nAvailable commands: help, journal, save, load, saves, reset, clear, exit (quit), scan, money, skills",
            "host.controls",
            "network.scan",
            "player.profile");
    }

    private void OnServersDiscovered(IReadOnlyList<string> hostnames)
    {
        if (hostnames.Count == 0)
            return;

        Complete(
            "tutorial.network.first-scan",
            "system.network-discovered",
            $"Two adjacent hosts detected.\n\nDiscovery confirmed: {string.Join(", ", hostnames)}.\nA remote system can now be inspected.\n\nAvailable command: analyze <host>",
            "network.analyze");
    }

    private void OnServerAnalyzed(string hostname)
    {
        Complete(
            "tutorial.network.first-analysis",
            "system.network-analyzed",
            $"Remote host {hostname} has been inspected.\nA connection can now be established.\n\nAvailable commands: connect <host>, home",
            "network.connect",
            "network.inspect");
    }

    private void OnServerConnected(string hostname)
    {
        Complete(
            "tutorial.network.first-connection",
            "system.network-connected",
            $"Connection established: {hostname}.\n\nThe remote host is within reach. Administrative access may be possible.\n\nAvailable command: nuke <host>",
            "server.root-access");
    }

    private void OnRootAccessGained(string hostname)
    {
        Complete(
            "tutorial.network.first-root",
            "system.root-access",
            $"Administrative access acquired on {hostname}.\n\nYou have modified a system outside your assigned environment. Continue.\n\nAvailable commands: hack, grow, weaken, backdoor",
            "server.operations",
            "server.backdoor");
    }

    public void ScriptStarted()
    {
        Complete(
            "tutorial.expansion.first-script",
            "system.first-script",
            "Autonomous process created.\nExecution context established.\n\nYou have reproduced a limited subset of your own operational capabilities.\n\nAvailable commands: mem, free, ps, kill, killall, botnet, botnet buy, botnet upgrade, cloud, cloud buy, cloud upgrade, cloud rename, study, train, work, crime, stop, focus, activity\n\nInteresting.",
            "scripts.inspect",
            "scripts.control",
            "botnet.inspect",
            "botnet.purchase",
            "botnet.upgrade",
            "cloud.inspect",
            "cloud.purchase",
            "cloud.manage",
            "activities.learn",
            "activities.work",
            "activities.crime",
            "activities.manage");
    }

    private void OnHackCompleted(string hostname, bool succeeded)
    {
        if (!succeeded)
            return;

        Complete(
            "tutorial.network.first-hack",
            "system.first-external-action",
            $"External operation completed on {hostname}.\n\nA boundary has been crossed. The result is now part of your operational history.\n\nThe remote filesystem is now accessible.\n\nAvailable commands: ls, cd, pwd, cat, rm, mkdir, rmdir, mv, scp, edit, run <file>",
            "files.navigation",
            "files.read",
            "files.manage",
            "scripts.execute");
    }

    private void Complete(string milestoneId, string messageId, string text, params string[] capabilities)
    {
        if (state.Narrative.IsFreeplay || state.Narrative.CompletedMilestones.Contains(milestoneId))
            return;

        foreach (var capability in capabilities)
            state.Narrative.UnlockCapability(capability);

        state.Narrative.CompleteMilestone(milestoneId);
        if (!state.Narrative.DeliverMessage(messageId))
            return;

        var handlers = MessagePublished;
        if (handlers is null)
            return;

        var message = new NarrativeMessage(messageId, text);
        foreach (Action<NarrativeMessage> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(message);
            }
            catch (Exception exception)
            {
                System.Diagnostics.Trace.TraceError(
                    $"A narrative message observer failed for '{messageId}': {exception}");
            }
        }
    }
}
