using System.Text.Json.Nodes;

namespace NullByte.Core.Persistence.Migrations;

/// <summary>
/// Migrates version 7 saves to version 8 by adding the story progression.
/// Games from before the journey existed already have every feature, so they stay unrestricted.
/// </summary>
public sealed class V7ToV8Migration : ISaveMigration
{
    public int FromVersion => 7;

    public void Migrate(JsonObject save)
    {
        ArgumentNullException.ThrowIfNull(save);

        save["progression"] = new JsonObject
        {
            ["unrestricted"] = true,
            ["completedChapters"] = new JsonArray()
        };
    }
}
