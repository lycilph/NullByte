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
    private const string defaultBasePath = "scripts";
    public const string StateFile = "scripts.json";
    public static string SourcePath(string name, string base_path = "") 
        => Path.Combine(string.IsNullOrWhiteSpace(base_path) ? defaultBasePath : base_path, name + ".cs");
    public static string LibPath(string name, string base_path = "") 
        => Path.Combine(string.IsNullOrWhiteSpace(base_path) ? defaultBasePath : base_path, "lib", name + ".cs");
    public static string StatePath(string base_path = "") 
        => Path.Combine(string.IsNullOrWhiteSpace(base_path) ? defaultBasePath : base_path, StateFile);

    public static string LibFolder(string base_path = "")
        => Path.Combine(string.IsNullOrWhiteSpace(base_path) ? defaultBasePath : base_path, "lib");

    public static void Save(IEnumerable<ScriptInstance> scripts, string base_path = "")
    {
        var script_dir = string.IsNullOrWhiteSpace(base_path) ? defaultBasePath : base_path;
        Directory.CreateDirectory(script_dir);

        var entries = scripts.Select(s => new ScriptSaveEntry
        {
            Name = s.Name,
            Source = s.Source,
            Data = s.Data,
            Running = s.IsRunning,
        }).ToList();
        File.WriteAllText(StatePath(base_path), JsonSerializer.Serialize(entries));
    }

    public static List<ScriptSaveEntry> Load(string base_path = "")
    {
        if (!File.Exists(StatePath(base_path)))
            return [];
        try
        {
            return JsonSerializer.Deserialize<List<ScriptSaveEntry>>(
                File.ReadAllText(StatePath(base_path))) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>Loads and guard-validates one script. Throws on bad names,
    /// missing files, and banned patterns.</summary>
    public static string LoadScript(string name, string base_path = "")
    {
        if (!ScriptingSystem.FileNameRegex.IsMatch(name))
            throw new ScriptCompileException(
                $"Invalid script name '{name}' — allowed: letters, digits, dashes, underscores.");
        string path = SourcePath(name, base_path);
        if (!File.Exists(path))
            throw new ScriptCompileException($"Script '{name}' not found at {path}.");
        string source = File.ReadAllText(path);
        ScriptGuard.Validate(source);
        return source;
    }

    /// <summary>Loads and guard-validates one library. Throws on bad names,
    /// missing files, and banned patterns.</summary>
    public static string LoadLibrary(string name, string base_path = "")
    {
        if (!ScriptingSystem.FileNameRegex.IsMatch(name))
            throw new ScriptCompileException(
                $"Invalid library name '{name}' — allowed: letters, digits, dashes, underscores.");
        string path = LibPath(name, base_path);
        if (!File.Exists(path))
            throw new ScriptCompileException($"Library '{name}' not found at {path}.");
        string source = File.ReadAllText(path);
        ScriptGuard.Validate(source);
        return source;
    }
}