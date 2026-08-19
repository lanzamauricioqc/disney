namespace Disney.Domain;

public sealed class Park
{
    public long Id { get; init; }
    public int SourceParkId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Timezone { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
