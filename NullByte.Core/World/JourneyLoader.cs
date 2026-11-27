using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using NullByte.Core.Domain.Progression;

namespace NullByte.Core.World;

/// <summary>
/// Loads and validates the journey from its serialized world definition.
/// </summary>
/// <remarks>
/// Every problem is reported as an <see cref="InvalidDataException"/> that says which chapter is wrong,
/// so a typo in the data file is easy to find.
/// </remarks>
public sealed partial class JourneyLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    // Chapter ids are stored in saves, so they are plain stable slugs.
    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex IdPattern();

    private static readonly HashSet<ConditionKind> HostnameConditions = [ConditionKind.AtServer, ConditionKind.RootOnServer];

    public Journey Load(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("Journey JSON cannot be empty.", nameof(json));

        JourneyDefinition? definition;
        try
        {
            definition = JsonSerializer.Deserialize<JourneyDefinition>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Journey JSON is invalid: {ex.Message}", ex);
        }

        if (definition is null || definition.Chapters.Count == 0)
            throw new InvalidDataException("The journey needs at least one chapter.");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var granted = new Dictionary<GameFeature, string>();
        var chapters = new List<Chapter>();

        foreach (var chapter in definition.Chapters)
        {
            if (string.IsNullOrEmpty(chapter.Id) || !IdPattern().IsMatch(chapter.Id))
                throw new InvalidDataException($"Chapter id '{chapter.Id}' must be a lowercase slug such as 'first-contact'.");

            if (!ids.Add(chapter.Id))
                throw new InvalidDataException($"Chapter id '{chapter.Id}' is used more than once.");

            if (string.IsNullOrWhiteSpace(chapter.Title))
                throw new InvalidDataException($"Chapter '{chapter.Id}' needs a title.");

            if (chapter.Story is null || chapter.Story.Count == 0)
                throw new InvalidDataException($"Chapter '{chapter.Id}' needs some story text.");

            foreach (var feature in chapter.Grants)
            {
                // One owner per feature keeps the story honest: there is exactly one moment where it unlocks.
                if (!granted.TryAdd(feature, chapter.Id))
                    throw new InvalidDataException($"Feature '{feature}' is granted by both '{granted[feature]}' and '{chapter.Id}'.");
            }

            var conditions = chapter.Conditions.Select(condition => ToCondition(chapter.Id, condition)).ToArray();

            chapters.Add(new Chapter(chapter.Id, chapter.Title, chapter.Hint, chapter.Story, conditions, chapter.Grants));
        }

        return new Journey(chapters);
    }

    private static ChapterCondition ToCondition(string chapterId, ConditionDefinition condition)
    {
        if (HostnameConditions.Contains(condition.Kind))
        {
            if (string.IsNullOrWhiteSpace(condition.Hostname))
                throw new InvalidDataException($"Chapter '{chapterId}': condition '{condition.Kind}' needs a hostname.");
        }
        else if (!double.IsFinite(condition.Amount) || condition.Amount <= 0)
        {
            throw new InvalidDataException($"Chapter '{chapterId}': condition '{condition.Kind}' needs an amount above zero.");
        }

        return new ChapterCondition(condition.Kind, condition.Amount, condition.Hostname);
    }
}
