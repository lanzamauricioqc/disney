using Disney.Application;
using Microsoft.AspNetCore.OutputCaching;

namespace Disney.Api;

internal static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/api/v1/admin")
            .WithTags("Administration");

        MapParkEndpoints(admin);
        MapLandEndpoints(admin);
        MapAttractionEndpoints(admin);
        MapCollectionEndpoints(admin);
        MapObservationEndpoints(admin);
        return endpoints;
    }

    private static void MapParkEndpoints(RouteGroupBuilder admin)
    {
        admin.MapGet(
            "/parks",
            async (IAdminRepository repository, CancellationToken cancellationToken) =>
                Results.Ok(await repository.GetParksAsync(cancellationToken)));

        admin.MapPost(
            "/parks",
            async (
                SaveParkRequest request,
                IAdminRepository repository,
                IOutputCacheStore outputCache,
                CancellationToken cancellationToken) =>
            {
                var validation = ValidatePark(request);
                if (validation is not null)
                {
                    return validation;
                }

                var park = await repository.CreateParkAsync(
                    request.ToCommand(),
                    cancellationToken);
                await EvictPublicDataAsync(outputCache, cancellationToken);
                return Results.Created($"/api/v1/admin/parks/{park.Id}", park);
            });

        admin.MapPut(
            "/parks/{parkId:long:min(1)}",
            async (
                long parkId,
                SaveParkRequest request,
                IAdminRepository repository,
                IOutputCacheStore outputCache,
                CancellationToken cancellationToken) =>
            {
                var validation = ValidatePark(request);
                if (validation is not null)
                {
                    return validation;
                }

                var park = await repository.UpdateParkAsync(
                    parkId,
                    request.ToCommand(),
                    cancellationToken);
                if (park is null)
                {
                    return Results.NotFound();
                }

                await EvictPublicDataAsync(outputCache, cancellationToken);
                return Results.Ok(park);
            });
    }

    private static void MapLandEndpoints(RouteGroupBuilder admin)
    {
        admin.MapGet(
            "/parks/{parkId:long:min(1)}/lands",
            async (
                long parkId,
                IAdminRepository repository,
                CancellationToken cancellationToken) =>
                Results.Ok(await repository.GetLandsAsync(parkId, cancellationToken)));

        admin.MapPost(
            "/lands",
            async (
                SaveLandRequest request,
                IAdminRepository repository,
                CancellationToken cancellationToken) =>
            {
                var validation = ValidateLand(request);
                return validation ?? Results.Created(
                    "/api/v1/admin/lands",
                    await repository.CreateLandAsync(
                        request.ToCommand(),
                        cancellationToken));
            });

        admin.MapPut(
            "/lands/{landId:long:min(1)}",
            async (
                long landId,
                SaveLandRequest request,
                IAdminRepository repository,
                CancellationToken cancellationToken) =>
            {
                var validation = ValidateLand(request);
                if (validation is not null)
                {
                    return validation;
                }

                var land = await repository.UpdateLandAsync(
                    landId,
                    request.ToCommand(),
                    cancellationToken);
                return land is null ? Results.NotFound() : Results.Ok(land);
            });
    }

    private static void MapAttractionEndpoints(RouteGroupBuilder admin)
    {
        admin.MapGet(
            "/parks/{parkId:long:min(1)}/attractions",
            async (
                long parkId,
                IAdminRepository repository,
                CancellationToken cancellationToken) =>
                Results.Ok(await repository.GetAttractionsAsync(
                    parkId,
                    cancellationToken)));

        admin.MapPost(
            "/attractions",
            async (
                SaveAttractionRequest request,
                IAdminRepository repository,
                CancellationToken cancellationToken) =>
            {
                var validation = ValidateAttraction(request);
                return validation ?? Results.Created(
                    "/api/v1/admin/attractions",
                    await repository.CreateAttractionAsync(
                        request.ToCommand(),
                        cancellationToken));
            });

        admin.MapPut(
            "/attractions/{attractionId:long:min(1)}",
            async (
                long attractionId,
                SaveAttractionRequest request,
                IAdminRepository repository,
                CancellationToken cancellationToken) =>
            {
                var validation = ValidateAttraction(request);
                if (validation is not null)
                {
                    return validation;
                }

                var attraction = await repository.UpdateAttractionAsync(
                    attractionId,
                    request.ToCommand(),
                    cancellationToken);
                return attraction is null
                    ? Results.NotFound()
                    : Results.Ok(attraction);
            });
    }

    private static void MapCollectionEndpoints(RouteGroupBuilder admin)
    {
        admin.MapGet(
            "/collection-runs",
            async (
                long? parkId,
                int? limit,
                IAdminRepository repository,
                CancellationToken cancellationToken) =>
                Results.Ok(await repository.GetCollectionRunsAsync(
                    parkId,
                    Math.Clamp(limit ?? 50, 1, 200),
                    cancellationToken)));

        admin.MapPost(
            "/parks/{parkId:long:min(1)}/collect",
            async (
                long parkId,
                IAdminRepository repository,
                IQueueCollectionService collectionService,
                CancellationToken cancellationToken) =>
            {
                var park = await repository.GetParkAsync(parkId, cancellationToken);
                if (park is null)
                {
                    return Results.NotFound();
                }
                if (!park.IsActive)
                {
                    return ValidationProblem(
                        "parkId",
                        "An inactive park cannot be collected.");
                }

                var result = await collectionService.CollectAsync(
                    park,
                    cancellationToken);
                await repository.SetCollectionRunTriggerAsync(
                    result.CollectionRunId,
                    "manual",
                    cancellationToken);
                return Results.Ok(result);
            });

        admin.MapPost(
            "/collection-runs/{runId:long:min(1)}/retry",
            async (
                long runId,
                IAdminRepository repository,
                IQueueCollectionService collectionService,
                CancellationToken cancellationToken) =>
            {
                var park = await repository.GetParkForRunAsync(
                    runId,
                    cancellationToken);
                if (park is null)
                {
                    return Results.NotFound();
                }

                var result = await collectionService.CollectAsync(
                    park,
                    cancellationToken);
                await repository.SetCollectionRunTriggerAsync(
                    result.CollectionRunId,
                    "retry",
                    cancellationToken);
                return Results.Ok(result);
            });
    }

    private static void MapObservationEndpoints(RouteGroupBuilder admin)
    {
        admin.MapGet(
            "/observations",
            async (
                long parkId,
                long? attractionId,
                bool? includeInvalid,
                int? limit,
                IAdminRepository repository,
                CancellationToken cancellationToken) =>
                Results.Ok(await repository.GetObservationsAsync(
                    parkId,
                    attractionId,
                    includeInvalid ?? true,
                    Math.Clamp(limit ?? 100, 1, 500),
                    cancellationToken)));

        admin.MapPost(
            "/observations",
            async (
                ManualObservationRequest request,
                IAdminRepository repository,
                IOutputCacheStore outputCache,
                CancellationToken cancellationToken) =>
            {
                var validation = ValidateManualObservation(request);
                if (validation is not null)
                {
                    return validation;
                }

                var observation = await repository.CreateManualObservationAsync(
                    new ManualObservationCommand(
                        request.AttractionId,
                        request.ObservedAt,
                        request.IsOpen,
                        request.WaitMinutes),
                    cancellationToken);
                await EvictPublicDataAsync(outputCache, cancellationToken);
                return Results.Created(
                    $"/api/v1/admin/observations/{observation.Id}",
                    observation);
            });

        admin.MapPut(
            "/observations/{observationId:long:min(1)}/validity",
            async (
                long observationId,
                ObservationValidityRequest request,
                IAdminRepository repository,
                IOutputCacheStore outputCache,
                CancellationToken cancellationToken) =>
            {
                if (!request.IsValid && string.IsNullOrWhiteSpace(request.Reason))
                {
                    return ValidationProblem(
                        "reason",
                        "A reason is required when invalidating an observation.");
                }

                var updated = await repository.SetObservationValidityAsync(
                    observationId,
                    request.IsValid,
                    request.Reason?.Trim(),
                    cancellationToken);
                if (!updated)
                {
                    return Results.NotFound();
                }

                await EvictPublicDataAsync(outputCache, cancellationToken);
                return Results.NoContent();
            });

        admin.MapPost(
            "/observations/purge",
            async (
                PurgeObservationsRequest request,
                IAdminRepository repository,
                IOutputCacheStore outputCache,
                CancellationToken cancellationToken) =>
            {
                if (request.From >= request.To)
                {
                    return ValidationProblem(
                        "from",
                        "The start of the purge range must be before the end.");
                }
                if (!string.Equals(request.Confirmation, "DELETE", StringComparison.Ordinal))
                {
                    return ValidationProblem(
                        "confirmation",
                        "Enter DELETE to confirm permanent removal.");
                }

                var deletedCount = await repository.PurgeObservationsAsync(
                    request.ParkId,
                    request.AttractionId,
                    request.From,
                    request.To,
                    cancellationToken);
                await EvictPublicDataAsync(outputCache, cancellationToken);
                return Results.Ok(new { deletedCount });
            });
    }

    private static IResult? ValidatePark(SaveParkRequest request)
    {
        if (request.SourceParkId <= 0)
        {
            return ValidationProblem("sourceParkId", "Source park ID must be positive.");
        }
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ValidationProblem("name", "Park name is required.");
        }
        if (request.CollectionIntervalMinutes is < 1 or > 1440)
        {
            return ValidationProblem(
                "collectionIntervalMinutes",
                "Collection interval must be between 1 and 1440 minutes.");
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(request.Timezone);
        }
        catch (Exception exception)
            when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return ValidationProblem("timezone", "Enter a valid IANA timezone.");
        }

        return null;
    }

    private static IResult? ValidateLand(SaveLandRequest request)
    {
        if (request.ParkId <= 0 || request.SourceLandId <= 0)
        {
            return ValidationProblem(
                "sourceLandId",
                "Park and source land IDs must be positive.");
        }
        return string.IsNullOrWhiteSpace(request.Name)
            ? ValidationProblem("name", "Land name is required.")
            : null;
    }

    private static IResult? ValidateAttraction(SaveAttractionRequest request)
    {
        if (request.ParkId <= 0 || request.SourceRideId <= 0)
        {
            return ValidationProblem(
                "sourceRideId",
                "Park and source attraction IDs must be positive.");
        }
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ValidationProblem("name", "Attraction name is required.");
        }
        if (request.DurationMinutes <= 0)
        {
            return ValidationProblem(
                "durationMinutes",
                "Duration must be positive when provided.");
        }
        if (request.Latitude is < -90 or > 90 || request.Longitude is < -180 or > 180)
        {
            return ValidationProblem(
                "coordinates",
                "Coordinates are outside their valid ranges.");
        }
        return null;
    }

    private static IResult? ValidateManualObservation(
        ManualObservationRequest request)
    {
        if (request.AttractionId <= 0)
        {
            return ValidationProblem("attractionId", "Select an attraction.");
        }
        if (request.ObservedAt > DateTimeOffset.UtcNow.AddMinutes(5))
        {
            return ValidationProblem(
                "observedAt",
                "Observation time cannot be more than five minutes in the future.");
        }
        if (request.IsOpen && request.WaitMinutes is null or < 0 or > short.MaxValue)
        {
            return ValidationProblem(
                "waitMinutes",
                "An open attraction requires a non-negative wait time.");
        }
        return null;
    }

    private static IResult ValidationProblem(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [field] = [message]
        });

    private static async Task EvictPublicDataAsync(
        IOutputCacheStore outputCache,
        CancellationToken cancellationToken)
    {
        await outputCache.EvictByTagAsync("parks", cancellationToken);
        await outputCache.EvictByTagAsync("current-waits", cancellationToken);
        await outputCache.EvictByTagAsync("analytics", cancellationToken);
    }

    private sealed record SaveParkRequest(
        int SourceParkId,
        string Name,
        string Timezone,
        bool IsActive,
        bool CollectionEnabled,
        int CollectionIntervalMinutes)
    {
        public SaveParkCommand ToCommand() =>
            new(
                SourceParkId,
                Name.Trim(),
                Timezone.Trim(),
                IsActive,
                CollectionEnabled,
                CollectionIntervalMinutes);
    }

    private sealed record SaveLandRequest(
        long ParkId,
        int SourceLandId,
        string Name,
        bool IsActive)
    {
        public SaveLandCommand ToCommand() =>
            new(ParkId, SourceLandId, Name.Trim(), IsActive);
    }

    private sealed record SaveAttractionRequest(
        long ParkId,
        long? CurrentLandId,
        int SourceRideId,
        string Name,
        bool IsActive,
        int? DurationMinutes,
        decimal? Latitude,
        decimal? Longitude)
    {
        public SaveAttractionCommand ToCommand() =>
            new(
                ParkId,
                CurrentLandId,
                SourceRideId,
                Name.Trim(),
                IsActive,
                DurationMinutes,
                Latitude,
                Longitude);
    }

    private sealed record ManualObservationRequest(
        long AttractionId,
        DateTimeOffset ObservedAt,
        bool IsOpen,
        int? WaitMinutes);

    private sealed record ObservationValidityRequest(bool IsValid, string? Reason);

    private sealed record PurgeObservationsRequest(
        long ParkId,
        long? AttractionId,
        DateTimeOffset From,
        DateTimeOffset To,
        string Confirmation);
}
