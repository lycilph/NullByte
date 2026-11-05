using System.Diagnostics;

namespace NullByte.Core.Time;

/// <summary>
/// A monotonic game clock backed by <see cref="Stopwatch"/>.
/// </summary>
public sealed class RealGameClock : IGameClock
{
    private readonly Stopwatch stopwatch = new();

    /// <inheritdoc />
    public TimeSpan Elapsed => stopwatch.Elapsed;

    /// <summary>
    /// Gets whether the clock is currently running.
    /// </summary>
    public bool IsRunning => stopwatch.IsRunning;

    /// <summary>
    /// Starts or resumes the clock.
    /// </summary>
    public void Start() => stopwatch.Start();

    /// <summary>
    /// Stops the clock without resetting its elapsed time.
    /// </summary>
    public void Stop() => stopwatch.Stop();

    /// <summary>
    /// Stops the clock and resets its elapsed time to zero.
    /// </summary>
    public void Reset() => stopwatch.Reset();
}