namespace Repositories
{
    public class Land
    {
        public int Id { get; set; }

        public int ParkId { get; set; }

        public int SourceLandId { get; set; }

        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }
    }
}
