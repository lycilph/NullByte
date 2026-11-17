using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NullByte.Core.Persistence;

/// <summary>
/// Reads and writes saves as JSON, upgrading old versions on the way in.
/// </summary>
public sealed class SaveGameSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,

        // Player scripts are full of quotes and non-ASCII text. This is a local file, not HTML, so keep
        // them readable (and diffable) instead of turning every quote into \u0022.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,

        // The same bytes on every platform, so the golden-file test does not depend on the OS.
        NewLine = "\n",

        // A "null" for a property that is not nullable is a damaged save, not a missing value.
        RespectNullableAnnotations = true
    };

    private static readonly JsonNodeOptions NodeOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly SaveMigrator migrator;

    public SaveGameSerializer(SaveMigrator? migrator = null)
    {
        this.migrator = migrator ?? new SaveMigrator();
    }

    public string Serialize(SaveGame save)
    {
        ArgumentNullException.ThrowIfNull(save);

        return JsonSerializer.Serialize(save, Options);
    }

    /// <exception cref="SaveFormatException">The text is not a save this version can read.</exception>
    public SaveGame Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        try
        {
            if (JsonNode.Parse(json, NodeOptions) is not JsonObject root)
                throw new SaveFormatException("The save is not a JSON object.");

            migrator.Upgrade(root);

            return root.Deserialize<SaveGame>(Options)
                ?? throw new SaveFormatException("The save is empty.");
        }
        catch (JsonException exception)
        {
            throw new SaveFormatException($"The save is damaged: {exception.Message}", exception);
        }
    }
}
