using NullByte.ConsoleUI.Commands;
using NullByte.Core.Configuration;
using NullByte.Core.Domain;
using NullByte.Core.Narrative;
using NullByte.Core.Scripting;
using NullByte.Core.Time;
using NullByte.Shell;

namespace NullByte.ConsoleUI.Tests.Infrastructure;

/// <summary>What a test can choose when it creates a console harness; everything else gets a deterministic default.</summary>
internal sealed record ConsoleTestOptions
{
    /// <summary>The game to play; by default a new one with only the home server.</summary>
    public GameState? State { get; init; }

    /// <summary>The player's money when no <see cref="State"/> is given.</summary>
    public double Money { get; init; }

    public BalanceConfig? Balance { get; init; }

    /// <summary>Replaces the script API that scripts get; by default the real one.</summary>
    public INullByteApiFactory? ApiFactory { get; init; }

    public FakeTerminal? Terminal { get; init; }

    public NarrativeProgressionMode ProgressionMode { get; init; } = NarrativeProgressionMode.Freeplay;

    public NarrativeContentCatalog? NarrativeDiscoveries { get; init; }
}

/// <summary>
/// The one place where console tests build their game: a real session on a fake clock, driven through the real
/// <see cref="CommandSetup"/> so that tests see the same commands as the player.
/// </summary>
internal static class ConsoleTestSetup
{
    public static ConsoleTestHarness CreateHarness(ConsoleTestOptions? options = null)
    {
        options ??= new ConsoleTestOptions();

        var terminal = options.Terminal ?? new FakeTerminal();
        var state = options.State ?? TestGameFactory.CreateGame(options.Money);
        state.Narrative.Mode = options.ProgressionMode;

        var session = GameSession.Create(new GameSessionOptions
        {
            Output = terminal,
            State = state,
            Balance = options.Balance ?? new BalanceConfig(),
            Clock = new FakeGameClock(),
            ApiFactory = options.ApiFactory,
            NarrativeDiscoveries = options.NarrativeDiscoveries
        });

        var registry = CommandSetup.Create(session, terminal, new HostRequestMailbox());

        return new ConsoleTestHarness(session, registry, terminal);
    }

    /// <summary>The registry of a default game, for tests that only need to know which commands exist.</summary>
    public static CommandRegistry CreateRegistry() => CreateHarness().Registry;
}
