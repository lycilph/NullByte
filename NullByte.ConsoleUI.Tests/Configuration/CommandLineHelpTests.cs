using NullByte.ConsoleUI.Configuration;

namespace NullByte.ConsoleUI.Tests.Configuration;

public sealed class CommandLineHelpTests
{
    [Theory]
    [InlineData("help")]
    [InlineData("--help")]
    public void IsRequested_WithHelpAlias_ReturnsTrue(string argument)
    {
        Assert.True(CommandLineParser.IsRequested([argument]));
    }

    [Fact]
    public void IsRequested_WithoutHelpAlias_ReturnsFalse()
    {
        Assert.False(CommandLineParser.IsRequested(["--new"]));
    }

    [Fact]
    public void IsRequested_WhenCombinedWithOtherArguments_ReturnsTrue()
    {
        Assert.True(CommandLineParser.IsRequested(["--new", "--help"]));
    }

    [Fact]
    public void GetUsageText_ListsEveryDefinedOptionAndDescription()
    {
        var help = CommandLineParser.GetUsageText();

        foreach (var option in CommandLineOptionDefinitions.All)
        {
            Assert.Contains(option.DisplayName, help);
            Assert.Contains(option.Description, help);

            if (option.ValueName is not null)
                Assert.Contains($"<{option.ValueName}>", help);
        }
    }
}
