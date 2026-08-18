namespace Disney.Application;

public sealed record CollectionResult(
    long CollectionRunId,
    int LandCount,
    int RideCount,
    int ObservationCount,
    int DeactivatedLandCount,
    int DeactivatedAttractionCount);
