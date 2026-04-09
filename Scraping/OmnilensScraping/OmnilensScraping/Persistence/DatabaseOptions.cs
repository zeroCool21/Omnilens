namespace OmnilensScraping.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";
    public const string DefaultFilePath = "App_Data/omnilens-mvp.db";

    public string FilePath { get; set; } = DefaultFilePath;
    public bool AutoCreateSchema { get; set; } = true;
    public bool SeedSourcesFromRegistry { get; set; } = true;
}
