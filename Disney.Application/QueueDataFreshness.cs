namespace Disney.Application;

public static class QueueDataFreshness
{
    public static readonly TimeSpan MaximumLiveObservationAge =
        TimeSpan.FromMinutes(15);
}

/// <summary>
/// The single definition of how far back queue history is trusted and how
/// many samples a quarter-hour bucket needs before it can be used.
/// </summary>
public static class QueueHistoryWindow
{
    public const int LookbackMonths = 3;
    public const int MinimumSampleCount = 3;
}
