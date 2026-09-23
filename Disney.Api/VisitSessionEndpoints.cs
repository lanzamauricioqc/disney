using Disney.Application;

namespace Disney.Api;

internal static class VisitSessionEndpoints
{
    public static IEndpointRouteBuilder MapVisitSessionEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                "/api/v1/parks/{parkId:long:min(1)}/visit-sessions",
                async (
                    long parkId,
                    StartVisitSessionRequest request,
                    IVisitSessionService service,
                    CancellationToken cancellationToken) =>
                    await ExecuteAsync(async () =>
                    {
                        var session = await service.StartAsync(
                            parkId,
                            new StartVisitSessionCommand(
                                request.PartySize,
                                request.Itinerary?.ToCommand() ??
                                    throw new ArgumentException(
                                        "An itinerary is required.",
                                        nameof(request.Itinerary))),
                            cancellationToken);
                        return Results.Created(
                            $"/api/v1/visit-sessions/{session.Id}",
                            session);
                    }))
            .WithName("StartVisitSession")
            .WithSummary("Starts and persists an optimized park visit")
            .WithTags("Visit sessions");

        endpoints.MapGet(
                "/api/v1/visit-sessions/{sessionId:guid}",
                async (
                    Guid sessionId,
                    IVisitSessionService service,
                    CancellationToken cancellationToken) =>
                    await ExecuteAsync(async () =>
                    {
                        var session = await service.GetAsync(sessionId, cancellationToken);
                        return session is null ? Results.NotFound() : Results.Ok(session);
                    }))
            .WithName("GetVisitSession")
            .WithSummary("Gets persisted visit progress")
            .WithTags("Visit sessions");

        endpoints.MapPost(
                "/api/v1/visit-sessions/{sessionId:guid}/replan",
                async (
                    Guid sessionId,
                    IVisitSessionService service,
                    CancellationToken cancellationToken) =>
                    await ExecuteAsync(async () =>
                    {
                        var session = await service.ReplanForCurrentConditionsAsync(
                            sessionId,
                            cancellationToken);
                        return session is null ? Results.NotFound() : Results.Ok(session);
                    }))
            .WithName("ReplanVisitSession")
            .WithSummary("Replans a visit after closures or material queue changes")
            .WithTags("Visit sessions");

        MapStopAction(
            endpoints,
            "complete",
            "CompleteVisitSessionAttraction",
            static (service, sessionId, attractionId, cancellationToken) =>
                service.CompleteAttractionAsync(
                    sessionId,
                    attractionId,
                    cancellationToken));
        MapStopAction(
            endpoints,
            "skip",
            "SkipVisitSessionAttraction",
            static (service, sessionId, attractionId, cancellationToken) =>
                service.SkipAttractionAsync(
                    sessionId,
                    attractionId,
                    cancellationToken));

        return endpoints;
    }

    private static void MapStopAction(
        IEndpointRouteBuilder endpoints,
        string action,
        string endpointName,
        Func<IVisitSessionService, Guid, long, CancellationToken, Task<VisitSession?>>
            update)
    {
        endpoints.MapPost(
                $"/api/v1/visit-sessions/{{sessionId:guid}}/stops/" +
                $"{{attractionId:long:min(1)}}/{action}",
                async (
                    Guid sessionId,
                    long attractionId,
                    IVisitSessionService service,
                    CancellationToken cancellationToken) =>
                    await ExecuteAsync(async () =>
                    {
                        var session = await update(
                            service,
                            sessionId,
                            attractionId,
                            cancellationToken);
                        return session is null ? Results.NotFound() : Results.Ok(session);
                    }))
            .WithName(endpointName)
            .WithSummary($"{char.ToUpperInvariant(action[0])}{action[1..]}s a visit attraction")
            .WithTags("Visit sessions");
    }

    private static async Task<IResult> ExecuteAsync(Func<Task<IResult>> execute)
    {
        try
        {
            return await execute();
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [exception.ParamName ?? "visitSession"] = [exception.Message]
            });
        }
        catch (InvalidOperationException exception)
        {
            return Results.Conflict(new { detail = exception.Message });
        }
    }

    internal sealed record StartVisitSessionRequest(
        int PartySize,
        ItineraryOptimizationEndpoints.GenerateItineraryRequest? Itinerary);
}
