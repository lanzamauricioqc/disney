namespace Disney.Application;

/// <summary>
/// Shared argument checks for the identifiers that every park-facing
/// application service receives, so the same rule is stated once.
/// </summary>
internal static class RequestGuard
{
    public static void RequirePositiveIdentifier(
        long identifier,
        string parameterName,
        string description)
    {
        if (identifier <= 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"{description} must be greater than zero.");
        }
    }

    public static void RequireParkIdentifier(long parkId) =>
        RequirePositiveIdentifier(parkId, nameof(parkId), "Park ID");

    public static void RequireAttractionIdentifier(long attractionId) =>
        RequirePositiveIdentifier(attractionId, nameof(attractionId), "Attraction ID");

    public static void RequireOptionalAttractionIdentifier(long? attractionId)
    {
        if (attractionId is not null)
        {
            RequireAttractionIdentifier(attractionId.Value);
        }
    }
}
