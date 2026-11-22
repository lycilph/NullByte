using NullByte.Core.Configuration;
using NullByte.Core.Domain;
using NullByte.Core.Domain.Activities;
using NullByte.Core.Formulas;
using NullByte.Core.Services;
using NullByte.Core.Systems;
using NullByte.Core.World;

namespace NullByte.Core.Tests.Activities;

/// <summary>
/// A small, deterministic world for the activity tests: round numbers, so the expected values are easy to check by hand.
/// </summary>
internal sealed class ActivityTestWorld
{
    public ActivityTestWorld(BalanceConfig? balance = null)
    {
        Balance = balance ?? new BalanceConfig();
        State = InitialGameStateFactory.Create();          // the player starts with $1000
        Formulas = new ActivityFormulas(Balance);
        Service = new ActivityService(State, Catalog, Formulas);
        System = new ActivitySystem(State, Formulas, Service);
    }

    public BalanceConfig Balance { get; }
    public GameState State { get; }
    public ActivityFormulas Formulas { get; }
    public ActivityService Service { get; }
    public ActivitySystem System { get; }
    public Player Player => State.Player;

    /// <summary>
    /// Two universities (a plain and a fancy one), one gym, and three courses:
    /// "algorithms" costs $10/s for 2 hacking xp/s, "free-course" is free, "strength" costs $5/s.
    /// </summary>
    public static ActivityCatalog Catalog { get; } = new(
        facilities:
        [
            new TrainingFacility("plain-university", FacilityKind.University, ExperienceMultiplier: 1, CostMultiplier: 1),
            new TrainingFacility("fancy-university", FacilityKind.University, ExperienceMultiplier: 2, CostMultiplier: 3),
            new TrainingFacility("test-gym", FacilityKind.Gym, ExperienceMultiplier: 1, CostMultiplier: 1)
        ],
        courses:
        [
            new Course("algorithms", FacilityKind.University, CostPerSecond: 10, new Dictionary<SkillKind, double> { [SkillKind.Hacking] = 2 }),
            new Course("free-course", FacilityKind.University, CostPerSecond: 0, new Dictionary<SkillKind, double> { [SkillKind.Charisma] = 1 }),
            new Course("strength", FacilityKind.Gym, CostPerSecond: 5, new Dictionary<SkillKind, double> { [SkillKind.Strength] = 1 })
        ],
        companies: [],
        crimes: []);
}