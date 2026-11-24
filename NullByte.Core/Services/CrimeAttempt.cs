using NullByte.Core.Domain.Activities;
using NullByte.Core.Formulas;

namespace NullByte.Core.Services;

/// <summary>
/// Describes an attempt at a crime that has just ended.
/// </summary>
/// <param name="Activity">The crime. Its totals include every attempt that ended in the same update as this one.</param>
/// <param name="Succeeded">Whether the attempt succeeded.</param>
/// <param name="Chance">The chance it had of succeeding.</param>
/// <param name="Rewards">What it gave.</param>
public sealed record CrimeAttempt(CrimeActivity Activity, bool Succeeded, double Chance, CrimeRewards Rewards);