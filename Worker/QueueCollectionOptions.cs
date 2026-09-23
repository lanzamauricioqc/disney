namespace Disney.Worker;

public sealed class QueueCollectionOptions
{
    public const string SectionName = "QueueCollection";

    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(1);
}
