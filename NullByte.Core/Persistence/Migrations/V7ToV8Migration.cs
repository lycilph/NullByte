using System.Text.Json.Nodes;

namespace NullByte.Core.Persistence.Migrations;

/// <summary>
/// Migrates pre-narrative saves to Freeplay mode so existing players never have to replay the tutorial.
/// </summary>
public sealed class V7ToV8Migration : ISaveMigration
{
    public int FromVersion => 7;

    public void Migrate(JsonObject save)
    {
        ArgumentNullException.ThrowIfNull(save);

        save["narrative"] = new JsonObject
        {
            ["mode"] = "Freeplay",
            ["unlockedCapabilities"] = new JsonArray(),
            ["completedMilestones"] = new JsonArray(),
            ["deliveredMessages"] = new JsonArray()
        };
    }
}
