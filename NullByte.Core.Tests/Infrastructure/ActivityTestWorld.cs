using NullByte.Core.Configuration;
using NullByte.Core.Domain;
using NullByte.Core.Domain.Activities;
using NullByte.Core.Formulas;
using NullByte.Core.Randomness;
using NullByte.Core.Services;
using NullByte.Core.Systems;
using NullByte.Core.World;

namespace NullByte.Core.Tests.Infrastructure;

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
        Random = new ScriptedRandom();
        System = new ActivitySystem(State, Formulas, Service, Random);
    }

    public BalanceConfig Balance { get; }
    public GameState State { get; }
    public ActivityFormulas Formulas { get; }
    public ActivityService Service { get; }
    public ActivitySystem System { get; }

    /// <summary>Decides every crime roll. By default every attempt with a chance above zero succeeds.</summary>
    public ScriptedRandom Random { get; }
    public Player Player => State.Player;

    /// <summary>Gives the player enough hacking experience to be at least this hacking level.</summary>
    public void GiveHackingLevel(int level)
    {
        var needed = new SkillFormulas(Balance).CalculateExperienceForLevel(SkillKind.Hacking, level);

        Player.Skills.AddExperience(SkillKind.Hacking, Math.Max(0, needed - Player.Skills.GetExperience(SkillKind.Hacking)));
    }

    /// <summary>
    /// Two universities (a plain and a fancy one), one gym, and three courses:
    /// "algorithms" costs $10/s for 2 hacking xp/s, "free-course" is free, "strength" costs $5/s.
    /// There are also two companies to work for and four crimes (see below).
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
        companies:
        [
            // testcorp: software has three ranks (Intern, Developer needs reputation 10 and hacking level 3,
            // Lead needs reputation 100 and hacking level 10); security has one. hardcorp's only rank needs hacking level 5.
            new Company("testcorp",
            [
                new CareerPath("software",
                [
                    Rank("Intern", salary: 4, reputation: 1, requiredReputation: 0, hackingLevel: 0, hackingXp: 0.5),
                    Rank("Developer", salary: 10, reputation: 2, requiredReputation: 10, hackingLevel: 3, hackingXp: 1),
                    Rank("Lead", salary: 30, reputation: 5, requiredReputation: 100, hackingLevel: 10, hackingXp: 2)
                ]),
                new CareerPath("security",
                [
                    new JobRank("Trainee", 3, 1, 0, new Dictionary<SkillKind, int>(), new Dictionary<SkillKind, double> { [SkillKind.Strength] = 0.5 })
                ])
            ]),
            new Company("hardcorp",
            [
                new CareerPath("software",
                [
                    Rank("Hard Intern", salary: 8, reputation: 1, requiredReputation: 0, hackingLevel: 5, hackingXp: 1)
                ])
            ])
        ],
        crimes:
        [
            // chance = weighted average skill level / (difficulty * 100), so at level 1:
            // sure-thing is certain, coin-flip is 50%, long-shot is 1%. mixed counts strength three times as much as agility.
            new Crime("sure-thing", 2, 100, 0.01, Amounts((SkillKind.Strength, 4), (SkillKind.Agility, 2)), Amounts((SkillKind.Strength, 1))),
            new Crime("coin-flip", 4, 500, 0.02, Amounts((SkillKind.Dexterity, 8)), Amounts((SkillKind.Dexterity, 1))),
            new Crime("long-shot", 10, 1000, 1.0, Amounts((SkillKind.Charisma, 10)), Amounts((SkillKind.Charisma, 1))),
            new Crime("mixed", 4, 200, 0.05, Amounts((SkillKind.Strength, 1)), Amounts((SkillKind.Strength, 3), (SkillKind.Agility, 1)))
        ]);

    private static JobRank Rank(string title, double salary, double reputation, double requiredReputation, int hackingLevel, double hackingXp)
        => new(title, salary, reputation, requiredReputation,
            hackingLevel > 0 ? new Dictionary<SkillKind, int> { [SkillKind.Hacking] = hackingLevel } : new Dictionary<SkillKind, int>(),
            new Dictionary<SkillKind, double> { [SkillKind.Hacking] = hackingXp });

    private static IReadOnlyDictionary<SkillKind, double> Amounts(params (SkillKind Skill, double Amount)[] amounts)
        => amounts.ToDictionary(a => a.Skill, a => a.Amount);
}