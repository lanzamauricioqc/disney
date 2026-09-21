namespace Disney.Domain;

public readonly record struct GeoCoordinate
{
    public GeoCoordinate(decimal latitude, decimal longitude)
    {
        if (latitude is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(
                nameof(latitude),
                "Latitude must be between -90 and 90 degrees.");
        }

        if (longitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(
                nameof(longitude),
                "Longitude must be between -180 and 180 degrees.");
        }

        Latitude = latitude;
        Longitude = longitude;
    }

    public decimal Latitude { get; }
    public decimal Longitude { get; }

    public double DistanceToMeters(GeoCoordinate destination)
    {
        const double EarthRadiusMeters = 6_371_000;
        var latitudeRadians = DegreesToRadians((double)Latitude);
        var destinationLatitudeRadians = DegreesToRadians((double)destination.Latitude);
        var latitudeDifference = destinationLatitudeRadians - latitudeRadians;
        var longitudeDifference = DegreesToRadians(
            (double)(destination.Longitude - Longitude));

        var haversine =
            Math.Pow(Math.Sin(latitudeDifference / 2), 2) +
            Math.Cos(latitudeRadians) *
            Math.Cos(destinationLatitudeRadians) *
            Math.Pow(Math.Sin(longitudeDifference / 2), 2);
        haversine = Math.Clamp(haversine, 0, 1);
        var angularDistance = 2 * Math.Atan2(
            Math.Sqrt(haversine),
            Math.Sqrt(1 - haversine));

        return EarthRadiusMeters * angularDistance;
    }

    private static double DegreesToRadians(double degrees) =>
        degrees * Math.PI / 180;
}
