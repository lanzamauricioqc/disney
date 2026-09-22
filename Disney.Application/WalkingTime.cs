using Disney.Domain;

namespace Disney.Application;

public enum WalkingTimeEstimateStatus
{
    Available,
    CoordinatesUnavailable,
    RouteUnavailable
}

public enum WalkingRouteSource
{
    ParkGraph,
    CoordinateEstimate
}

public sealed record WalkingTimeData(
    long FromAttractionId,
    string FromAttractionName,
    decimal? FromLatitude,
    decimal? FromLongitude,
    long ToAttractionId,
    string ToAttractionName,
    decimal? ToLatitude,
    decimal? ToLongitude,
    long? FromRouteNodeId = null,
    long? ToRouteNodeId = null,
    IReadOnlyList<long>? RouteNodeIds = null,
    IReadOnlyList<WalkableRouteEdge>? RouteEdges = null);

public sealed record WalkingTimeEstimateResult(
    long ParkId,
    long FromAttractionId,
    string FromAttractionName,
    long ToAttractionId,
    string ToAttractionName,
    WalkingTimeEstimateStatus Status,
    int? DirectDistanceMeters,
    int? EstimatedRouteDistanceMeters,
    int? EstimatedWalkingMinutes,
    WalkingRouteSource? RouteSource,
    IReadOnlyList<long>? RouteNodeIds,
    decimal RouteDistanceMultiplier,
    decimal WalkingSpeedMetersPerSecond,
    string AlgorithmVersion);

public interface IWalkingTimeReader
{
    Task<WalkingTimeData?> GetWalkingTimeDataAsync(
        long parkId,
        long fromAttractionId,
        long toAttractionId,
        CancellationToken cancellationToken);
}

public interface IWalkingTimeService
{
    Task<WalkingTimeEstimateResult?> EstimateAsync(
        long parkId,
        long fromAttractionId,
        long toAttractionId,
        CancellationToken cancellationToken);
}
