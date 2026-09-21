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
                    await ExecuteAsync(
                        () => walkingTimeService.EstimateAsync(
                            parkId,
                            fromAttractionId,
                            toAttractionId,
                            cancellationToken)))
            .WithName("EstimateWalkingTime")
            .WithSummary("Estimates walking time between two attractions")
            .WithTags("Park routing");

        return endpoints;
    }

    private static async Task<IResult> ExecuteAsync(
        Func<Task<WalkingTimeEstimateResult?>> execute)
    {
        try
        {
            var estimate = await execute();
            return estimate is null
                ? Results.NotFound()
                : Results.Ok(estimate);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [exception.ParamName ?? "walkingTime"] = [exception.Message]
            });
        }
    }
}
