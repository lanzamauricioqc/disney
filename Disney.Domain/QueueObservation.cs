namespace Disney.Domain;

public sealed class QueueObservation
{
    public long Id { get; set; }
    public long CollectionRunId { get; set; }
    public long ParkId { get; set; }
    public long? LandId { get; set; }
    public long AttractionId { get; set; }
    public DateTimeOffset CollectedAt { get; set; }
    public DateTimeOffset ObservedAt { get; set; }
    public DateOnly ObservedLocalDate { get; set; }
    public TimeOnly ObservedLocalTime { get; set; }
    public short ObservedLocalHour { get; set; }
    public short ObservedSlotMinutes { get; set; }
    public short ObservedDayOfWeek { get; set; }
    public bool IsOpen { get; set; }
    public short? WaitMinutes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
