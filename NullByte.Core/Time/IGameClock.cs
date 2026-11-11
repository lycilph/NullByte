namespace NullByte.Core.Time;

/// <summary>
/// Provides the current monotonic game time.
/// </summary>
public interface IGameClock
{
    /// <summary>
    /// Gets the amount of game time that has elapsed since the clock started.
    /// </summary>
    TimeSpan Elapsed { get; }

    void Start();
    void Stop();
}