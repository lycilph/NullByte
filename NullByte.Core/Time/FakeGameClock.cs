namespace NullByte.Core.Time;

/// <summary>
/// A manually controlled game clock intended for deterministic tests.
/// </summary>
public sealed class FakeGameClock : IGameClock
{
    public FakeGameClock()
        : this(TimeSpan.Zero)
    {
    }

    public FakeGameClock(TimeSpan initialTime)
    {
        if (initialTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialTime),
                "Initial time cannot be negative.");
        }

        Elapsed = initialTime;
    }

    /// <inheritdoc />
    public TimeSpan Elapsed { get; private set; }

    /// <summary>
    /// Advances the clock by the specified amount.
    /// </summary>
    public void Advance(TimeSpan amount)
    {
        if (amount < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "The clock cannot move backwards.");
        }

        Elapsed += amount;
    }
}