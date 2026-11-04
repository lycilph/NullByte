namespace NullByte.Core.Time;

public sealed class FakeClock : IGameClock
{
    public TimeSpan Elapsed { get; private set; }

    public void Advance(TimeSpan amount)
    {
        if (amount < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Cannot advance game time by a negative duration.");
        }

        Elapsed += amount;
    }
}
