namespace NullByte.Core.Systems;

// ---------------------------------------------------------------------------
// Systems: pure game logic. Each exposes a Tick(GameState) that advances
// exactly one tick. No I/O, no timers, no UI — fully unit-testable.
// ---------------------------------------------------------------------------


// ---------------------------------------------------------------------------
// Contracts: timed bonus objectives. Pure bonus — missing the deadline only
// loses the reward. Offers rotate from the content pool (max 3 at a time,
// never the same contract offered and active simultaneously); completed
// contracts can be offered again later.
//
// Progress is a stat DELTA since acceptance (baseline snapshotted at accept),
// so goals like "complete 5 hacks" work regardless of lifetime totals, and it
// reuses the same stat keys as achievements.
// ---------------------------------------------------------------------------

public static class ContractsSystem
{
    public const int MaxOffers = 3;

    private static readonly Random rng = new();

    public static double Progress(GameState s, GameContent c, ActiveContractState a)
    {
        var def = c.FindContract(a.DefId);
        return def is null ? 0
            : Math.Max(0, AchievementsSystem.StatValue(s, def.Stat) - a.StartStat);
    }

    public static void Accept(GameState s, GameContent c, string defId)
    {
        var def = c.FindContract(defId);
        var offer = s.ContractOffers.Find(o => o.DefId == defId);
        if (def is null || offer is null)
        {
            s.Log($"No open contract '{defId}' (see 'contracts').");
            return;
        }

        s.ContractOffers.Remove(offer);
        s.ActiveContracts.Add(new ActiveContractState
        {
            DefId = def.Id,
            DeadlineRemaining = def.DurationTicks,
            StartStat = AchievementsSystem.StatValue(s, def.Stat),
        });
        s.Log($"Contract accepted: {def.Name}. Deadline: {def.DurationTicks} ticks.");
    }

    public static void Tick(GameState s, GameContent c)
    {
        // Refill the offer board.
        while (s.ContractOffers.Count < MaxOffers)
        {
            var pool = c.Contracts
                .Where(d => s.ContractOffers.All(o => o.DefId != d.Id)
                         && s.ActiveContracts.All(a => a.DefId != d.Id))
                .ToList();
            if (pool.Count == 0)
                break;

            var pick = pool[rng.Next(pool.Count)];
            s.ContractOffers.Add(new ContractOfferState { DefId = pick.Id });
            s.Log($"New contract available: {pick.Name} (see 'contracts').");
        }

        // Advance active contracts: completion check, then deadline.
        for (int i = s.ActiveContracts.Count - 1; i >= 0; i--)
        {
            var a = s.ActiveContracts[i];
            var def = c.FindContract(a.DefId);
            if (def is null)
            {
                s.ActiveContracts.RemoveAt(i); // def removed from content
                continue;
            }

            if (Progress(s, c, a) >= def.Goal)
            {
                s.Money += def.RewardMoney;
                s.Xp += def.RewardXp;
                s.Stats.ContractsSucceeded++;
                s.Log($"CONTRACT COMPLETE: {def.Name} — bonus +${def.RewardMoney:0.##}, +{def.RewardXp:0.#} XP.");
                s.ActiveContracts.RemoveAt(i);
                continue;
            }

            a.DeadlineRemaining--;
            if (a.DeadlineRemaining <= 0)
            {
                s.Stats.ContractsFailed++;
                s.Log($"Contract failed: {def.Name} — expired. No bonus this time.");
                s.ActiveContracts.RemoveAt(i);
            }
        }
    }
}