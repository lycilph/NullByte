using NullByte.Core.Domain;
using NullByte.Core.Domain.Activities;
using NullByte.Core.Formulas;
using NullByte.Core.Randomness;
using NullByte.Core.Services;
using NullByte.Core.Simulation;

namespace NullByte.Core.Systems;

/// <summary>
/// Advances the player's current activity as game time passes: it pays for it,
/// and gives the player what it earns.
/// </summary>
/// <remarks>
/// <para>
/// Runs on the game thread, driven by <see cref="GameLoop"/>. The rates are
/// constant for the length of one update, so switching between focused and
/// background takes effect from the next update on.
/// </para>
/// <para>
/// Crimes are different: they pay in lumps. Time builds up towards the end of an
/// attempt, and then the dice are rolled. Skills may change within one update,
/// so every attempt is rolled with the player's skills as they are by then.
/// </para>
/// </remarks>
public sealed class ActivitySystem : ISimulationSystem
{
    private readonly GameState gameState;
    private readonly ActivityFormulas formulas;
    private readonly ActivityService service;
    private readonly IRandomSource random;

    public ActivitySystem(GameState gameState, ActivityFormulas formulas, ActivityService service, IRandomSource random)
    {
        this.gameState = gameState
            ?? throw new ArgumentNullException(nameof(gameState));

        this.formulas = formulas
            ?? throw new ArgumentNullException(nameof(formulas));

        this.service = service
            ?? throw new ArgumentNullException(nameof(service));

        this.random = random
            ?? throw new ArgumentNullException(nameof(random));
    }

    /// <inheritdoc />
    public void Update(TimeSpan delta)
    {
        if (delta <= TimeSpan.Zero)
            return;

        var activity = gameState.Activity.Current;

        if (activity is null)
            return;

        if (activity is CrimeActivity crime)
        {
            AdvanceCrime(crime, delta);
            return;
        }

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

    private void AdvanceCrime(CrimeActivity crime, TimeSpan delta)
    {
        var definition = crime.Crime;
        var player = gameState.Player;
        var seconds = delta.TotalSeconds;

        var cycle = crime.CycleSeconds + seconds;
        var money = 0.0;
        var attempts = 0;
        var successes = 0;
        var experience = new Dictionary<SkillKind, double>();
        var results = new List<CrimeAttempt>();
        var completed = false;

        while (cycle >= definition.DurationSeconds)
        {
            cycle -= definition.DurationSeconds;

            var chance = formulas.CalculateCrimeSuccessChance(definition, player.Skills);
            var succeeded = random.NextDouble() < chance;
            var rewards = formulas.CalculateCrimeRewards(definition, crime.Mode, succeeded);

            if (rewards.Money > 0)
                player.AddMoney(rewards.Money);

            foreach (var (skill, amount) in rewards.Experience)
            {
                player.Skills.AddExperience(skill, amount);
                experience[skill] = experience.GetValueOrDefault(skill) + amount;
            }

            money += rewards.Money;
            attempts++;

            if (succeeded)
                successes++;

            results.Add(new CrimeAttempt(crime, succeeded, chance, rewards));

            // One attempt only: the rest of this update's time is not spent.
            if (!crime.Repeat)
            {
                completed = true;
                break;
            }
        }

        var elapsed = completed ? seconds - cycle : seconds;

        crime.SetCycle(completed ? 0 : cycle);
        crime.RecordProgress(TimeSpan.FromSeconds(elapsed), money, experience, attempts: attempts, successes: successes);

        // Announce only now, so that whoever listens sees totals that are up to date.
        foreach (var result in results)
            service.RaiseCrimeAttempted(result);

        if (completed)
            service.End(ActivityEndReason.Completed);
    }
}