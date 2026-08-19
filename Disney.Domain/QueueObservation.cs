namespace Disney.Domain;

public sealed class QueueObservation
{
    public long Id { get; init; }
    public long CollectionRunId { get; init; }
    public long ParkId { get; init; }
    public long? LandId { get; init; }
    public long AttractionId { get; init; }
    public DateTimeOffset CollectedAt { get; init; }
    public DateTimeOffset ObservedAt { get; init; }
    public DateOnly ObservedLocalDate { get; init; }
    public TimeOnly ObservedLocalTime { get; init; }
    public short ObservedLocalHour { get; init; }
    public short ObservedSlotMinutes { get; init; }
    public short ObservedDayOfWeek { get; init; }
    public bool IsOpen { get; init; }
    public short? WaitMinutes { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
