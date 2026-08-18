namespace Disney.Application;

public sealed record QueueTimesSnapshot(
    IReadOnlyList<QueueLandSnapshot> Lands,
    IReadOnlyList<QueueRideSnapshot> Rides);

public sealed record QueueLandSnapshot(
    int SourceLandId,
    string Name,
    IReadOnlyList<QueueRideSnapshot> Rides);

public sealed record QueueRideSnapshot(
    int SourceRideId,
    string Name,
    bool IsOpen,
    int WaitMinutes,
    DateTimeOffset ObservedAt);
