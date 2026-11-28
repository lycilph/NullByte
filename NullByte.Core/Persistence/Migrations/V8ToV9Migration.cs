using System.Text.Json.Nodes;

namespace NullByte.Core.Persistence.Migrations;

/// <summary>Adds the persisted set of narrative discoveries to existing saves.</summary>
public sealed class V8ToV9Migration : ISaveMigration
{
    public int FromVersion => 8;

    public void Migrate(JsonObject save)
    {
        ArgumentNullException.ThrowIfNull(save);

        var narrative = save["narrative"] as JsonObject
            ?? throw new InvalidDataException("The version 8 save has no narrative state.");
        narrative["discoveredDiscoveries"] = new JsonArray();
    }
}
