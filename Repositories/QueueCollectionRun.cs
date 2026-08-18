namespace Repositories
{
    public class QueueCollectionRun
    {
        public int Id { get; set; }

        public int ParkId { get; set; }

        public DateTimeOffset? StartedAt { get; set; }

        public DateTimeOffset? CompletedAt { get; set; }

        public bool Success { get; set; }

        public string? ErrorMessage { get; set; }
    }
}
