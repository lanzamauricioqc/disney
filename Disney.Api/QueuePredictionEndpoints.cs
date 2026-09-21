using Disney.Application;
using Microsoft.AspNetCore.OutputCaching;

namespace Disney.Api;

internal static class QueuePredictionEndpoints
{
    public static IEndpointRouteBuilder MapQueuePredictionEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            "/api/v1/parks/{parkId:long:min(1)}/attractions/" +
            "{attractionId:long:min(1)}/wait-time-prediction",
            async (
                long parkId,
                long attractionId,
                DateTimeOffset at,
                IQueuePredictionService predictionService,
                CancellationToken cancellationToken) =>
                await ExecutePredictionAsync(
                    () => predictionService.PredictWaitTimeAsync(
                        parkId,
                        attractionId,
                        at,
                        cancellationToken)))
            .WithName("PredictAttractionWaitTime")
            .WithSummary("Predicts an attraction wait time later in the day")
            .WithTags("Queue prediction")
            .CacheOutput("analytics");

        return endpoints;
    }

    private static async Task<IResult> ExecutePredictionAsync(
        Func<Task<WaitTimePredictionResult?>> execute)
    {
        try
        {
            var prediction = await execute();
            return prediction is null
                ? Results.NotFound()
                : Results.Ok(prediction);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [exception.ParamName ?? "prediction"] = [exception.Message]
            });
        }
    }
}
