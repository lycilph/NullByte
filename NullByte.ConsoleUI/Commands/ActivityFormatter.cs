using NullByte.Core.Domain;
using NullByte.Core.Domain.Activities;
using NullByte.Core.Formulas;
using NullByte.Core.Services;

namespace NullByte.ConsoleUI.Commands;

/// <summary>
/// Turns activities and their numbers into text for the console.
/// </summary>
public static class ActivityFormatter
{
    /// <summary>Formats money: <c>$1,234.50</c>, or <c>-$3.00</c> when negative.</summary>
    public static string FormatMoney(double amount)
        => amount < 0 ? $"-${-amount:N2}" : $"${amount:N2}";

    /// <summary>Formats a gain or a loss of money: <c>+$12.00</c> or <c>-$3.00</c>.</summary>
    public static string FormatSignedMoney(double amount)
        => amount < 0 ? $"-${-amount:N2}" : $"+${amount:N2}";

    /// <summary>Formats a duration in whole seconds: <c>7s</c>, <c>2m 05s</c> or <c>1h 02m 03s</c>.</summary>
    public static string FormatDuration(TimeSpan duration)
    {
        var total = (long)Math.Floor(Math.Max(0, duration.TotalSeconds));
        var hours = total / 3600;
        var minutes = total % 3600 / 60;
        var seconds = total % 60;

        if (hours > 0)
            return $"{hours}h {minutes:00}m {seconds:00}s";

        return minutes > 0 ? $"{minutes}m {seconds:00}s" : $"{seconds}s";
    }

    /// <summary>The name of a skill as the player types it: <c>hacking</c>.</summary>
    public static string FormatSkill(SkillKind skill) => skill.ToString().ToLowerInvariant();

    /// <summary>Formats experience: <c>+166.00 hacking xp, +20.00 charisma xp</c>.</summary>
    public static string FormatExperience(IReadOnlyDictionary<SkillKind, double> experience, bool perSecond = false)
    {
        var parts = experience
            .Where(entry => entry.Value > 0)
            .OrderBy(entry => entry.Key)
            .Select(entry => $"+{entry.Value:N2} {FormatSkill(entry.Key)} xp{(perSecond ? "/s" : "")}")
            .ToList();

        return parts.Count == 0 ? "no experience" : string.Join(", ", parts);
    }

    /// <summary>Formats reputation: <c>+31.00 reputation</c>, or per second <c>+0.10 reputation/s</c>.</summary>
    public static string FormatReputation(double reputation, bool perSecond = false)
        => $"+{reputation:N2} reputation{(perSecond ? "/s" : "")}";

    /// <summary>Formats what a job rank asks for: <c>hacking level 25, reputation 100</c>.</summary>
    public static string FormatRequirements(JobRank rank)
    {
        var parts = rank.RequiredLevels
            .OrderBy(entry => entry.Key)
            .Select(entry => $"{FormatSkill(entry.Key)} level {entry.Value}")
            .ToList();

        if (rank.RequiredReputation > 0)
            parts.Add($"reputation {rank.RequiredReputation:N0}");

        return parts.Count == 0 ? "no requirements" : string.Join(", ", parts);
    }

    /// <summary>Formats what an activity gives and costs: <c>costs $40.00/s, +2.00 hacking xp/s</c>.</summary>
    public static string FormatRates(ActivityRates rates)
    {
        var money = rates.MoneyPerSecond switch
        {
            < 0 => $"costs {FormatMoney(-rates.MoneyPerSecond)}/s",
            > 0 => $"pays {FormatMoney(rates.MoneyPerSecond)}/s",
            _ => "free"
        };

        var line = $"{money}, {FormatExperience(rates.ExperiencePerSecond, perSecond: true)}";

        return rates.ReputationPerSecond > 0
            ? $"{line}, {FormatReputation(rates.ReputationPerSecond, perSecond: true)}"
            : line;
    }

    /// <summary>Formats what an activity has earned so far, as one line for the live panel.</summary>
    public static string FormatProgress(ActivityProgress progress)
    {
        var parts = new List<string>
        {
            FormatDuration(progress.Elapsed),
            FormatSignedMoney(progress.NetMoney),
            FormatExperience(progress.ExperienceGained)
        };

        if (progress.ReputationGained > 0)
            parts.Add(FormatReputation(progress.ReputationGained));

        if (progress.Attempts > 0)
            parts.Add($"{progress.Successes}/{progress.Attempts} succeeded");

        return string.Join(" | ", parts);
    }

    /// <summary>Formats the result of an activity that has ended.</summary>
    public static string FormatSummary(Activity activity)
    {
        var progress = activity.Progress;

        var parts = new List<string>
        {
            FormatDuration(progress.Elapsed),
            FormatSignedMoney(progress.NetMoney),
            FormatExperience(progress.ExperienceGained)
        };

        if (progress.ReputationGained > 0)
            parts.Add(FormatReputation(progress.ReputationGained));

        if (progress.Attempts > 0)
            parts.Add($"{progress.Successes} of {progress.Attempts} succeeded");

        return $"{activity.Description}: {string.Join(", ", parts)}.";
    }

    /// <summary>Formats a chance as a whole percentage: <c>20%</c>, or <c>&lt;1%</c> for a chance that is small but not nil.</summary>
    public static string FormatChance(double chance)
    {
        // Built by hand: {chance:P0} puts a space before the % in some cultures.
        var percent = (int)Math.Round(chance * 100);

        return chance > 0 && percent == 0 ? "<1%" : $"{percent}%";
    }

    /// <summary>Formats what an attempt at a crime gives: <c>+$300.00, +2.00 dexterity xp</c>, or <c>no money, ...</c> for a failure.</summary>
    public static string FormatCrimeRewards(CrimeRewards rewards)
        => $"{(rewards.Money > 0 ? FormatSignedMoney(rewards.Money) : "no money")}, {FormatExperience(rewards.Experience)}";

    /// <summary>Formats how an attempt at a crime went, as one line.</summary>
    public static string FormatAttempt(CrimeAttempt attempt)
        => $"{attempt.Activity.Crime.Name}: {(attempt.Succeeded ? "success" : "failed")} ({FormatChance(attempt.Chance)} chance): {FormatCrimeRewards(attempt.Rewards)}";

    /// <summary>The short word shown in the prompt while the player is busy: <c>studying</c>.</summary>
    public static string PromptTag(Activity activity) => activity switch
    {
        ClassActivity { Facility.Kind: FacilityKind.University } => "studying",
        ClassActivity => "training",
        JobActivity => "working",
        CrimeActivity => "crime",
        _ => "busy"
    };
}