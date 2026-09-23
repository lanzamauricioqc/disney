using Disney.Application;
using Microsoft.AspNetCore.OutputCaching;

namespace Disney.Api;

internal static class QueueAnalyticsEndpoints
{
    private const string AttractionIdentifierRequired =
        "Attraction ID must be greater than zero.";

    public static IEndpointRouteBuilder MapQueueAnalyticsEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var parkEndpoints = endpoints.MapGroup("/api/v1/parks/{parkId:long:min(1)}")
            .WithTags("Queue analytics");

        MapCurrentWaitTimesEndpoint(parkEndpoints);
        MapDailyWaitTimeHistoryEndpoint(parkEndpoints);
        MapHistoricalWaitTimesEndpoint(parkEndpoints);
        MapQuarterHourlyWaitTimePatternsEndpoint(parkEndpoints);
        MapQuarterHourlyClosurePatternsEndpoint(parkEndpoints);
        MapDailyParkWaitTimesEndpoint(endpoints);
        return endpoints;
    }

    private static void MapDailyParkWaitTimesEndpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            "/api/v1/analytics/parks/daily",
            async (
                DateOnly? weekStart,
                IQueueAnalyticsService analyticsService,
                CancellationToken cancellationToken) =>
                await EndpointResults.ExecuteOkAsync(
                    () => analyticsService.GetDailyParkWaitTimesAsync(
                        weekStart,
                        cancellationToken),
                    "weekStart"))
            .WithName("GetDailyParkWaitTimes")
            .WithSummary("Compares daily average wait times across parks for one week")
            .WithTags("Queue analytics")
            .CacheOutput("analytics");
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

    private static void MapHistoricalWaitTimesEndpoint(RouteGroupBuilder parkEndpoints)
    {
        parkEndpoints.MapGet(
            "/analytics/wait-times/history",
            async (
                long parkId,
                long attractionId,
                DateTimeOffset from,
                DateTimeOffset to,
                IQueueAnalyticsService analyticsService,
                CancellationToken cancellationToken) =>
                await ExecuteHistoricalWaitTimesQuery(
                    attractionId,
                    from,
                    to,
                    () => analyticsService.GetHistoricalWaitTimesAsync(
                        parkId,
                        attractionId,
                        from,
                        to,
                        cancellationToken)))
            .WithName("GetHistoricalWaitTimes")
            .WithSummary("Gets historical wait-time observations for an attraction and time range")
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
            return EndpointResults.ValidationProblem(
                "attractionId",
                AttractionIdentifierRequired);
        }

        return Results.Ok(await execute());
    }

    private static async Task<IResult> ExecuteHistoricalWaitTimesQuery<T>(
        long attractionId,
        DateTimeOffset from,
        DateTimeOffset to,
        Func<Task<T>> execute)
    {
        var errors = new Dictionary<string, string[]>();

        if (attractionId <= 0)
        {
            errors["attractionId"] = [AttractionIdentifierRequired];
        }

        if (from >= to)
        {
            errors["from"] = ["The start must be before the end."];
        }
        else if (to - from > QueueAnalyticsService.MaximumHistoricalQueryWindow)
        {
            errors["to"] =
            [
                $"The query range cannot exceed " +
                $"{QueueAnalyticsService.MaximumHistoricalQueryWindow.TotalDays} days."
            ];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        return Results.Ok(await execute());
    }
}
