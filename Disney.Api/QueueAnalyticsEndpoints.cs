using Disney.Application;
using Microsoft.AspNetCore.OutputCaching;

namespace Disney.Api;

internal static class QueueAnalyticsEndpoints
{
    public static IEndpointRouteBuilder MapQueueAnalyticsEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var parkEndpoints = endpoints.MapGroup("/api/v1/parks/{parkId:long:min(1)}")
            .WithTags("Queue analytics");

        MapCurrentWaitTimesEndpoint(parkEndpoints);
        MapWaitTimePatternsEndpoint(parkEndpoints);
        MapClosurePatternsEndpoint(parkEndpoints);
        return endpoints;
    }

    private static void MapCurrentWaitTimesEndpoint(RouteGroupBuilder parkEndpoints)
    {
        parkEndpoints.MapGet(
            "/wait-times/current",
            async (
                long parkId,
                IQueueAnalyticsService analyticsService,
                CancellationToken cancellationToken) =>
                Results.Ok(await analyticsService.GetCurrentWaitTimesAsync(
                    parkId,
                    cancellationToken)))
            .WithName("GetCurrentWaitTimes")
            .WithSummary("Gets the most recent wait time for each active attraction")
            .CacheOutput("current-waits");
    }

    private static void MapWaitTimePatternsEndpoint(RouteGroupBuilder parkEndpoints)
    {
        parkEndpoints.MapGet(
            "/analytics/wait-times/weekday-hourly",
            async (
                long parkId,
                long? attractionId,
                IQueueAnalyticsService analyticsService,
                CancellationToken cancellationToken) =>
                await ExecuteWithAttractionValidation(
                    attractionId,
                    () => analyticsService.GetWeekdayWaitTimePatternsAsync(
                        parkId,
                        attractionId,
                        cancellationToken)))
            .WithName("GetWeekdayHourlyWaitPatterns")
            .WithSummary("Gets hourly wait-time patterns grouped by weekday")
            .CacheOutput("analytics");
    }

    private static void MapClosurePatternsEndpoint(RouteGroupBuilder parkEndpoints)
    {
        parkEndpoints.MapGet(
            "/analytics/closures/weekday-hourly",
            async (
                long parkId,
                long? attractionId,
                IQueueAnalyticsService analyticsService,
                CancellationToken cancellationToken) =>
                await ExecuteWithAttractionValidation(
                    attractionId,
                    () => analyticsService.GetWeekdayClosurePatternsAsync(
                        parkId,
                        attractionId,
                        cancellationToken)))
            .WithName("GetWeekdayHourlyClosurePatterns")
            .WithSummary("Gets hourly closure frequency grouped by weekday")
            .CacheOutput("analytics");
    }

    private static async Task<IResult> ExecuteWithAttractionValidation<T>(
        long? attractionId,
        Func<Task<T>> execute)
    {
        if (attractionId <= 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["attractionId"] = ["Attraction id must be greater than zero."]
            });
        }

        return Results.Ok(await execute());
    }
}
