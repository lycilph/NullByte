using NullByte.ConsoleUI.Terminal;

namespace NullByte.ConsoleUI.Tests;

public sealed class InputHistoryTests
{
    [Fact]
    public void Previous_WithNoHistory_ReturnsNull()
    {
        var history = new InputHistory();

        Assert.Null(history.Previous("typed"));
        Assert.False(history.IsBrowsing);
    }

    [Fact]
    public void Previous_WalksFromNewestToOldestAndStops()
    {
        var history = CreateHistory("scan", "connect n00dles", "hack");

        Assert.Equal("hack", history.Previous(string.Empty));
        Assert.Equal("connect n00dles", history.Previous(string.Empty));
        Assert.Equal("scan", history.Previous(string.Empty));
        Assert.Null(history.Previous(string.Empty));
    }

    [Fact]
    public void Next_WalksBackTowardsNewestThenRestoresDraft()
    {
        var history = CreateHistory("scan", "hack");

        history.Previous("half-typed");
        history.Previous("ignored, already browsing");

        Assert.Equal("hack", history.Next());
        Assert.Equal("half-typed", history.Next());
        Assert.Null(history.Next());
        Assert.False(history.IsBrowsing);
    }

    [Fact]
    public void Next_WhenNotBrowsing_ReturnsNull()
    {
        var history = CreateHistory("scan");

        Assert.Null(history.Next());
    }

    [Fact]
    public void Add_IgnoresBlankLines()
    {
        var history = CreateHistory("", "   ", "scan");

        Assert.Equal(1, history.Count);
    }

    [Fact]
    public void Add_IgnoresImmediateRepeatsButKeepsNonAdjacentOnes()
    {
        var history = CreateHistory("scan", "scan", "hack", "scan");

        Assert.Equal(3, history.Count);
        Assert.Equal("scan", history.Previous(string.Empty));
        Assert.Equal("hack", history.Previous(string.Empty));
        Assert.Equal("scan", history.Previous(string.Empty));
    }

    [Fact]
    public void Add_DropsOldestEntryWhenCapacityIsExceeded()
    {
        var history = new InputHistory(capacity: 2);

        history.Add("one");
        history.Add("two");
        history.Add("three");

        Assert.Equal(2, history.Count);
        Assert.Equal("three", history.Previous(string.Empty));
        Assert.Equal("two", history.Previous(string.Empty));
        Assert.Null(history.Previous(string.Empty));
    }

    [Fact]
    public void Add_EndsBrowsing()
    {
        var history = CreateHistory("scan", "hack");

        history.Previous(string.Empty);
        history.Add("money");

        Assert.False(history.IsBrowsing);
        Assert.Equal("money", history.Previous(string.Empty));
    }

    [Fact]
    public void ResetNavigation_StartsOverFromNewestEntry()
    {
        var history = CreateHistory("scan", "hack");

        history.Previous(string.Empty);
        history.Previous(string.Empty);
        history.ResetNavigation();

        Assert.Equal("hack", history.Previous(string.Empty));
    }

    [Fact]
    public void Constructor_RejectsNonPositiveCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new InputHistory(0));
    }

    private static InputHistory CreateHistory(params string[] entries)
    {
        var history = new InputHistory();

        foreach (var entry in entries)
            history.Add(entry);

        return history;
    }
}