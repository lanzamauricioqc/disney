namespace Disney.Application;

public static class QueueDataFreshness
{
    public static readonly TimeSpan MaximumLiveObservationAge =
        TimeSpan.FromMinutes(15);
}
