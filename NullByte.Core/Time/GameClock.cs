using System.Diagnostics;

namespace NullByte.Core.Time;

public sealed class GameClock : IGameClock
{
    private readonly Stopwatch stopwatch;
    private readonly double timeMultiplier;

    public GameClock(double timeMultiplier = 1.0)
    {
        if (!double.IsFinite(timeMultiplier) || timeMultiplier <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeMultiplier),
                "Time multiplier must be a finite positive number.");
        }

        this.timeMultiplier = timeMultiplier;
        stopwatch = Stopwatch.StartNew();
    }

    public TimeSpan Elapsed =>
        TimeSpan.FromSeconds(
            stopwatch.Elapsed.TotalSeconds * timeMultiplier);
}
