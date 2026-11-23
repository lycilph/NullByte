using NullByte.Core.Domain;
using NullByte.Core.Domain.Activities;
using NullByte.Core.Formulas;
using NullByte.Core.Services;
using NullByte.Core.Simulation;

namespace NullByte.Core.Systems;

/// <summary>
/// Advances the player's current activity as game time passes: it pays for it,
/// and gives the player what it earns.
/// </summary>
/// <remarks>
/// Runs on the game thread, driven by <see cref="GameLoop"/>. The rates are
/// constant for the length of one update, so switching between focused and
/// background takes effect from the next update on.
/// </remarks>
public sealed class ActivitySystem : ISimulationSystem
{
    private readonly GameState gameState;
    private readonly ActivityFormulas formulas;
    private readonly ActivityService service;

    public ActivitySystem(GameState gameState, ActivityFormulas formulas, ActivityService service)
    {
        this.gameState = gameState
            ?? throw new ArgumentNullException(nameof(gameState));

        this.formulas = formulas
            ?? throw new ArgumentNullException(nameof(formulas));

        this.service = service
            ?? throw new ArgumentNullException(nameof(service));
    }

    /// <inheritdoc />
    public void Update(TimeSpan delta)
    {
        if (delta <= TimeSpan.Zero)
            return;

        var activity = gameState.Activity.Current;

        if (activity is null)
            return;

        var rates = formulas.CalculateRates(activity);
        var seconds = delta.TotalSeconds;
        var player = gameState.Player;

        var money = rates.MoneyPerSecond * seconds;

        // Pay first: an activity the player cannot afford ends without giving anything for this update.
        if (money < 0 && !player.TrySpendMoney(-money))
        {
            service.End(ActivityEndReason.CannotAfford);
            return;
        }

        if (money > 0)
            player.AddMoney(money);

        var experience = rates.ExperiencePerSecond.ToDictionary(
            skill => skill.Key,
            skill => skill.Value * seconds);

        foreach (var (skill, amount) in experience)
            player.Skills.AddExperience(skill, amount);

        // Reputation is earned with the company the player works for.
        var reputation = rates.ReputationPerSecond * seconds;

        if (reputation > 0 && activity is JobActivity working)
            gameState.Employment.AddReputation(working.Company.Name, reputation);

        activity.RecordProgress(delta, money, experience, reputation);

        // The new experience and reputation may be enough for the next rank.
        if (activity is JobActivity job)
            service.PromoteIfQualified(job);
    }
}