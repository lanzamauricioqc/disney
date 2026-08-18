namespace Disney.Domain;

public sealed class Park
{
    public long Id { get; set; }
    public int SourceParkId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Timezone { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
