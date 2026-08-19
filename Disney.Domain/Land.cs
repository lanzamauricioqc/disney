namespace Disney.Domain;

public sealed class Land
{
    public long Id { get; init; }
    public long ParkId { get; init; }
    public int SourceLandId { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
