using System.Text.Json;

namespace NullByte.Scripting;

public sealed class ScriptSaveEntry
{
    public string Name { get; set; } = "";
    public string Source { get; set; } = "";
    public ScriptData? Data { get; set; }
    public bool Running { get; set; }
}

public static class ScriptPersistence
{
    public const string Folder = "scripts";
    public const string StateFile = "scripts/scripts.json";

    public static string SourcePath(string name) => Path.Combine(Folder, name + ".cs");

    public static void Save(IEnumerable<ScriptInstance> scripts)
    {
        Directory.CreateDirectory(Folder);
        var entries = scripts.Select(s => new ScriptSaveEntry
        {
            Name = s.Name,
            Source = s.Source,
            Data = s.Data,
            Running = s.IsRunning,
        }).ToList();
        File.WriteAllText(StateFile, JsonSerializer.Serialize(entries));
    }

    public static List<ScriptSaveEntry> Load()
    {
        if (!File.Exists(StateFile))
            return [];
        try
        {
            return JsonSerializer.Deserialize<List<ScriptSaveEntry>>(
                       File.ReadAllText(StateFile)) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}