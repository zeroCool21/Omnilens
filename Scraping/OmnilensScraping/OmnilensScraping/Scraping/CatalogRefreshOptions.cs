namespace OmnilensScraping.Scraping;

public class CatalogRefreshOptions
{
    public bool Enabled { get; set; } = true;
    public bool RunOnStartup { get; set; } = true;
    public int InitialDelaySeconds { get; set; } = 15;
    public int CheckIntervalMinutes { get; set; } = 180;
    public int SnapshotStaleAfterMinutes { get; set; } = 180;
    public string SnapshotRootDirectory { get; set; } = string.Empty;
}
