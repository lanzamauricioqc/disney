namespace Repositories
{
    public class Park
    {
        public int Id { get; set; }

        public int SourceParkId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Timezone { get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }
    }
}