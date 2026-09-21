using Disney.Domain;

namespace Disney.Application;

public sealed class WalkingTimeService(IWalkingTimeReader reader) : IWalkingTimeService
{
    private const decimal RouteDistanceMultiplier = 1.25m;
    private const decimal WalkingSpeedMetersPerSecond = 1.4m;
    private const string AlgorithmVersion = "haversine-route-factor-v1";

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

        if (!TryGetCoordinates(data, out var origin, out var destination))
        {
            return CreateResult(data, parkId, null, null, null);
        }

        var directDistanceMeters = ToWholeMeters(origin.DistanceToMeters(destination));
        var estimatedRouteDistanceMeters = ToWholeMeters(
            directDistanceMeters * (double)RouteDistanceMultiplier);
        var estimatedWalkingMinutes = estimatedRouteDistanceMeters == 0
            ? 0
            : Math.Max(
                1,
                (int)Math.Ceiling(
                    estimatedRouteDistanceMeters /
                    (double)WalkingSpeedMetersPerSecond /
                    60));

        return CreateResult(
            data,
            parkId,
            directDistanceMeters,
            estimatedRouteDistanceMeters,
            estimatedWalkingMinutes);
    }

    private static WalkingTimeEstimateResult CreateResult(
        WalkingTimeData data,
        long parkId,
        int? directDistanceMeters,
        int? estimatedRouteDistanceMeters,
        int? estimatedWalkingMinutes) =>
        new(
            parkId,
            data.FromAttractionId,
            data.FromAttractionName,
            data.ToAttractionId,
            data.ToAttractionName,
            directDistanceMeters.HasValue
                ? WalkingTimeEstimateStatus.Available
                : WalkingTimeEstimateStatus.CoordinatesUnavailable,
            directDistanceMeters,
            estimatedRouteDistanceMeters,
            estimatedWalkingMinutes,
            RouteDistanceMultiplier,
            WalkingSpeedMetersPerSecond,
            AlgorithmVersion);

    private static bool TryGetCoordinates(
        WalkingTimeData data,
        out GeoCoordinate origin,
        out GeoCoordinate destination)
    {
        origin = default;
        destination = default;

        if (data.FromLatitude is null ||
            data.FromLongitude is null ||
            data.ToLatitude is null ||
            data.ToLongitude is null)
        {
            return false;
        }

        origin = new GeoCoordinate(data.FromLatitude.Value, data.FromLongitude.Value);
        destination = new GeoCoordinate(data.ToLatitude.Value, data.ToLongitude.Value);
        return true;
    }

    private static int ToWholeMeters(double meters) =>
        checked((int)Math.Round(meters, MidpointRounding.AwayFromZero));

    private static void Validate(
        long parkId,
        long fromAttractionId,
        long toAttractionId)
    {
        if (parkId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(parkId),
                "Park id must be greater than zero.");
        }

        if (fromAttractionId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fromAttractionId),
                "Origin attraction id must be greater than zero.");
        }

        if (toAttractionId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(toAttractionId),
                "Destination attraction id must be greater than zero.");
        }
    }
}
