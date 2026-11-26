using NullByte.ConsoleUI.Commands;
using NullByte.ConsoleUI.Commands.Groups;
using NullByte.Core.Configuration;
using NullByte.Core.Domain;
using NullByte.Core.Formulas;
using NullByte.Core.Services;
using NullByte.Core.Systems;
using NullByte.Core.World;

namespace NullByte.ConsoleUI.Tests.Infrastructure;

/// <summary>
/// The activity commands on a small catalog, without a game loop: the test moves time forward by hand
/// with <see cref="Advance"/> so that every outcome is deterministic.
/// </summary>
internal sealed class ActivityTestHarness
{
    private readonly BalanceConfig balance = new();
    private readonly ActivitySystem system;
    private readonly CommandDispatcher dispatcher;

    public ActivityTestHarness()
    {
        var formulas = new ActivityFormulas(balance);

        Service = new ActivityService(State, TestActivityCatalog.Default, formulas);
        system = new ActivitySystem(State, formulas, Service, Random);

        var registry = new CommandRegistry();
        new ActivityCommands(Service, State.Player, new SkillFormulas(balance), Terminal).Register(registry);
        dispatcher = new CommandDispatcher(registry, Terminal);
    }

    /// <summary>The player starts with $1000.</summary>
    public GameState State { get; } = InitialGameStateFactory.Create();

    public FakeTerminal Terminal { get; } = new();

    public ScriptedRandom Random { get; } = new();

    public ActivityService Service { get; }

    public Task<bool> RunAsync(string commandLine) => dispatcher.DispatchAsync(commandLine);

    /// <summary>Lets the running activity (if any) play for this much game time.</summary>
    public void Advance(TimeSpan time) => system.Update(time);

    public void GiveHackingLevel(int level)
        => State.Player.Skills.AddExperience(SkillKind.Hacking, new SkillFormulas(balance).CalculateExperienceForLevel(SkillKind.Hacking, level));
}
