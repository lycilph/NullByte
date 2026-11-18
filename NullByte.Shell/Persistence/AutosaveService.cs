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