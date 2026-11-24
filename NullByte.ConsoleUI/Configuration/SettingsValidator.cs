namespace NullByte.ConsoleUI.Configuration;

public static class SettingsValidator
{
    public static void Validate(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var errors = new List<string>();

        ValidateSaveDirectory(settings, errors);
        ValidateEditor(settings, errors);
        ValidateDevelopment(settings, errors);

        if (errors.Count > 0)
            throw new SettingsValidationException(errors);
    }

    private static void ValidateSaveDirectory(AppSettings settings, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(settings.SaveDirectory))
            errors.Add("SaveDirectory cannot be empty.");
    }

    private static void ValidateEditor(AppSettings settings, ICollection<string> errors)
    {
        if (settings.Editor is null)
        {
            errors.Add("Editor settings cannot be null.");
            return;
        }

        if (settings.Editor.Port is <= 0 or > 65535)
            errors.Add("Editor.Port must be between 1 and 65535.");

        if (settings.Editor.ConnectionFilePath is not null && string.IsNullOrWhiteSpace(settings.Editor.ConnectionFilePath))
            errors.Add("Editor.ConnectionFilePath cannot be empty.");
    }

    private static void ValidateDevelopment(AppSettings settings, ICollection<string> errors)
    {
        if (settings.Development is null)
        {
            errors.Add("Development settings cannot be null.");
            return;
        }

        var gameSpeed = settings.Development.GameSpeedMultiplier;

        if (!double.IsFinite(gameSpeed) || gameSpeed <= 0.0)
            errors.Add("Development.GameSpeedMultiplier must be a finite number greater than 0.");
    }
}