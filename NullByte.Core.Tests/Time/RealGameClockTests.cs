using NullByte.Core.Time;

namespace NullByte.Core.Tests.Time;

public sealed class RealGameClockTests
{
    [Fact]
    public void NewClock_IsStoppedAtZero()
    {
        var clock = new RealGameClock();

        Assert.False(clock.IsRunning);
        Assert.Equal(TimeSpan.Zero, clock.Elapsed);
    }

    [Fact]
    public void Start_AdvancesElapsedTime()
    {
        var clock = new RealGameClock();

        clock.Start();

        Assert.True(SpinWait.SpinUntil(() => clock.Elapsed > TimeSpan.Zero, TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void Stop_FreezesElapsedTimeAndKeepsIt()
    {
        var clock = new RealGameClock();

        clock.Start();
        Assert.True(SpinWait.SpinUntil(() => clock.Elapsed > TimeSpan.Zero, TimeSpan.FromSeconds(5)));

        clock.Stop();
        var stoppedAt = clock.Elapsed;

        Thread.Sleep(30);

        Assert.False(clock.IsRunning);
        Assert.True(stoppedAt > TimeSpan.Zero);
        Assert.Equal(stoppedAt, clock.Elapsed);
    }

    [Fact]
    public void SpeedMultiplier_ChangedWhileRunning_KeepsClockRunning()
    {
        var clock = new RealGameClock();

        clock.Start();
        Assert.True(SpinWait.SpinUntil(() => clock.Elapsed > TimeSpan.Zero, TimeSpan.FromSeconds(5)));

        clock.SpeedMultiplier = 2.0;

        var afterChange = clock.Elapsed;

        Assert.True(clock.IsRunning);
        Assert.True(SpinWait.SpinUntil(() => clock.Elapsed > afterChange, TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void SpeedMultiplier_Zero_PausesTimeWithoutStoppingTheClock()
    {
        var clock = new RealGameClock();

        clock.Start();
        clock.SpeedMultiplier = 0;

        var pausedAt = clock.Elapsed;

        Thread.Sleep(30);

        Assert.True(clock.IsRunning);
        Assert.Equal(pausedAt, clock.Elapsed);

        clock.SpeedMultiplier = 1;

        Assert.True(SpinWait.SpinUntil(() => clock.Elapsed > pausedAt, TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void SpeedMultiplier_AppliesToTimeThatPassesAfterTheChange()
    {
        var clock = new RealGameClock { SpeedMultiplier = 0 };

        clock.Start();
        clock.SpeedMultiplier = 1000;

        Assert.True(SpinWait.SpinUntil(() => clock.Elapsed >= TimeSpan.FromMilliseconds(50), TimeSpan.FromSeconds(5)));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void SpeedMultiplier_RejectsInvalidValues(double value)
    {
        var clock = new RealGameClock();

        Assert.Throws<ArgumentOutOfRangeException>(() => clock.SpeedMultiplier = value);
    }

    [Fact]
    public void Reset_StopsAndClearsElapsedTime()
    {
        var clock = new RealGameClock();

        clock.Start();
        Assert.True(SpinWait.SpinUntil(() => clock.Elapsed > TimeSpan.Zero, TimeSpan.FromSeconds(5)));

        clock.Reset();

        Assert.False(clock.IsRunning);
        Assert.Equal(TimeSpan.Zero, clock.Elapsed);
    }
}