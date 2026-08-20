using Disney.Domain;

namespace Disney.Application;

public sealed class AdminPark
{
    public long Id { get; init; }
    public int SourceParkId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Timezone { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public bool CollectionEnabled { get; init; }
    public int CollectionIntervalMinutes { get; init; }
    public DateTimeOffset? LastCollectionStartedAt { get; init; }
    public DateTimeOffset? LastCollectionCompletedAt { get; init; }
    public bool? LastCollectionSucceeded { get; init; }
    public string? LastCollectionError { get; init; }
    public int AttractionCount { get; init; }
    public long ObservationCount { get; init; }
}

public sealed class AdminLand
{
    public long Id { get; init; }
    public long ParkId { get; init; }
    public int SourceLandId { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}

public sealed class AdminAttraction
{
    public long Id { get; init; }
    public long ParkId { get; init; }
    public long? CurrentLandId { get; init; }
    public string? LandName { get; init; }
    public int SourceRideId { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public int? DurationMinutes { get; init; }
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }
}

public sealed class AdminCollectionRun
{
    public long Id { get; init; }
    public long ParkId { get; init; }
    public string ParkName { get; init; } = string.Empty;
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string TriggerSource { get; init; } = string.Empty;
    public int ObservationCount { get; init; }
}

public sealed class AdminObservation
{
    public long Id { get; init; }
    public long ParkId { get; init; }
    public string ParkName { get; init; } = string.Empty;
    public long AttractionId { get; init; }
    public string AttractionName { get; init; } = string.Empty;
    public long? LandId { get; init; }
    public string? LandName { get; init; }
    public DateTimeOffset ObservedAt { get; init; }
    public bool IsOpen { get; init; }
    public short? WaitMinutes { get; init; }
    public bool IsValid { get; init; }
    public string? InvalidReason { get; init; }
    public string TriggerSource { get; init; } = string.Empty;
}

public sealed record SaveParkCommand(
    int SourceParkId,
    string Name,
    string Timezone,
    bool IsActive,
    bool CollectionEnabled,
    int CollectionIntervalMinutes);

public sealed record SaveLandCommand(
    long ParkId,
    int SourceLandId,
    string Name,
    bool IsActive);

public sealed record SaveAttractionCommand(
    long ParkId,
    long? CurrentLandId,
    int SourceRideId,
    string Name,
    bool IsActive,
    int? DurationMinutes,
    decimal? Latitude,
    decimal? Longitude);

public sealed record ManualObservationCommand(
    long AttractionId,
    DateTimeOffset ObservedAt,
    bool IsOpen,
    int? WaitMinutes);

public interface IAdminRepository
{
    Task<IReadOnlyList<AdminPark>> GetParksAsync(CancellationToken cancellationToken);
    Task<AdminPark> CreateParkAsync(SaveParkCommand command, CancellationToken cancellationToken);
    Task<AdminPark?> UpdateParkAsync(long parkId, SaveParkCommand command, CancellationToken cancellationToken);
    Task<Park?> GetParkAsync(long parkId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminLand>> GetLandsAsync(long parkId, CancellationToken cancellationToken);
    Task<AdminLand> CreateLandAsync(SaveLandCommand command, CancellationToken cancellationToken);
    Task<AdminLand?> UpdateLandAsync(long landId, SaveLandCommand command, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminAttraction>> GetAttractionsAsync(long parkId, CancellationToken cancellationToken);
    Task<AdminAttraction> CreateAttractionAsync(SaveAttractionCommand command, CancellationToken cancellationToken);
    Task<AdminAttraction?> UpdateAttractionAsync(long attractionId, SaveAttractionCommand command, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminCollectionRun>> GetCollectionRunsAsync(long? parkId, int limit, CancellationToken cancellationToken);
    Task<Park?> GetParkForRunAsync(long runId, CancellationToken cancellationToken);
    Task SetCollectionRunTriggerAsync(long runId, string triggerSource, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminObservation>> GetObservationsAsync(long parkId, long? attractionId, bool includeInvalid, int limit, CancellationToken cancellationToken);
    Task<AdminObservation> CreateManualObservationAsync(ManualObservationCommand command, CancellationToken cancellationToken);
    Task<bool> SetObservationValidityAsync(long observationId, bool isValid, string? reason, CancellationToken cancellationToken);
    Task<int> PurgeObservationsAsync(long parkId, long? attractionId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
}
