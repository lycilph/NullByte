using NullByte.ConsoleUI.Systems;
using NullByte.Core.Commands;
using NullByte.Core.Systems;

namespace NullByte.Core.Tests;

public class EngineTests
{
    private static GameContent Content() => ContentLoader.FromJson(ContentLoader.DefaultContentJson);
    private static GameSession Session() => new(Content());

    // -- Cost curve --

    [Fact]
    public void UpgradeCost_IsExponential()
    {
        var cpu = Content().FindUpgrade("cpu")!;
        Assert.Equal(50.0, CostMath.UpgradeCost(cpu, 0), 3);
        Assert.Equal(50.0 * 1.15, CostMath.UpgradeCost(cpu, 1), 3);
        Assert.Equal(50.0 * 1.15 * 1.15, CostMath.UpgradeCost(cpu, 2), 3);
    }

    // -- Hacking --

    [Fact]
    public void Hack_CompletesAndPays()
    {
        var session = Session();
        session.Submit(new StartHackCommand("webmail"));

        for (int i = 0; i < 31; i++) // 1 tick to apply + 30 duration
            session.Tick();

        Assert.Empty(session.State.ActiveHacks);
        Assert.Equal(15.0, session.State.Money, 3);
        Assert.Equal(5.0, session.State.Xp, 3);
    }

    [Fact]
    public void Hack_BlockedByLevelGate()
    {
        var session = Session();
        session.Submit(new StartHackCommand("city-bank")); // needs level 7
        session.Tick();

        Assert.Empty(session.State.ActiveHacks);
        Assert.Equal(0.0, session.State.Money, 3);
        Assert.Contains(session.State.EventLog, e => e.Contains("security too high"));
    }

    [Fact]
    public void RigRequirements_BlockHack()
    {
        // corp-vpn requires CPU level 5 (and only player level 4)
        var session = Session();
        session.State.Level = 10;
        session.State.UpgradeLevels["cpu"] = 4;

        session.Submit(new StartHackCommand("corp-vpn"));
        session.Tick();

        Assert.Empty(session.State.ActiveHacks);
        Assert.Contains(session.State.EventLog, e => e.Contains("CPU level 5"));
    }

    [Fact]
    public void RigRequirements_Met_HackSucceeds()
    {
        var session = Session();
        session.State.Level = 10;
        session.State.UpgradeLevels["cpu"] = 5;

        session.Submit(new StartHackCommand("corp-vpn"));
        session.Tick();

        Assert.Single(session.State.ActiveHacks);
    }

    // -- Skill tree --

    [Fact]
    public void LevelUp_GrantsSkillPoint()
    {
        var session = Session();
        session.State.Xp = 101; // 100 xp needed for level 2
        session.Tick();

        Assert.Equal(2, session.State.Level);
        Assert.Equal(1, session.State.SkillPoints);
    }

    [Fact]
    public void SkillPerk_CanBeBoughtAndApplied()
    {
        var session = Session();
        session.State.SkillPoints = 2;

        session.Submit(new SpendSkillCommand("multitask"));
        session.Tick();

        Assert.Equal(1, session.State.SkillPoints);
        Assert.Equal(1, session.State.PerkLevels["multitask"]);
        Assert.Equal(2, HackSystem.MaxConcurrentHacks(session.State)); // base + perk
    }

    [Fact]
    public void SkillPerk_WithoutPoints_IsRefused()
    {
        var session = Session();
        session.Submit(new SpendSkillCommand("overclock"));
        session.Tick();

        Assert.Equal(0, session.State.PerkLevels.GetValueOrDefault("overclock"));
        Assert.Contains(session.State.EventLog, e => e.Contains("No skill points"));
    }

    [Fact]
    public void OverclockPerk_ReducesDuration()
    {
        var session = Session();
        var webmail = Content().FindTarget("webmail")!;
        session.State.PerkLevels["overclock"] = 10;

        Assert.Equal(22, HackSystem.HackDuration(webmail, session.State)); // 30 * 0.97^10
    }
    
    [Fact]
    public void RansomPerk_BoostsReward()
    {
        var session = Session();
        session.State.PerkLevels["ransom"] = 5; // +50% money

        session.Submit(new StartHackCommand("webmail"));
        session.Tick();

        Assert.Equal(15 * 1.5, session.State.ActiveHacks[0].MoneyReward, 3);
    }

    // -- Upgrades --

    [Fact]
    public void Ram_AddsConcurrentSlots()
    {
        var session = Session();
        session.State.UpgradeLevels["ram"] = 2;
        Assert.Equal(3, HackSystem.MaxConcurrentHacks(session.State));

        session.Submit(new StartHackCommand("webmail"));
        session.Submit(new StartHackCommand("webmail"));
        session.Submit(new StartHackCommand("webmail"));
        session.Submit(new StartHackCommand("webmail")); // 4th should not fit
        session.Tick();

        Assert.Equal(3, session.State.ActiveHacks.Count);
    }

    [Fact]
    public void Cpu_ReducesDuration()
    {
        var session = Session();
        var webmail = Content().FindTarget("webmail")!;
        Assert.Equal(30, HackSystem.HackDuration(webmail, session.State));

        session.State.UpgradeLevels["cpu"] = 10;
        Assert.Equal(18, HackSystem.HackDuration(webmail, session.State)); // 30 * 0.95^10
    }

    // -- Economy & progression --

    [Fact]
    public void Botnet_GeneratesPassiveIncome()
    {
        var session = Session();
        session.State.UpgradeLevels["botnet"] = 1; // 0.23/tick
        session.FastForward(10);

        Assert.Equal(10 * 0.2 * 1.15, session.State.Money, 3);
    }

    [Fact]
    public void Xp_LevelsUpPlayer()
    {
        var session = Session();
        session.State.Xp = 101; // 100 xp needed for level 2
        session.Tick();

        Assert.Equal(2, session.State.Level);
    }

    // -- Buying --

    [Fact]
    public void Buy_RequiresMoneyAndApplies()
    {
        var session = Session();
        session.State.Money = 40;
        session.Submit(new BuyUpgradeCommand("cpu")); // costs 50
        session.Tick();
        Assert.Equal(0, session.State.UpgradeLevels.GetValueOrDefault("cpu"));

        session.State.Money = 60;
        session.Submit(new BuyUpgradeCommand("cpu"));
        session.Tick();

        Assert.Equal(1, session.State.UpgradeLevels["cpu"]);
        Assert.Equal(10.0, session.State.Money, 3); // 60 - 50
    }

    // -- Flavor --

    [Fact]
    public void Flavor_TemplatesAreFilled()
    {
        string filled = Flavor.Fill("Siphoned ${money} out of {target}",
                                    target: "bank", money: 42.5, xp: 3);
        Assert.Equal("Siphoned $42,5 out of bank", filled);
    }

    [Fact]
    public void Flavor_PickHandlesEmptyPool()
    {
        Assert.Equal("", Flavor.Pick(new List<string>()));
    }

    [Fact]
    public void DefaultContent_IncludesFlavorPools()
    {
        var content = Content();
        Assert.NotEmpty(content.Flavor.HackStart);
        Assert.NotEmpty(content.Flavor.HackComplete);
        Assert.NotEmpty(content.Flavor.Idle);
    }

    // -- Achievements & stats --

    [Fact]
    public void Stats_TrackHackCompletion()
    {
        var session = Session();
        session.Submit(new StartHackCommand("webmail"));
        session.FastForward(31);

        Assert.Equal(1, session.State.Stats.TotalHacks);
        Assert.Equal(1, session.State.Stats.HacksPerTarget["webmail"]);
        Assert.Equal(15.0, session.State.Stats.MoneyEarned, 3);
    }

    [Fact]
    public void Achievement_AwardedExactlyOnce()
    {
        var session = Session();
        session.Submit(new StartHackCommand("webmail"));
        session.FastForward(31);

        Assert.Contains("first-blood", session.State.Achievements);
        Assert.Single(session.State.Achievements);
        Assert.Contains(session.State.EventLog, e => e.Contains("First Blood"));
    }

    [Fact]
    public void TargetAchievement_RequiresSpecificTarget()
    {
        var session = Session();
        session.State.Level = 10;
        session.Submit(new StartHackCommand("webmail"));
        session.FastForward(31);

        Assert.DoesNotContain("bank-job", session.State.Achievements);
    }

    [Fact]
    public void StatsAndAchievements_SurviveRoundTrip()
    {
        string path = Path.Combine(Path.GetTempPath(), $"nbtest-{Guid.NewGuid():N}.json");
        try
        {
            var session = Session();
            session.Submit(new StartHackCommand("webmail"));
            session.FastForward(31);
            SaveSystem.Save(session.State, path);

            var loaded = SaveSystem.Load(path);
            Assert.NotNull(loaded);

            Assert.Equal(1, loaded!.Value.State.Stats.TotalHacks);
            Assert.Contains("first-blood", loaded.Value.State.Achievements);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // -- Contracts --

    /// <summary>Makes the offer board deterministic for tests.</summary>
    private static void ForceOffer(GameSession session, string defId)
    {
        session.State.ContractOffers.Clear();
        session.State.ContractOffers.Add(new ContractOfferState { DefId = defId });
    }

    [Fact]
    public void Contract_CompletesAndPaysBonus()
    {
        var session = Session();
        ForceOffer(session, "warmup"); // complete 5 hacks, deadline 1200 ticks
        session.Submit(new AcceptContractCommand("warmup"));
        session.Tick(); // accept applied

        for (int i = 0; i < 5; i++)
        {
            session.Submit(new StartHackCommand("webmail"));
            Forward(session, 31);
        }

        Assert.Empty(session.State.ActiveContracts);
        Assert.Contains(session.State.EventLog, e => e.Contains("CONTRACT COMPLETE"));
        Assert.Equal(5 * 15 + 500, session.State.Money, 3); // hack money + bonus
    }

    private static void Forward(GameSession session, long ticks)
    {
        for (long i = 0; i < ticks; i++)
            session.Tick();
    }

    [Fact]
    public void Contract_FailsOnExpiryWithNoPenalty()
    {
        var session = Session();
        ForceOffer(session, "warmup");
        session.Submit(new AcceptContractCommand("warmup"));
        session.Tick();
        session.State.ActiveContracts[0].DeadlineRemaining = 1;

        Forward(session, 2); // Must use this, as fastforward skips the contracts update

        Assert.Empty(session.State.ActiveContracts);
        Assert.Contains(session.State.EventLog, e => e.Contains("Contract failed"));
        Assert.Equal(0.0, session.State.Money, 3); // pure bonus: nothing lost
    }

    [Fact]
    public void Contracts_OffersRefillUpToMax()
    {
        var session = Session();
        Forward(session, 5); // Must use this, as fastforward skips the contracts update

        Assert.Equal(ContractsSystem.MaxOffers, session.State.ContractOffers.Count);
        Assert.Equal(ContractsSystem.MaxOffers,
            session.State.ContractOffers.Select(o => o.DefId).Distinct().Count());
    }

    [Fact]
    public void Accept_RequiresOpenOffer()
    {
        var session = Session();
        session.State.ContractOffers.Clear();
        session.Submit(new AcceptContractCommand("warmup"));
        session.Tick();

        Assert.Empty(session.State.ActiveContracts);
    }

    // -- Prestige --

    [Fact]
    public void Prestige_TokenCurveDoublesEveryTwoLevels()
    {
        Assert.Equal(0, PrestigeSystem.TokensForLevel(9));
        Assert.Equal(1, PrestigeSystem.TokensForLevel(10));
        Assert.Equal(2, PrestigeSystem.TokensForLevel(12));
        Assert.Equal(4, PrestigeSystem.TokensForLevel(14));
        Assert.Equal(32, PrestigeSystem.TokensForLevel(20));
    }

    [Fact]
    public void Prestige_LockedBelowUnlockLevel()
    {
        var session = Session();
        session.State.UpgradeLevels["cpu"] = 5;

        session.Submit(new PrestigeCommand());
        session.Tick();

        Assert.Equal(1, session.State.Level);            // nothing happened
        Assert.Equal(5, session.State.UpgradeLevels["cpu"]);
        Assert.Equal(0, session.State.ZeroDays);
        Assert.Contains(session.State.EventLog, e => e.Contains("requires level 10"));
    }

    [Fact]
    public void Prestige_ResetGrantsTokensAndWipesProgress()
    {
        var session = Session();
        session.State.Level = 12;
        session.State.UpgradeLevels["cpu"] = 5;
        session.State.ZeroDays = 1;
        session.State.Stats.TotalHacks = 40;
        session.State.Achievements.Add("first-blood");

        session.Submit(new PrestigeCommand());
        session.Tick();

        Assert.Equal(3, session.State.ZeroDays);           // 1 held + 2 earned
        Assert.Equal(1, session.State.Level);
        Assert.Equal(0.0, session.State.Money, 3);         // no Burner Fund yet
        Assert.Empty(session.State.UpgradeLevels);
        Assert.Equal(40, session.State.Stats.TotalHacks);  // lifetime kept
        Assert.Contains("first-blood", session.State.Achievements);
    }

    [Fact]
    public void Implant_CostsZeroDaysAndApplies()
    {
        var session = Session();
        session.State.ZeroDays = 5;

        session.Submit(new ImplantCommand("notoriety"));   // base cost 1
        session.Tick();

        Assert.Equal(1, session.State.PrestigeLevels["notoriety"]);
        Assert.Equal(4, session.State.ZeroDays);
        Assert.Equal(1.1, PrestigeSystem.MoneyMult(session.State), 3);
    }

    [Fact]
    public void BurnerFund_SeedsMoneyAfterBurn()
    {
        var session = Session();
        session.State.PrestigeLevels["burner"] = 2;       // 250 * (9 - 1)
        session.State.Level = 12;

        session.Submit(new PrestigeCommand());
        session.Tick();

        Assert.Equal(250 * 8, session.State.Money, 3);
    }

    // -- Save / load & offline --

    [Fact]
    public void Save_RoundTrips()
    {
        string path = Path.Combine(Path.GetTempPath(), $"nbtest-{Guid.NewGuid():N}.json");
        try
        {
            var session = Session();
            session.Submit(new StartHackCommand("webmail"));
            session.FastForward(31);
            session.State.UpgradeLevels["botnet"] = 3;
            SaveSystem.Save(session.State, path);

            var loaded = SaveSystem.Load(path);
            Assert.NotNull(loaded);

            Assert.Equal(session.State.Money, loaded!.Value.State.Money, 3);
            Assert.Equal(session.State.GameTick, loaded.Value.State.GameTick);
            Assert.Equal(3, loaded.Value.State.UpgradeLevels["botnet"]);
            Assert.Empty(loaded.Value.State.ActiveHacks);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Offline_FastForwardAccruesIncome()
    {
        var session = Session();
        session.State.UpgradeLevels["botnet"] = 5;
        session.FastForward(100); // e.g. 10 seconds away

        Assert.Equal(100 * 0.2 * 5 * Math.Pow(1.15, 5), session.State.Money, 3);
    }
}