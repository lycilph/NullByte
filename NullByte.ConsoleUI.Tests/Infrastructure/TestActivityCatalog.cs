using NullByte.Core.Domain;
using NullByte.Core.Domain.Activities;
using NullByte.Core.World;

namespace NullByte.ConsoleUI.Tests.Infrastructure;

/// <summary>
/// A small activity catalog with round numbers, so that tests can work out the expected amounts by hand.
/// </summary>
internal static class TestActivityCatalog
{
    // "algorithms" costs $10/s for 2 hacking xp/s, "free-course" is free, "strength" costs $5/s. The player starts with $1000.
    public static ActivityCatalog Default { get; } = new(
        facilities:
        [
            new TrainingFacility("plain-university", FacilityKind.University, 1, 1),
            new TrainingFacility("fancy-university", FacilityKind.University, 2, 3),
            new TrainingFacility("test-gym", FacilityKind.Gym, 1, 1)
        ],
        courses:
        [
            new Course("algorithms", FacilityKind.University, 10, new Dictionary<SkillKind, double> { [SkillKind.Hacking] = 2 }),
            new Course("free-course", FacilityKind.University, 0, new Dictionary<SkillKind, double> { [SkillKind.Charisma] = 1 }),
            new Course("strength", FacilityKind.Gym, 5, new Dictionary<SkillKind, double> { [SkillKind.Strength] = 1 })
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
