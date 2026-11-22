using NullByte.Core.Domain.Activities;

namespace NullByte.Core.Domain;

/// <summary>
/// Everything a player can do besides hacking: the facilities, courses,
/// companies and crimes of the world. This is fixed data, not game state;
/// it is never saved.
/// </summary>
/// <remarks>
/// Names are unique per kind and looked up ignoring case.
/// The catalog trusts its input: validation is the loader's job.
/// </remarks>
public sealed class ActivityCatalog
{
    private readonly Dictionary<string, TrainingFacility> facilitiesByName;
    private readonly Dictionary<string, Course> coursesByName;
    private readonly Dictionary<string, Company> companiesByName;
    private readonly Dictionary<string, Crime> crimesByName;

    public ActivityCatalog(
        IReadOnlyList<TrainingFacility> facilities,
        IReadOnlyList<Course> courses,
        IReadOnlyList<Company> companies,
        IReadOnlyList<Crime> crimes)
    {
        Facilities = facilities ?? throw new ArgumentNullException(nameof(facilities));
        Courses = courses ?? throw new ArgumentNullException(nameof(courses));
        Companies = companies ?? throw new ArgumentNullException(nameof(companies));
        Crimes = crimes ?? throw new ArgumentNullException(nameof(crimes));

        facilitiesByName = facilities.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
        coursesByName = courses.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
        companiesByName = companies.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
        crimesByName = crimes.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<TrainingFacility> Facilities { get; }
    public IReadOnlyList<Course> Courses { get; }
    public IReadOnlyList<Company> Companies { get; }
    public IReadOnlyList<Crime> Crimes { get; }

    public bool TryGetFacility(string name, out TrainingFacility facility)
        => facilitiesByName.TryGetValue(name, out facility!);

    public bool TryGetCourse(string name, out Course course)
        => coursesByName.TryGetValue(name, out course!);

    public bool TryGetCompany(string name, out Company company)
        => companiesByName.TryGetValue(name, out company!);

    public bool TryGetCrime(string name, out Crime crime)
        => crimesByName.TryGetValue(name, out crime!);
}