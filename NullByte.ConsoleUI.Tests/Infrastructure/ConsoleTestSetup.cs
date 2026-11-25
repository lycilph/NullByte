using NullByte.ConsoleUI.Commands;
using NullByte.Core.Configuration;
using NullByte.Core.Domain;
using NullByte.Core.Scripting;
using NullByte.Shell;

namespace NullByte.ConsoleUI.Tests.Infrastructure;

internal static class ConsoleTestSetup
{
    public static (GameSession Session, CommandRegistry Registry, FakeTerminal Terminal) CreateSession(
        GameState? state = null,
        BalanceConfig? balance = null,
        INullByteApiFactory? apiFactory = null,
        FakeTerminal? terminal = null)
    {
        terminal ??= new FakeTerminal();
        var session = GameSession.Create(new GameSessionOptions
        {
            Balance = balance ?? new BalanceConfig(),
            State = state,
            ApiFactory = apiFactory,
            Output = terminal
        });

        var hostRequests = new HostRequestMailbox();
        var registry = CommandSetup.Create(session, terminal, hostRequests);

        return (session, registry, terminal);
    }

    public static ConsoleTestHarness CreateHarness(
        GameState? state = null,
        double money = 0,
        BalanceConfig? balance = null,
        INullByteApiFactory? apiFactory = null,
        FakeTerminal? terminal = null)
    {
        state ??= TestGameFactory.CreateGame(money);

        var setup = CreateSession(state, balance, apiFactory, terminal);
        return new ConsoleTestHarness(setup.Session, setup.Registry, setup.Terminal);
    }
}
