namespace Repositories
{
    public class Attraction
    {
        public int Id { get; set; }

        public int ParkId { get; set; }

        public int? CurrentLandId { get; set; }

        public int SourceRideId { get; set; }

        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }
    }
}
