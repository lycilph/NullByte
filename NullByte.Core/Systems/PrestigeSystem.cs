namespace NullByte.Core.Systems;

// ---------------------------------------------------------------------------
// Systems: pure game logic. Each exposes a Tick(GameState) that advances
// exactly one tick. No I/O, no timers, no UI — fully unit-testable.
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Prestige: burn your identity at level >= UnlockLevel for 0-day tokens
// that scale with your level at burn time. Tokens install implants
// (permanent upgrades that survive every future reset).
//
// Reset wipes: money, XP, level, skill points, rig, perks, contracts.
// Keeps: 0-days, implants, achievements, lifetime stats.
//
// The unlock is a THRESHOLD (learn the mechanic once), but the trigger is
// manual + scaling — every burn is a push-your-luck decision, which is the
// actual gameplay.
// ---------------------------------------------------------------------------

public static class PrestigeSystem
{
    public const int UnlockLevel = 10;

    public static bool Unlocked(GameState s) => s.Level >= UnlockLevel;

    /// <summary>0-days for burning at 'level': 1 at 10, 2 at 12, 4 at 14, ...</summary>
    public static long TokensForLevel(int level)
        => level < UnlockLevel
            ? 0
            : (long)Math.Floor(Math.Pow(2, (level - UnlockLevel) / 2.0));

    // -- Implant effect helpers (referenced by id in the content JSON) --

    public static double MoneyMult(GameState s)
        => 1 + 0.1 * s.PrestigeLevels.GetValueOrDefault("notoriety");

    public static double XpMult(GameState s)
        => 1 + 0.1 * s.PrestigeLevels.GetValueOrDefault("blackice");

    public static double SpeedMult(GameState s)
        => Math.Pow(0.97, s.PrestigeLevels.GetValueOrDefault("neural"));

    public static double SeedMoney(GameState s)
        => 250 * (Math.Pow(3, s.PrestigeLevels.GetValueOrDefault("burner")) - 1);

    /// <summary>Execute the burn. Validates; no-ops below the unlock level.</summary>
    public static void Reset(GameState s, GameContent c)
    {
        if (!Unlocked(s))
        {
            s.Log($"Identity burning requires level {UnlockLevel}.");
            return;
        }

        long tokens = TokensForLevel(s.Level);
        if (tokens <= 0)
        {
            s.Log("Not enough progress to burn for 0-days.");
            return;
        }

        s.ZeroDays += tokens;

        // Wipe the run (keep: stats, achievements, 0-days, implants).
        s.Money = SeedMoney(s);
        s.Xp = 0;
        s.Level = 1;
        s.SkillPoints = 0;
        s.UpgradeLevels = [];
        s.PerkLevels = [];
        s.ActiveHacks = [];
        s.ContractOffers = [];
        s.ActiveContracts = [];

        s.Stats.PrestigeCount++;

        s.Log($"Identity burned. +{tokens} 0-days. The dark web forgets you... for now.");
    }
}