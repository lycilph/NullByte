namespace NullByte.Core.Domain.Activities;

/// <summary>
/// The player's working life: the job held at each company, and the reputation
/// earned with each.
/// </summary>
/// <remarks>
/// A player holds at most one job per company, but any number of companies.
/// Reputation belongs to the company, not the job, so it is kept when the player
/// moves to another career there. Only <c>ActivityService</c> and
/// <c>ActivitySystem</c> change it. Company names are matched ignoring case.
/// </remarks>
public sealed class Employment
{
    private readonly Dictionary<string, double> reputation = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Job> jobs = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the jobs held, by company name.</summary>
    public IReadOnlyDictionary<string, Job> Jobs => jobs;

    /// <summary>Gets the reputation earned, by company name. Companies with none yet are missing.</summary>
    public IReadOnlyDictionary<string, double> Reputation => reputation;

    /// <summary>Gets the reputation earned with a company (zero if none).</summary>
    public double GetReputation(string company) => reputation.GetValueOrDefault(company);

    /// <summary>Gets the job held at a company, or null.</summary>
    public Job? GetJob(string company) => jobs.GetValueOrDefault(company);

    internal void AddReputation(string company, double amount)
    {
        if (!double.IsFinite(amount) || amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Reputation must be a finite, non-negative value.");

        reputation[company] = GetReputation(company) + amount;
    }

    internal void SetJob(string company, Job job)
    {
        ArgumentNullException.ThrowIfNull(job);

        jobs[company] = job;
    }
}