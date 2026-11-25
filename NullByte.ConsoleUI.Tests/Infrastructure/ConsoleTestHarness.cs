using NullByte.ConsoleUI.Commands;
using NullByte.Shell;

namespace NullByte.ConsoleUI.Tests.Infrastructure;

internal sealed class ConsoleTestHarness
{
    private readonly CommandDispatcher dispatcher;

    public ConsoleTestHarness(GameSession session, CommandRegistry registry, FakeTerminal terminal)
    {
        Session = session;
        Terminal = terminal;
        dispatcher = new CommandDispatcher(registry, terminal);
    }

    public GameSession Session { get; }

    public FakeTerminal Terminal { get; }

    public string Output => string.Join(Environment.NewLine, Terminal.Lines);

    public Task RunAsync(string commandLine) => dispatcher.DispatchAsync(commandLine);
}
