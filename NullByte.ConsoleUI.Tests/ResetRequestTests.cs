namespace NullByte.ConsoleUI.Tests;

public sealed class ResetRequestTests
{
    [Fact]
    public void Take_WhenNotRequested_ReturnsFalse()
    {
        var sut = new ResetRequest();

        var result = sut.Take();

        Assert.False(result);
    }

    [Fact]
    public void Take_AfterRequest_ReturnsTrue()
    {
        var sut = new ResetRequest();
        sut.Request();

        var result = sut.Take();

        Assert.True(result);
    }

    [Fact]
    public void Take_AfterRequest_ConsumesRequest()
    {
        var sut = new ResetRequest();
        sut.Request();

        var first = sut.Take();
        var second = sut.Take();

        Assert.True(first);
        Assert.False(second);
    }
}