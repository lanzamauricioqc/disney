namespace Repositories
{
    public class QueueObservation
    {
        public int Id { get; set; }

        public int CollectionRunId { get; set; }

        public int ParkId { get; set; }

        public int? LandId { get; set; }

        public int? AttractionId { get; set; }

        public DateTimeOffset CollectedAt { get; set; }

        public DateOnly ObservedLocalDate { get; set; }

        public TimeOnly ObservedLocalTime { get; set; }

        public int ObservedLocalHour { get; set; }

        public int ObservedSlotMinutes { get; set; }

        public int ObservedDayOfWeek { get; set; }

        public bool IsOpen { get; set; }

        public int? WaitMinutes { get; set; }

        public DateTimeOffset? SourceLastUpdated { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
    }
}