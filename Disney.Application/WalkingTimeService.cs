using Disney.Domain;

namespace Disney.Application;

public sealed class WalkingTimeService(IWalkingTimeReader reader) : IWalkingTimeService
{
    private const decimal RouteDistanceMultiplier = 1.25m;
    private const decimal WalkingSpeedMetersPerSecond = 1.4m;
    private const string CoordinateAlgorithmVersion = "haversine-route-factor-v1";
    private const string GraphAlgorithmVersion = "park-graph-dijkstra-v1";

    public async Task<WalkingTimeEstimateResult?> EstimateAsync(
        long parkId,
        long fromAttractionId,
        long toAttractionId,
        CancellationToken cancellationToken)
    {
        Validate(parkId, fromAttractionId, toAttractionId);
        var data = await reader.GetWalkingTimeDataAsync(
            parkId,
            fromAttractionId,
            toAttractionId,
            cancellationToken);

        if (data is null)
        {
            return null;
        }

        var directDistanceMeters = TryCalculateDirectDistance(data);
        if (data.FromRouteNodeId.HasValue && data.ToRouteNodeId.HasValue)
        {
            var route = FindGraphRoute(data);
            if (route is null)
            {
                return CreateUnavailableResult(
                    data,
                    parkId,
                    WalkingTimeEstimateStatus.RouteUnavailable,
                    directDistanceMeters,
                    WalkingRouteSource.ParkGraph,
                    1m,
                    GraphAlgorithmVersion);
            }

            return CreateAvailableResult(
                data,
                parkId,
                directDistanceMeters,
                route.DistanceMeters,
                WalkingRouteSource.ParkGraph,
                route.NodeIds,
                GraphAlgorithmVersion);
        }

        if (directDistanceMeters is null)
        {
            return CreateUnavailableResult(
                data,
                parkId,
                WalkingTimeEstimateStatus.CoordinatesUnavailable,
                null,
                null,
                RouteDistanceMultiplier,
                CoordinateAlgorithmVersion);
        }

        var estimatedRouteDistanceMeters = ToWholeMeters(
            directDistanceMeters.Value * (double)RouteDistanceMultiplier);

        return CreateAvailableResult(
            data,
            parkId,
            directDistanceMeters,
            estimatedRouteDistanceMeters,
            WalkingRouteSource.CoordinateEstimate,
            null,
            CoordinateAlgorithmVersion);
    }

    private static WalkingTimeEstimateResult CreateAvailableResult(
        WalkingTimeData data,
        long parkId,
        int? directDistanceMeters,
        int routeDistanceMeters,
        WalkingRouteSource routeSource,
        IReadOnlyList<long>? routeNodeIds,
        string algorithmVersion) =>
        new(
            parkId,
            data.FromAttractionId,
            data.FromAttractionName,
            data.ToAttractionId,
            data.ToAttractionName,
            WalkingTimeEstimateStatus.Available,
            directDistanceMeters,
            routeDistanceMeters,
            CalculateWalkingMinutes(routeDistanceMeters),
            routeSource,
            routeNodeIds,
            routeSource == WalkingRouteSource.CoordinateEstimate
                ? RouteDistanceMultiplier
                : 1m,
            WalkingSpeedMetersPerSecond,
            algorithmVersion);

    private static WalkingTimeEstimateResult CreateUnavailableResult(
        WalkingTimeData data,
        long parkId,
        WalkingTimeEstimateStatus status,
        int? directDistanceMeters,
        WalkingRouteSource? routeSource,
        decimal routeDistanceMultiplier,
        string algorithmVersion) =>
        new(
            parkId,
            data.FromAttractionId,
            data.FromAttractionName,
            data.ToAttractionId,
            data.ToAttractionName,
            status,
            directDistanceMeters,
            null,
            null,
            routeSource,
            null,
            routeDistanceMultiplier,
            WalkingSpeedMetersPerSecond,
            algorithmVersion);

    private static int? TryCalculateDirectDistance(WalkingTimeData data)
    {
        if (data.FromLatitude is null ||
            data.FromLongitude is null ||
            data.ToLatitude is null ||
            data.ToLongitude is null)
        {
            return null;
        }

        var origin = new GeoCoordinate(data.FromLatitude.Value, data.FromLongitude.Value);
        var destination = new GeoCoordinate(data.ToLatitude.Value, data.ToLongitude.Value);
        return ToWholeMeters(origin.DistanceToMeters(destination));
    }

    private static WalkableRoute? FindGraphRoute(WalkingTimeData data)
    {
        if (data.FromRouteNodeId is null || data.ToRouteNodeId is null)
        {
            return null;
        }

        var graph = new WalkableParkGraph(
            data.RouteNodeIds ?? [],
            data.RouteEdges ?? []);
        return graph.FindShortestRoute(
            data.FromRouteNodeId.Value,
            data.ToRouteNodeId.Value);
    }

    private static int CalculateWalkingMinutes(int distanceMeters) =>
        distanceMeters == 0
            ? 0
            : Math.Max(
                1,
                (int)Math.Ceiling(
                    distanceMeters /
                    WalkingSpeedMetersPerSecond /
                    60m));

    private static int ToWholeMeters(double meters) =>
        checked((int)Math.Round(meters, MidpointRounding.AwayFromZero));

    private static void Validate(
        long parkId,
        long fromAttractionId,
        long toAttractionId)
    {
        RequestGuard.RequireParkIdentifier(parkId);
        RequestGuard.RequirePositiveIdentifier(
            fromAttractionId,
            nameof(fromAttractionId),
            "Origin attraction ID");
        RequestGuard.RequirePositiveIdentifier(
            toAttractionId,
            nameof(toAttractionId),
            "Destination attraction ID");
    }
}
