using Disney.Application;

namespace Disney.Api;

internal static class WalkingTimeEndpoints
{
    public static IEndpointRouteBuilder MapWalkingTimeEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/v1/parks/{parkId:long:min(1)}/walking-time",
                async (
                    long parkId,
                    long fromAttractionId,
                    long toAttractionId,
                    IWalkingTimeService walkingTimeService,
                    CancellationToken cancellationToken) =>
                    await EndpointResults.ExecuteFoundAsync(
                        () => walkingTimeService.EstimateAsync(
                            parkId,
                            fromAttractionId,
                            toAttractionId,
                            cancellationToken),
                        "walkingTime"))
            .WithName("EstimateWalkingTime")
            .WithSummary("Estimates walking time between two attractions")
            .WithTags("Park routing");

        return endpoints;
    }
}
