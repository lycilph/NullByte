namespace NullByte.Core.Domain.Activities;

/// <summary>
/// Committing a crime: attempts of a fixed duration, each of which succeeds or
/// fails when it ends. A repeating crime starts the next attempt by itself.
/// </summary>
/// <remarks>
/// The time into the current attempt is kept here; <c>ActivitySystem</c> advances
/// it and rolls the dice. <see cref="CycleProgress"/> may be read from any thread
/// (the terminal draws a progress bar from it).
/// </remarks>
public sealed class CrimeActivity : Activity
{
    private double cycleProgress;

    public CrimeActivity(Crime crime, bool repeat, ActivityMode mode) : base(mode)
    {
        Crime = crime ?? throw new ArgumentNullException(nameof(crime));
        Repeat = repeat;
    }

    public Crime Crime { get; }

    /// <summary>Whether a new attempt starts when one ends. If not, the activity ends after one attempt.</summary>
    public bool Repeat { get; }

    /// <summary>How far into the current attempt it is, from 0 to 1.</summary>
    public double CycleProgress => Volatile.Read(ref cycleProgress);

    /// <summary>Game seconds into the current attempt. Game thread only.</summary>
    internal double CycleSeconds { get; private set; }

    internal void SetCycle(double seconds)
    {
        CycleSeconds = seconds;
        Volatile.Write(ref cycleProgress, Math.Clamp(seconds / Crime.DurationSeconds, 0.0, 1.0));
    }

    public override string Description => $"Committing {Crime.Name}";
}