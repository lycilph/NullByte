namespace NullByte.Core.Scripting.Activities;

/// <summary>
/// Defines the activities sub-API exposed to player-written scripts through
/// <c>nb.Activities</c>: working, studying, training and crime.
/// </summary>
/// <remarks>
/// The player does one thing at a time, and scripts follow the same rules as the console.
/// Whatever a script starts runs in the background, with the reduced rewards that go with
/// it, so a script can never take over the console. Names are the ones the console uses
/// (<c>algorithms</c>, <c>pixelworks</c>, <c>shoplift</c>, ...); a script that gets one
/// wrong gets an exception, except where a result says why.
/// </remarks>
public interface INullByteActivitiesApi
{
    /// <summary>
    /// Starts working for a company, at the highest rank the player qualifies for.
    /// The career can be left out if the player already has a job there, or the company has only one.
    /// </summary>
    [RamCost(2.0)]
    ActivityActionResult Work(string company, string career = "");

    /// <summary>Starts a university course. The university can be left out (the cheapest is used).</summary>
    [RamCost(2.0)]
    ActivityActionResult Study(string course, string university = "");

    /// <summary>Starts training a skill at a gym. The gym can be left out (the cheapest is used).</summary>
    [RamCost(2.0)]
    ActivityActionResult Train(string skill, string gym = "");

    /// <summary>
    /// Makes one attempt at a crime and waits for it to end. If the player is busy with
    /// something else, it does not start and the result says so.
    /// </summary>
    [RamCost(2.0)]
    Task<CrimeOutcome> Crime(string crime);

    /// <summary>Stops whatever the player is doing. Returns false if they were not doing anything.</summary>
    [RamCost(1.0)]
    bool Stop();

    /// <summary>Gets what the player is doing right now, or null if nothing.</summary>
    [RamCost(1.0)]
    ActivityStatus? GetStatus();

    /// <summary>Gets the level of a skill: hacking, strength, defense, dexterity, agility or charisma.</summary>
    [RamCost(0.1)]
    int GetSkillLevel(string skill);

    /// <summary>Gets the chance that the player's next attempt at a crime succeeds.</summary>
    [RamCost(0.5)]
    double GetCrimeChance(string crime);

    /// <summary>Gets the reputation the player has earned with a company.</summary>
    [RamCost(0.5)]
    double GetReputation(string company);

    /// <summary>Gets the job the player holds at a company, or null if none.</summary>
    [RamCost(0.5)]
    JobInfo? GetJob(string company);

    /// <summary>Lists the names of the crimes.</summary>
    [RamCost(0.2)]
    IReadOnlyList<string> GetCrimes();

    /// <summary>Lists the names of the courses, both university courses and gym courses.</summary>
    [RamCost(0.2)]
    IReadOnlyList<string> GetCourses();

    /// <summary>Lists the names of the companies.</summary>
    [RamCost(0.2)]
    IReadOnlyList<string> GetCompanies();
}