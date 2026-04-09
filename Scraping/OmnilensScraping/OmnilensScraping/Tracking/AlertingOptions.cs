namespace OmnilensScraping.Tracking;

public sealed class AlertingOptions
{
    public bool Enabled { get; set; } = true;
    public bool RunOnStartup { get; set; } = true;
    public int InitialDelaySeconds { get; set; } = 10;
    public int CheckIntervalMinutes { get; set; } = 5;
    public int CooldownMinutes { get; set; } = 1440;
    public int MaxDeliveriesPerRun { get; set; } = 200;
}

