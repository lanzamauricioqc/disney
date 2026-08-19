using Disney.Application;

namespace Disney.Api;

internal static class ParkEndpoints
{
    public static IEndpointRouteBuilder MapParkEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/v1/parks",
                async (IParkReader parkReader, CancellationToken cancellationToken) =>
                {
                    var parks = await parkReader.GetAllAsync(cancellationToken);
                    return Results.Ok(parks.Select(park =>
                        new ParkSummary(park.Id, park.Name, park.Timezone)));
                })
            .WithName("GetParks")
            .WithSummary("Gets the parks available for queue analytics")
            .WithTags("Parks")
            .CacheOutput("parks");

        return endpoints;
    }

    private sealed record ParkSummary(long Id, string Name, string Timezone);
}
