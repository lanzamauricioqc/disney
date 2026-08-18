namespace Disney.Domain;

public sealed class Land
{
    public long Id { get; set; }
    public long ParkId { get; set; }
    public int SourceLandId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
