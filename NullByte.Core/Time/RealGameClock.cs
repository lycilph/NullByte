using System.Diagnostics;

namespace NullByte.Core.Time;

/// <summary>
/// A monotonic game clock backed by a real-time Stopwatch.
/// </summary>
public sealed class RealGameClock : IGameClock
{
    private readonly Stopwatch stopwatch = new();

    private TimeSpan accumulatedGameTime;

    private double speedMultiplier = 1.0;

    /// <summary>
    /// Gets the amount of game time that has elapsed.
    /// </summary>
    public TimeSpan Elapsed
    {
        get
        {
            if (!stopwatch.IsRunning)
                return accumulatedGameTime;

            return accumulatedGameTime +
                   Scale(
                       stopwatch.Elapsed,
                       speedMultiplier);
        }
    }

    /// <summary>Gets whether the clock is currently running.</summary>
    public bool IsRunning => stopwatch.IsRunning;

    /// <summary>
    /// Gets or sets the rate at which game time advances
    /// relative to real time.
    /// </summary>
    public double SpeedMultiplier
    {
        get => speedMultiplier;

        set
        {
            if (!double.IsFinite(value) || value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    "The speed multiplier must be a finite, " +
                    "non-negative value.");
            }

            if (value == speedMultiplier)
                return;

            CommitElapsedTime();

            speedMultiplier = value;
        }
    }

    /// <summary>
    /// Starts or resumes the clock.
    /// </summary>
    public void Start()
    {
        if (stopwatch.IsRunning)
            return;

        stopwatch.Restart();
    }

    /// <summary>
    /// Stops the clock while preserving elapsed game time.
    /// </summary>
    public void Stop()
    {
        if (!stopwatch.IsRunning)
            return;

        CommitElapsedTime();
    }

    /// <summary>
    /// Stops the clock and resets elapsed game time to zero.
    /// </summary>
    public void Reset()
    {
        stopwatch.Reset();
        accumulatedGameTime = TimeSpan.Zero;
    }

    private void CommitElapsedTime()
    {
        if (!stopwatch.IsRunning)
            return;

        accumulatedGameTime +=
            Scale(
                stopwatch.Elapsed,
                speedMultiplier);

        stopwatch.Reset();
    }

    private static TimeSpan Scale(
        TimeSpan duration,
        double multiplier)
    {
        if (multiplier == 0)
            return TimeSpan.Zero;

        var scaledTicks = duration.Ticks * multiplier;

        if (scaledTicks >= TimeSpan.MaxValue.Ticks)
            return TimeSpan.MaxValue;

        return TimeSpan.FromTicks(
            (long)scaledTicks);
    }
}