using NullByte.Core.Domain.Progression;

namespace NullByte.Core.World;

public static class JourneyFactory
{
    private static readonly string JourneyPath = Path.Combine("Data", "journey.json");

    /// <summary>Loads the journey that ships with the game.</summary>
    public static Journey CreateDefault()
    {
        var path = Path.Combine(AppContext.BaseDirectory, JourneyPath);
        if (!File.Exists(path))
            throw new FileNotFoundException("The journey definition could not be found.", path);

        return new JourneyLoader().Load(File.ReadAllText(path));
    }
}
