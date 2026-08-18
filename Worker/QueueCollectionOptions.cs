namespace Disney.Worker;

public sealed class QueueCollectionOptions
{
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(5);
}
