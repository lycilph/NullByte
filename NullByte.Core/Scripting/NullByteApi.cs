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
    private readonly GameState _gameState;
    private readonly GameScheduler _scheduler;
    private readonly ServerActions _serverActions;
    private readonly IScriptOutput _output;

    public NullByteApi(
        GameState gameState,
        GameScheduler scheduler,
        ServerActions serverActions,
        IScriptOutput output)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(serverActions);
        ArgumentNullException.ThrowIfNull(output);

        _gameState = gameState;
        _scheduler = scheduler;
        _serverActions = serverActions;
        _output = output;
    }

    public void Print(object? value)
    {
        _output.WriteLine(value?.ToString() ?? string.Empty);
    }

    public Task Sleep(TimeSpan duration, CancellationToken cancellationToken = default)
    {
        return _scheduler.DelayAsync(duration, cancellationToken);
    }

    public Task<HackResult> Hack(string hostname, CancellationToken cancellationToken = default)
    {
        var server = GetServer(hostname);

        return _serverActions.HackAsync(
            server.Id,
            threads: 1,
            cancellationToken);
    }

    public Task<GrowResult> Grow(string hostname, CancellationToken cancellationToken = default)
    {
        var server = GetServer(hostname);

        return _serverActions.GrowAsync(
            server.Id,
            threads: 1,
            cancellationToken);
    }

    public Task<WeakenResult> Weaken(string hostname, CancellationToken cancellationToken = default)
    {
        var server = GetServer(hostname);

        return _serverActions.WeakenAsync(
            server.Id,
            threads: 1,
            cancellationToken);
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

        if (!_gameState.Network.TryFindServer(hostname, out var server))
            throw new InvalidOperationException($"Server '{hostname}' does not exist.");

        return server!;
    }
}