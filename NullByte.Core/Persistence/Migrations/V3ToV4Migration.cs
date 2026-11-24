using System.Text.Json.Nodes;

namespace NullByte.Core.Persistence.Migrations;

/// <summary>
/// Migrates version 3 saves to version 4: the player's single hacking experience becomes the
/// experience per skill, and there is an (empty) working life. A version 3 player was never
/// doing anything besides hacking, so there is no activity to carry over.
/// </summary>
public sealed class V3ToV4Migration : ISaveMigration
{
    public int FromVersion => 3;

    public void Migrate(JsonObject save)
    {
        ArgumentNullException.ThrowIfNull(save);

        if (save["player"] is JsonObject player && player["skills"] is null)
        {
            double hacking;

            try
            {
                hacking = player["hackingExperience"]?.GetValue<double>() ?? 0;
            }
            catch (Exception exception) when (exception is FormatException or InvalidOperationException)
            {
                throw new SaveFormatException("The save has no valid hacking experience.", exception);
            }

            player["skills"] = new JsonObject { ["hacking"] = hacking };
            player.Remove("hackingExperience");
        }

        if (save["employment"] is null)
        {
            save["employment"] = new JsonObject
            {
                ["jobs"] = new JsonArray(),
                ["reputation"] = new JsonArray()
            };
        }
    }
}