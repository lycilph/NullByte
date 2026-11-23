using System.Text.Json;

namespace NullByte.ConsoleUI.Configuration;

public static class SettingsFileLoader
{
    private const string SettingsFileName = "settings.json";

    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    public static AppSettings Load() => Load(GetSettingsFilePath());

    public static AppSettings Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(path);

            return JsonSerializer.Deserialize<AppSettings>(json, jsonOptions)
                   ?? throw new InvalidOperationException($"Settings file '{path}' contains no settings.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Settings file '{path}' contains invalid JSON.", ex);
        }
    }

    public static string WriteDefault() => WriteDefault(GetSettingsFilePath());

    public static string WriteDefault(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (File.Exists(path))
            throw new InvalidOperationException($"Settings file '{path}' already exists.");

        var settings = new AppSettings();
        var json = JsonSerializer.Serialize(settings, jsonOptions);

        File.WriteAllText(path, json);

        return path;
    }

    public static string GetSettingsFilePath()
    {
        return Path.Combine(AppContext.BaseDirectory, SettingsFileName);
    }
}