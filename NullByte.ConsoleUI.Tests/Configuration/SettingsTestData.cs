namespace NullByte.ConsoleUI.Tests.Configuration;

internal static class SettingsTestData
{
    /// <summary>Where saves go when no settings say otherwise.</summary>
    public static string DefaultSaveDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "NullByte",
        "saves");
}
