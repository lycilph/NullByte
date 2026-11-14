using NullByte.Core.Domain;
using NullByte.Core.Services;
using NullByte.Core.Time;

namespace NullByte.Core.Scripting;

/// <summary>
/// Provides the API exposed to player-written scripts through the global
/// <c>nb</c> object.
/// </summary>
public sealed class NullByteApi : INullByteApi
{
    private readonly GameState gameState;
    private readonly GameScheduler scheduler;
    private readonly ServerActions serverActions;
    private readonly IGameOutput output;

    private readonly int threads;
    private readonly CancellationToken cancellationToken;

    public NullByteApi(
        GameState gameState,
        GameScheduler scheduler,
        ServerActions serverActions,
        IGameOutput output,
        int threads = 1,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(serverActions);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentOutOfRangeException.ThrowIfLessThan(threads, 1);

        this.gameState = gameState;
        this.scheduler = scheduler;
        this.serverActions = serverActions;
        this.output = output;
        this.cancellationToken = cancellationToken;
        this.threads = threads;
    }

    public void Print(object? value)
    {
        output.WriteLine(value?.ToString() ?? string.Empty);
    }

    public Task Sleep(TimeSpan duration)
    {
        return scheduler.DelayAsync(duration, cancellationToken);
    }

    public Task<HackResult> Hack(string hostname)
    {
        var server = GetServer(hostname);

        return serverActions.HackAsync(server.Hostname, threads, cancellationToken);
    }

    public Task<GrowResult> Grow(string hostname)
    {
        var server = GetServer(hostname);

        return serverActions.GrowAsync(server.Hostname, threads, cancellationToken);
    }

    public Task<WeakenResult> Weaken(string hostname)
    {
        var server = GetServer(hostname);

        return serverActions.WeakenAsync(server.Hostname, threads, cancellationToken);
    }

    public double GetServerMoneyAvailable(string hostname)
    {
        return GetServer(hostname).Money;
    }

    public double GetServerMaxMoney(string hostname)
    {
        return GetServer(hostname).MaximumMoney;
    }

    public double GetServerSecurityLevel(string hostname)
    {
        return GetServer(hostname).Security;
    }

    public double GetServerMinSecurityLevel(string hostname)
    {
        return GetServer(hostname).MinimumSecurity;
    }

    private Server GetServer(string hostname)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostname);

        if (!gameState.Network.TryGetServer(hostname, out var server))
            throw new InvalidOperationException($"Server '{hostname}' does not exist.");

        return server!;
    }
}