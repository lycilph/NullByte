using NullByte.Core.Time;

namespace NullByte.Core.Tests.Time;

public class GameClockTests
{
    [Fact]
    public void NewClock_StartsAtApproximatelyZero()
    {
        var clock = new GameClock();

        Assert.True(clock.Elapsed >= TimeSpan.Zero);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ElapsedTime_IsMonotonicallyIncreasing()
    {
        var clock = new GameClock();

        TimeSpan first = clock.Elapsed;

        Thread.Sleep(20);

        TimeSpan second = clock.Elapsed;

        Assert.True(second >= first);
    }

    [Fact]
    public void TimeMultiplier_ScalesElapsedTime()
    {
        var clock = new GameClock(timeMultiplier: 2.0);

        Thread.Sleep(100);

        Assert.True(
            clock.Elapsed >= TimeSpan.FromMilliseconds(150));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidTimeMultiplier_Throws(double multiplier)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GameClock(multiplier));
    }
}
