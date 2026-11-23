using NullByte.ConsoleUI.Commands.Groups;

namespace NullByte.ConsoleUI.Tests.Commands;

public sealed class ActivityArgumentsTests
{
    [Theory]
    [InlineData("", null, null, false)]
    [InlineData("algorithms", "algorithms", null, false)]
    [InlineData("algorithms summit-university", "algorithms", "summit-university", false)]
    [InlineData("algorithms -b", "algorithms", null, true)]
    [InlineData("-b algorithms summit-university", "algorithms", "summit-university", true)]
    [InlineData("algorithms --background summit-university", "algorithms", "summit-university", true)]
    [InlineData("-b", null, null, true)]
    public void Names_and_the_background_flag_are_found_anywhere(string line, string? target, string? detail, bool background)
    {
        var arguments = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        Assert.True(ActivityArguments.TryParse(arguments, out var request, out _));
        Assert.Equal(target, request.Target);
        Assert.Equal(detail, request.Detail);
        Assert.Equal(background, request.Background);
    }

    [Theory]
    [InlineData("algorithms -x", "Unknown option '-x'")]
    [InlineData("a b c", "Too many arguments")]
    public void Bad_arguments_are_explained(string line, string expected)
    {
        var arguments = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        Assert.False(ActivityArguments.TryParse(arguments, out _, out var error));
        Assert.Contains(expected, error);
    }
}