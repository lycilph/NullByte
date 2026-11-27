using NullByte.Core.Domain;
using NullByte.Core.Services;

namespace NullByte.Core.Narrative;

/// <summary>
/// Converts successful gameplay events into one-time tutorial milestones.
/// All methods and subscribed events are expected to run on the game thread.
/// </summary>
public sealed class NarrativeCoordinator
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

    public void StatusUsed()
    {
        Complete(
            "tutorial.status.used",
            "system.host-controls",
            "Local environment verified.\n\nRuntime controls are now available.\nA network interface is present, but its connection table is incomplete.",
            "host.controls");
    }

    private void OnServersDiscovered(IReadOnlyList<string> hostnames)
    {
        if (hostnames.Count == 0)
            return;

        Complete(
            "tutorial.network.first-scan",
            "system.network-discovered",
            $"Two adjacent hosts detected.\n\nDiscovery confirmed: {string.Join(", ", hostnames)}.\nA remote system can now be inspected.",
            "network.analyze");
    }

    private void OnServerAnalyzed(string hostname)
    {
        Complete(
            "tutorial.network.first-analysis",
            "system.network-analyzed",
            $"Remote host {hostname} has been inspected.\nA connection can now be established.",
            "network.connect");
    }

    private void OnServerConnected(string hostname)
    {
        Complete(
            "tutorial.network.first-connection",
            "system.network-connected",
            $"Connection established: {hostname}.\n\nThe remote host is within reach. Administrative access may be possible.",
            "server.root-access");
    }

    private void OnRootAccessGained(string hostname)
    {
        Complete(
            "tutorial.network.first-root",
            "system.root-access",
            $"Administrative access acquired on {hostname}.\n\nYou have modified a system outside your assigned environment. Continue.",
            "server.operations");
    }

    private void OnHackCompleted(string hostname, bool succeeded)
    {
        if (!succeeded)
            return;

        Complete(
            "tutorial.network.first-hack",
            "system.first-external-action",
            $"External operation completed on {hostname}.\n\nA boundary has been crossed. The result is now part of your operational history.");
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
