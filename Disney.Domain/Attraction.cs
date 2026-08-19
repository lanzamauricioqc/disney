namespace Disney.Domain;

public sealed class Attraction
{
    public long Id { get; init; }
    public long ParkId { get; init; }
    public long? CurrentLandId { get; init; }
    public int SourceRideId { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public int? DurationMinutes { get; init; }
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
