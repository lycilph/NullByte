namespace NullByte.Scripting;

/// <summary>Key-value store for durable script state. Serialized as JSON.</summary>
public sealed class ScriptData
{
    public Dictionary<string, double> Numbers { get; set; } = [];
    public Dictionary<string, string> Strings { get; set; } = [];

    public double GetNum(string key, double def = 0)
        => Numbers.TryGetValue(key, out var v) ? v : def;
    public void SetNum(string key, double v) => Numbers[key] = v;

    public string GetStr(string key, string def = "")
        => Strings.TryGetValue(key, out var v) ? v : def;
    public void SetStr(string key, string v) => Strings[key] = v;
}