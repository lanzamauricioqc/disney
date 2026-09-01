namespace Disney.Worker;

internal sealed class DatabaseStartupOptions
{
    public const string SectionName = "DatabaseStartup";

    public int MaxAttempts { get; set; } = 10;

    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(2);

    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);
}
