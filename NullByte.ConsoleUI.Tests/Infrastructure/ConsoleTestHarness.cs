using NullByte.ConsoleUI.Commands;
using NullByte.Core.Domain;
using NullByte.Shell;

namespace NullByte.ConsoleUI.Tests.Infrastructure;

/// <summary>
/// A game session with every console command registered and a fake terminal, ready to run command lines.
/// </summary>
internal sealed class ConsoleTestHarness
{
    private readonly CommandDispatcher dispatcher;

    public ConsoleTestHarness(GameSession session, CommandRegistry registry, FakeTerminal terminal)
    {
        Session = session;
        Registry = registry;
        Terminal = terminal;
        dispatcher = new CommandDispatcher(registry, terminal, session.Narrative);
        session.Narrative.Initialize();
    }

    public GameSession Session { get; }

    public GameState State => Session.State;

    public VirtualFileSystem Files => State.FileSystem;

    public CommandRegistry Registry { get; }

    public FakeTerminal Terminal { get; }

    /// <summary>Everything written to the terminal so far.</summary>
    public string Output => Terminal.Text;

    public Task RunAsync(string commandLine) => dispatcher.DispatchAsync(commandLine);

    /// <summary>Runs a command line and returns only the lines that it printed.</summary>
    public async Task<IReadOnlyList<string>> RunAndCaptureAsync(string commandLine)
    {
        Terminal.Clear();
        await dispatcher.DispatchAsync(commandLine);
        return Terminal.Lines;
    }
}
