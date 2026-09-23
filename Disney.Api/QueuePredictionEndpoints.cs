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
                await EndpointResults.ExecuteFoundAsync(
                    () => predictionService.PredictWaitTimeAsync(
                        parkId,
                        attractionId,
                        at,
                        cancellationToken),
                    "prediction"))
            .WithName("PredictAttractionWaitTime")
            .WithSummary("Predicts an attraction wait time later in the day")
            .WithTags("Queue prediction")
            .CacheOutput("analytics");

        return endpoints;
    }
}
