namespace Disney.Domain;

public sealed class Park
{
    public long Id { get; init; }
    public int SourceParkId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Timezone { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
    public bool CollectionEnabled { get; init; } = true;
    public int CollectionIntervalMinutes { get; init; } = 5;
    public DateTimeOffset? LastCollectionStartedAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
