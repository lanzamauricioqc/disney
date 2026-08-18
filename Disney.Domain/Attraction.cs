namespace Disney.Domain;

public sealed class Attraction
{
    public long Id { get; set; }
    public long ParkId { get; set; }
    public long? CurrentLandId { get; set; }
    public int SourceRideId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int? DurationMinutes { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
