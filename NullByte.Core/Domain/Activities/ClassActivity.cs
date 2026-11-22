namespace NullByte.Core.Domain.Activities;

/// <summary>
/// Taking a course at a university or gym.
/// </summary>
public sealed class ClassActivity : Activity
{
    public ClassActivity(Course course, TrainingFacility facility, ActivityMode mode)
        : base(mode)
    {
        Course = course ?? throw new ArgumentNullException(nameof(course));
        Facility = facility ?? throw new ArgumentNullException(nameof(facility));

        if (course.Kind != facility.Kind)
            throw new ArgumentException($"The {course.Kind} course '{course.Name}' cannot be taken at the {facility.Kind} '{facility.Name}'.", nameof(facility));
    }

    public Course Course { get; }

    public TrainingFacility Facility { get; }

    public override string Description
        => $"{(Facility.Kind == FacilityKind.University ? "Studying" : "Training")} {Course.Name} at {Facility.Name}";
}