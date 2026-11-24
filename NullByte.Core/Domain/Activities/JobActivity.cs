namespace NullByte.Core.Domain.Activities;

/// <summary>
/// Working at a company, at one rank of one career path. The rank goes up by
/// itself when the player qualifies for the next one.
/// </summary>
/// <remarks>
/// <see cref="RankIndex"/> is changed on the game thread only. The description
/// does not mention the rank, so it stays the same through promotions.
/// </remarks>
public sealed class JobActivity : Activity
{
    private int rankIndex;

    public JobActivity(Company company, CareerPath career, int rankIndex, ActivityMode mode) : base(mode)
    {
        Company = company ?? throw new ArgumentNullException(nameof(company));
        Career = career ?? throw new ArgumentNullException(nameof(career));

        if (!company.Careers.Contains(career))
            throw new ArgumentException($"'{career.Name}' is not a career at '{company.Name}'.", nameof(career));

        if (rankIndex < 0 || rankIndex >= career.Ranks.Count)
            throw new ArgumentOutOfRangeException(nameof(rankIndex), rankIndex, "That rank does not exist.");

        this.rankIndex = rankIndex;
    }

    public Company Company { get; }

    public CareerPath Career { get; }

    /// <summary>The position on the career path, counting from zero at the entry rank.</summary>
    public int RankIndex
    {
        get => rankIndex;
        internal set
        {
            if (value < 0 || value >= Career.Ranks.Count)
                throw new ArgumentOutOfRangeException(nameof(value), value, "That rank does not exist.");

            rankIndex = value;
        }
    }

    /// <summary>The rank the player holds right now.</summary>
    public JobRank Rank => Career.Ranks[RankIndex];

    /// <summary>Whether there is no higher rank to reach.</summary>
    public bool IsTopRank => RankIndex == Career.Ranks.Count - 1;

    public override string Description => $"Working at {Company.Name} ({Career.Name})";
}