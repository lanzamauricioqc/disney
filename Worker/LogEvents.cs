using Microsoft.Extensions.Logging;

namespace WorkerModels;

internal static class LogEvents
{
    public static readonly EventId WorkerStarted = new(1000, nameof(WorkerStarted));
    public static readonly EventId CollectionCycleStarted = new(1001, nameof(CollectionCycleStarted));
    public static readonly EventId CollectionCycleCompleted = new(1002, nameof(CollectionCycleCompleted));
    public static readonly EventId CollectionCycleFailed = new(1003, nameof(CollectionCycleFailed));
    public static readonly EventId WorkerStopping = new(1004, nameof(WorkerStopping));

    public static readonly EventId CollectionJobStarted = new(2000, nameof(CollectionJobStarted));
    public static readonly EventId ParkSkipped = new(2001, nameof(ParkSkipped));
    public static readonly EventId ParkCollectionFailed = new(2002, nameof(ParkCollectionFailed));
    public static readonly EventId CollectionJobCompleted = new(2003, nameof(CollectionJobCompleted));
    public static readonly EventId CollectionJobCanceled = new(2004, nameof(CollectionJobCanceled));

    public static readonly EventId CollectionRunStarted = new(3000, nameof(CollectionRunStarted));
    public static readonly EventId QueueTimesReceived = new(3001, nameof(QueueTimesReceived));
    public static readonly EventId RideObserved = new(3002, nameof(RideObserved));
    public static readonly EventId CollectionRunCompleted = new(3003, nameof(CollectionRunCompleted));
    public static readonly EventId CollectionRunFailed = new(3004, nameof(CollectionRunFailed));
    public static readonly EventId CollectionRunCanceled = new(3005, nameof(CollectionRunCanceled));

    public static readonly EventId QueueTimesRequestStarted = new(4000, nameof(QueueTimesRequestStarted));
    public static readonly EventId QueueTimesRequestCompleted = new(4001, nameof(QueueTimesRequestCompleted));
    public static readonly EventId QueueTimesRequestRejected = new(4002, nameof(QueueTimesRequestRejected));
}
