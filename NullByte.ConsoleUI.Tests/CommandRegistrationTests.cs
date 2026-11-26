using NullByte.ConsoleUI.Tests.Infrastructure;

namespace NullByte.ConsoleUI.Tests;

public sealed class CommandRegistrationTests
{
    [Theory]
    [InlineData("botnet")]
    [InlineData("run")]
    public void Player_commands_are_registered_and_are_not_debug_commands(string name)
    {
        var registry = ConsoleTestSetup.CreateRegistry();

        Assert.True(registry.TryGet(name, out var command));
        Assert.NotNull(command);
        Assert.False(command!.Debug);
    }

    [Fact]
    public void Run_documents_its_arguments()
    {
        var registry = ConsoleTestSetup.CreateRegistry();

        Assert.True(registry.TryGet("run", out var command));
        Assert.Equal("run <filename> [-t <threads>] [args...]", command!.Usage);
    }
}
