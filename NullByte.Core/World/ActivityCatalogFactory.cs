using NullByte.Core.Domain;

namespace NullByte.Core.World;

public static class ActivityCatalogFactory
{
    private static readonly string CatalogPath = Path.Combine("Data", "activities.json");

    /// <summary>Loads the activity catalog that ships with the game.</summary>
    public static ActivityCatalog CreateDefault()
    {
        var path = Path.Combine(AppContext.BaseDirectory, CatalogPath);
        if (!File.Exists(path))
            throw new FileNotFoundException("The activity catalog could not be found.", path);

        return new ActivityCatalogLoader().Load(File.ReadAllText(path));
    }
}