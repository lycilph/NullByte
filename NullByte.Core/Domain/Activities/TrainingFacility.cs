namespace NullByte.Core.Domain.Activities;

/// <summary>
/// A university or gym. Every facility of a kind offers every course of that
/// kind; facilities differ only in price and quality.
/// </summary>
/// <param name="Name">Unique command-friendly name, e.g. <c>rothman-university</c>.</param>
/// <param name="Kind">Whether this is a university or a gym.</param>
/// <param name="ExperienceMultiplier">Multiplies the experience a course gives here.</param>
/// <param name="CostMultiplier">Multiplies the cost of a course here.</param>
public sealed record TrainingFacility(
    string Name,
    FacilityKind Kind,
    double ExperienceMultiplier,
    double CostMultiplier);