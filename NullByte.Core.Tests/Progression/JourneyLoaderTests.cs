using NullByte.Core.Domain.Progression;
using NullByte.Core.World;

namespace NullByte.Core.Tests.Progression;

public sealed class JourneyLoaderTests
{
    private static readonly JourneyLoader Loader = new();

    private static string Json(string chapters) => $$"""{ "chapters": [ {{chapters}} ] }""";

    private const string ValidChapter = """
        { "id": "awakening", "title": "Awakening", "story": ["You exist."], "conditions": [], "grants": ["scan"] }
        """;

    [Fact]
    public void A_valid_journey_loads_with_its_conditions_and_grants()
    {
        var journey = Loader.Load(Json("""
            { "id": "one", "title": "One", "hint": "go", "story": ["a", "b"], "grants": ["scan", "connect"] },
            { "id": "two", "title": "Two", "story": ["c"],
              "conditions": [ { "kind": "atServer", "hostname": "n00dles" }, { "kind": "moneyAtLeast", "amount": 50 } ],
              "grants": ["nuke"] }
            """));

        Assert.Equal(2, journey.Chapters.Count);
        Assert.Equal(new[] { GameFeature.Scan, GameFeature.Connect }, journey.Chapters[0].Grants);
        Assert.Equal("go", journey.Chapters[0].Hint);
        Assert.Equal(new[] { "a", "b" }, journey.Chapters[0].Story);
        Assert.Equal(new ChapterCondition(ConditionKind.AtServer, 0, "n00dles"), journey.Chapters[1].Conditions[0]);
        Assert.Equal(new ChapterCondition(ConditionKind.MoneyAtLeast, 50), journey.Chapters[1].Conditions[1]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_json_is_refused(string json)
    {
        Assert.Throws<ArgumentException>(() => Loader.Load(json));
    }

    [Fact]
    public void Broken_json_is_reported_as_invalid_data()
    {
        Assert.Throws<InvalidDataException>(() => Loader.Load("{ not json"));
    }

    [Fact]
    public void A_journey_without_chapters_is_refused()
    {
        Assert.Throws<InvalidDataException>(() => Loader.Load("""{ "chapters": [] }"""));
    }

    [Fact]
    public void An_unknown_feature_is_refused()
    {
        var error = Assert.Throws<InvalidDataException>(() => Loader.Load(Json(
            """{ "id": "a", "title": "A", "story": ["x"], "grants": ["teleport"] }""")));

        Assert.Contains("invalid", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void An_unknown_condition_is_refused()
    {
        Assert.Throws<InvalidDataException>(() => Loader.Load(Json(
            """{ "id": "a", "title": "A", "story": ["x"], "conditions": [ { "kind": "fullMoon", "amount": 1 } ] }""")));
    }

    [Theory]
    [InlineData("First Contact")]
    [InlineData("first_contact")]
    [InlineData("-first")]
    [InlineData("")]
    public void A_chapter_id_must_be_a_lowercase_slug(string id)
    {
        var error = Assert.Throws<InvalidDataException>(() => Loader.Load(Json(
            $$"""{ "id": "{{id}}", "title": "A", "story": ["x"] }""")));

        Assert.Contains("slug", error.Message);
    }

    [Fact]
    public void A_repeated_chapter_id_is_refused_and_named()
    {
        var error = Assert.Throws<InvalidDataException>(() => Loader.Load(Json(
            $"{ValidChapter}, {ValidChapter}")));

        Assert.Contains("awakening", error.Message);
    }

    [Fact]
    public void A_feature_cannot_be_granted_by_two_chapters()
    {
        var error = Assert.Throws<InvalidDataException>(() => Loader.Load(Json("""
            { "id": "a", "title": "A", "story": ["x"], "grants": ["scan"] },
            { "id": "b", "title": "B", "story": ["x"], "grants": ["scan"] }
            """)));

        Assert.Contains("Scan", error.Message);
    }

    [Fact]
    public void A_chapter_needs_a_title_and_story()
    {
        Assert.Throws<InvalidDataException>(() => Loader.Load(Json("""{ "id": "a", "title": " ", "story": ["x"] }""")));
        Assert.Throws<InvalidDataException>(() => Loader.Load(Json("""{ "id": "a", "title": "A", "story": [] }""")));
    }

    [Theory]
    [InlineData("""{ "kind": "moneyAtLeast" }""")]
    [InlineData("""{ "kind": "moneyAtLeast", "amount": -5 }""")]
    [InlineData("""{ "kind": "atServer" }""")]
    [InlineData("""{ "kind": "rootOnServer", "hostname": " " }""")]
    public void A_condition_without_what_it_needs_is_refused(string condition)
    {
        Assert.Throws<InvalidDataException>(() => Loader.Load(Json(
            $$"""{ "id": "a", "title": "A", "story": ["x"], "conditions": [ {{condition}} ] }""")));
    }
}
