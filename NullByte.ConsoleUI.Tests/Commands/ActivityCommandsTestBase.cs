using NullByte.ConsoleUI.Tests.Infrastructure;

namespace NullByte.ConsoleUI.Tests.Commands;

/// <summary>
/// What the activity command tests share: a game on a small catalog that only moves when the test says so
/// (see <see cref="ActivityTestHarness"/>), with the invariant culture so that "$1,234.50" reads the same everywhere.
/// </summary>
public abstract class ActivityCommandsTestBase : IDisposable
{
    private readonly InvariantCulture culture = new();
    private readonly ActivityTestHarness game = new();

    private protected static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private protected static readonly TimeSpan TenSeconds = TimeSpan.FromSeconds(10);

    private protected FakeTerminal Terminal => game.Terminal;
    private protected ScriptedRandom Random => game.Random;
    private protected NullByte.Core.Domain.GameState State => game.State;
    private protected NullByte.Core.Services.ActivityService Service => game.Service;

    private protected Task<bool> Run(string commandLine) => game.RunAsync(commandLine);
    private protected void Advance(TimeSpan time) => game.Advance(time);
    private protected void GiveHackingLevel(int level) => game.GiveHackingLevel(level);

    public void Dispose() => culture.Dispose();
}
