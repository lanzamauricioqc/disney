using Disney.Application;

namespace Disney.Api;

internal static class ItineraryOptimizationEndpoints
{
    public static IEndpointRouteBuilder MapItineraryOptimizationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                "/api/v1/parks/{parkId:long:min(1)}/itineraries/optimize",
                async (
                    long parkId,
                    GenerateItineraryRequest request,
                    IItineraryOptimizationService optimizationService,
                    CancellationToken cancellationToken) =>
                    await ExecuteAsync(
                        () => optimizationService.GenerateAsync(
                            parkId,
                            request.ToCommand(),
                            cancellationToken)))
            .WithName("GenerateOptimizedItinerary")
            .WithSummary("Generates an ordered itinerary for a park visit")
            .WithTags("Itinerary optimization");

        return endpoints;
    }

    private static async Task<IResult> ExecuteAsync(
        Func<Task<OptimizedItineraryResult>> execute)
    {
        try
        {
            return Results.Ok(await execute());
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [exception.ParamName ?? "itinerary"] = [exception.Message]
            });
        }
    }

    internal sealed record GenerateItineraryRequest(
        DateTimeOffset VisitStartAt,
        DateTimeOffset VisitEndAt,
        long? StartingAttractionId,
        IReadOnlyList<ItineraryPreferenceRequest?>? Preferences)
    {
        public GenerateItineraryCommand ToCommand()
        {
            if (Preferences?.Any(preference => preference is null) == true)
            {
                throw new ArgumentException(
                    "Attraction preferences cannot contain null entries.",
                    nameof(Preferences));
            }

            return new(
                VisitStartAt,
                VisitEndAt,
                StartingAttractionId,
                Preferences?
                    .Select(preference =>
                    {
                        if (preference!.Level is null)
                        {
                            throw new ArgumentException(
                                "Every attraction preference requires a level.",
                                nameof(Preferences));
                        }

                        return new ItineraryPreference(
                            preference.AttractionId,
                            preference.Level.Value);
                    })
                    .ToArray() ?? []);
        }
    }

    internal sealed record ItineraryPreferenceRequest(
        long AttractionId,
        AttractionPreferenceLevel? Level);
}
