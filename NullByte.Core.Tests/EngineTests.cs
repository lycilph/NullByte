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