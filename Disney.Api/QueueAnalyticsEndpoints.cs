using Disney.Application;
using Microsoft.AspNetCore.OutputCaching;

namespace Disney.Api;

internal static class QueueAnalyticsEndpoints
{
    public static IEndpointRouteBuilder MapQueueAnalyticsEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/parks/{parkId:long:min(1)}")
            .WithTags("Queue analytics");

        group.MapGet(
                "/wait-times/current",
                async (
                    long parkId,
                    IQueueAnalyticsService analytics,
                    CancellationToken cancellationToken) =>
                    Results.Ok(await analytics.GetCurrentWaitTimesAsync(
                        parkId,
                        cancellationToken)))
            .WithName("GetCurrentWaitTimes")
            .WithSummary("Gets the most recent wait time for each active attraction")
            .CacheOutput("current-waits");

        group.MapGet(
                "/analytics/wait-times/weekday-hourly",
                async (
                    long parkId,
                    long? attractionId,
                    IQueueAnalyticsService analytics,
                    CancellationToken cancellationToken) =>
                    await ExecuteWithAttractionValidation(
                        attractionId,
                        () => analytics.GetWeekdayWaitTimePatternsAsync(
                            parkId,
                            attractionId,
                            cancellationToken)))
            .WithName("GetWeekdayHourlyWaitPatterns")
            .WithSummary("Gets hourly wait-time patterns grouped by weekday")
            .CacheOutput("analytics");

        group.MapGet(
                "/analytics/closures/weekday-hourly",
                async (
                    long parkId,
                    long? attractionId,
                    IQueueAnalyticsService analytics,
                    CancellationToken cancellationToken) =>
                    await ExecuteWithAttractionValidation(
                        attractionId,
                        () => analytics.GetWeekdayClosurePatternsAsync(
                            parkId,
                            attractionId,
                            cancellationToken)))
            .WithName("GetWeekdayHourlyClosurePatterns")
            .WithSummary("Gets hourly closure frequency grouped by weekday")
            .CacheOutput("analytics");

        return endpoints;
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
