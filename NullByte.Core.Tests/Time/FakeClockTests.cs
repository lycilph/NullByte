using NullByte.Core.Time;

namespace NullByte.Core.Tests.Time;

public class FakeGameClockTests
{
    [Fact]
    public void NewClock_StartsAtZero()
    {
        var clock = new FakeClock();

        Assert.Equal(TimeSpan.Zero, clock.Elapsed);
    }

    [Fact]
    public void Advance_IncreasesElapsedTime()
    {
        var clock = new FakeClock();

        clock.Advance(TimeSpan.FromSeconds(5));

        Assert.Equal(TimeSpan.FromSeconds(5), clock.Elapsed);
    }

    [Fact]
    public void MultipleAdvances_Accumulate()
    {
        var clock = new FakeClock();

        clock.Advance(TimeSpan.FromSeconds(5));
        clock.Advance(TimeSpan.FromSeconds(3));

        Assert.Equal(TimeSpan.FromSeconds(8), clock.Elapsed);
    }

    [Fact]
    public void Advance_WithZero_IsAllowed()
    {
        var clock = new FakeClock();

        clock.Advance(TimeSpan.Zero);

        Assert.Equal(TimeSpan.Zero, clock.Elapsed);
    }

    [Fact]
    public void Advance_WithNegativeDuration_Throws()
    {
        var clock = new FakeClock();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            clock.Advance(TimeSpan.FromSeconds(-1)));
    }
}
