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
        MapDailyWaitTimeHistoryEndpoint(parkEndpoints);
        MapQuarterHourlyWaitTimePatternsEndpoint(parkEndpoints);
        MapQuarterHourlyClosurePatternsEndpoint(parkEndpoints);
        return endpoints;
    }

    private static void MapDailyWaitTimeHistoryEndpoint(RouteGroupBuilder parkEndpoints)
    {
        parkEndpoints.MapGet(
            "/analytics/wait-times/daily",
            async (
                long parkId,
                long attractionId,
                IQueueAnalyticsService analyticsService,
                CancellationToken cancellationToken) =>
                await ExecuteWithAttractionValidation(
                    attractionId,
                    () => analyticsService.GetDailyWaitTimeHistoryAsync(
                        parkId,
                        attractionId,
                        cancellationToken)))
            .WithName("GetDailyWaitTimeHistory")
            .WithSummary("Gets daily wait-time history for an attraction")
            .CacheOutput("analytics");
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

    private static void MapQuarterHourlyWaitTimePatternsEndpoint(
        RouteGroupBuilder parkEndpoints)
    {
        parkEndpoints.MapGet(
            "/analytics/wait-times/weekday-quarter-hourly",
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
            .WithName("GetWeekdayQuarterHourlyWaitPatterns")
            .WithSummary("Gets 15-minute wait-time patterns grouped by weekday")
            .CacheOutput("analytics");
    }

    private static void MapQuarterHourlyClosurePatternsEndpoint(
        RouteGroupBuilder parkEndpoints)
    {
        parkEndpoints.MapGet(
            "/analytics/closures/weekday-quarter-hourly",
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
            .WithName("GetWeekdayQuarterHourlyClosurePatterns")
            .WithSummary("Gets 15-minute closure frequency grouped by weekday")
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
