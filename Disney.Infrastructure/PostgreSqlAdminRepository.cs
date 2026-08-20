using System.Data;
using Dapper;
using Disney.Application;
using Disney.Domain;

namespace Disney.Infrastructure;

internal sealed class PostgreSqlAdminRepository(
    PostgreSqlConnectionFactory connectionFactory,
    QueueObservationFactory observationFactory) : IAdminRepository
{
    public async Task<IReadOnlyList<AdminPark>> GetParksAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var parks = await connection.QueryAsync<AdminPark>(new CommandDefinition(
            AdminParkSelect + " " + AdminParkGroupBy + " ORDER BY park.name;",
            cancellationToken: cancellationToken));
        return parks.AsList();
    }

    public async Task<AdminPark> CreateParkAsync(
        SaveParkCommand command,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var parkId = await connection.QuerySingleAsync<long>(new CommandDefinition(
            """
            INSERT INTO public.parks
                (source_park_id, name, timezone, is_active, collection_enabled,
                 collection_interval_minutes)
            VALUES
                (@SourceParkId, @Name, @Timezone, @IsActive, @CollectionEnabled,
                 @CollectionIntervalMinutes)
            RETURNING id;
            """,
            command,
            cancellationToken: cancellationToken));
        return (await GetAdminParkAsync(connection, parkId, cancellationToken))!;
    }

    public async Task<AdminPark?> UpdateParkAsync(
        long parkId,
        SaveParkCommand command,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var affectedRows = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE public.parks
            SET source_park_id = @SourceParkId,
                name = @Name,
                timezone = @Timezone,
                is_active = @IsActive,
                collection_enabled = @CollectionEnabled,
                collection_interval_minutes = @CollectionIntervalMinutes,
                updated_at = now()
            WHERE id = @ParkId;
            """,
            new
            {
                ParkId = parkId,
                command.SourceParkId,
                command.Name,
                command.Timezone,
                command.IsActive,
                command.CollectionEnabled,
                command.CollectionIntervalMinutes
            },
            cancellationToken: cancellationToken));
        return affectedRows == 0
            ? null
            : await GetAdminParkAsync(connection, parkId, cancellationToken);
    }

    public async Task<Park?> GetParkAsync(long parkId, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Park>(new CommandDefinition(
            """
            SELECT park.id, park.source_park_id AS SourceParkId, park.name, park.timezone,
                   park.is_active AS IsActive,
                   park.collection_enabled AS CollectionEnabled,
                   park.collection_interval_minutes AS CollectionIntervalMinutes,
                   latest_run.started_at AS LastCollectionStartedAt,
                   park.created_at AS CreatedAt, park.updated_at AS UpdatedAt
            FROM public.parks park
            LEFT JOIN LATERAL (
                SELECT run.started_at
                FROM public.queue_collection_runs run
                WHERE run.park_id = park.id
                ORDER BY run.started_at DESC
                LIMIT 1
            ) latest_run ON TRUE
            WHERE park.id = @ParkId;
            """,
            new { ParkId = parkId },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<AdminLand>> GetLandsAsync(
        long parkId,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var lands = await connection.QueryAsync<AdminLand>(new CommandDefinition(
            """
            SELECT id, park_id AS ParkId, source_land_id AS SourceLandId, name,
                   is_active AS IsActive
            FROM public.lands
            WHERE park_id = @ParkId
            ORDER BY name;
            """,
            new { ParkId = parkId },
            cancellationToken: cancellationToken));
        return lands.AsList();
    }

    public async Task<AdminLand> CreateLandAsync(
        SaveLandCommand command,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleAsync<AdminLand>(new CommandDefinition(
            """
            INSERT INTO public.lands (park_id, source_land_id, name, is_active)
            VALUES (@ParkId, @SourceLandId, @Name, @IsActive)
            RETURNING id, park_id AS ParkId, source_land_id AS SourceLandId, name,
                      is_active AS IsActive;
            """,
            command,
            cancellationToken: cancellationToken));
    }

    public async Task<AdminLand?> UpdateLandAsync(
        long landId,
        SaveLandCommand command,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<AdminLand>(new CommandDefinition(
            """
            UPDATE public.lands
            SET park_id = @ParkId,
                source_land_id = @SourceLandId,
                name = @Name,
                is_active = @IsActive,
                updated_at = now()
            WHERE id = @LandId
            RETURNING id, park_id AS ParkId, source_land_id AS SourceLandId, name,
                      is_active AS IsActive;
            """,
            new
            {
                LandId = landId,
                command.ParkId,
                command.SourceLandId,
                command.Name,
                command.IsActive
            },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<AdminAttraction>> GetAttractionsAsync(
        long parkId,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var attractions = await connection.QueryAsync<AdminAttraction>(new CommandDefinition(
            """
            SELECT attraction.id, attraction.park_id AS ParkId,
                   attraction.current_land_id AS CurrentLandId, land.name AS LandName,
                   attraction.source_ride_id AS SourceRideId, attraction.name,
                   attraction.is_active AS IsActive,
                   attraction.duration_minutes AS DurationMinutes,
                   attraction.latitude, attraction.longitude
            FROM public.attractions attraction
            LEFT JOIN public.lands land ON land.id = attraction.current_land_id
            WHERE attraction.park_id = @ParkId
            ORDER BY attraction.name;
            """,
            new { ParkId = parkId },
            cancellationToken: cancellationToken));
        return attractions.AsList();
    }

    public async Task<AdminAttraction> CreateAttractionAsync(
        SaveAttractionCommand command,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var attractionId = await connection.QuerySingleAsync<long>(new CommandDefinition(
            """
            INSERT INTO public.attractions
                (park_id, current_land_id, source_ride_id, name, is_active,
                 duration_minutes, latitude, longitude)
            VALUES
                (@ParkId, @CurrentLandId, @SourceRideId, @Name, @IsActive,
                 @DurationMinutes, @Latitude, @Longitude)
            RETURNING id;
            """,
            command,
            cancellationToken: cancellationToken));
        return (await GetAttractionAsync(connection, attractionId, cancellationToken))!;
    }

    public async Task<AdminAttraction?> UpdateAttractionAsync(
        long attractionId,
        SaveAttractionCommand command,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var affectedRows = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE public.attractions
            SET park_id = @ParkId,
                current_land_id = @CurrentLandId,
                source_ride_id = @SourceRideId,
                name = @Name,
                is_active = @IsActive,
                duration_minutes = @DurationMinutes,
                latitude = @Latitude,
                longitude = @Longitude,
                updated_at = now()
            WHERE id = @AttractionId;
            """,
            new
            {
                AttractionId = attractionId,
                command.ParkId,
                command.CurrentLandId,
                command.SourceRideId,
                command.Name,
                command.IsActive,
                command.DurationMinutes,
                command.Latitude,
                command.Longitude
            },
            cancellationToken: cancellationToken));
        return affectedRows == 0
            ? null
            : await GetAttractionAsync(connection, attractionId, cancellationToken);
    }

    public async Task<IReadOnlyList<AdminCollectionRun>> GetCollectionRunsAsync(
        long? parkId,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var runs = await connection.QueryAsync<AdminCollectionRun>(new CommandDefinition(
            """
            SELECT run.id, run.park_id AS ParkId, park.name AS ParkName,
                   run.started_at AS StartedAt, run.completed_at AS CompletedAt,
                   run.success, run.error_message AS ErrorMessage,
                   run.trigger_source AS TriggerSource,
                   COUNT(observation.id)::int AS ObservationCount
            FROM public.queue_collection_runs run
            JOIN public.parks park ON park.id = run.park_id
            LEFT JOIN public.queue_observations observation
              ON observation.collection_run_id = run.id
            WHERE (@ParkId IS NULL OR run.park_id = @ParkId)
            GROUP BY run.id, park.name
            ORDER BY run.started_at DESC
            LIMIT @Limit;
            """,
            new { ParkId = parkId, Limit = limit },
            cancellationToken: cancellationToken));
        return runs.AsList();
    }

    public async Task<Park?> GetParkForRunAsync(
        long runId,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var parkId = await connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            "SELECT park_id FROM public.queue_collection_runs WHERE id = @RunId;",
            new { RunId = runId },
            cancellationToken: cancellationToken));
        return parkId is null
            ? null
            : await GetParkAsync(parkId.Value, cancellationToken);
    }

    public async Task SetCollectionRunTriggerAsync(
        long runId,
        string triggerSource,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var affectedRows = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE public.queue_collection_runs
            SET trigger_source = @TriggerSource
            WHERE id = @RunId;
            """,
            new { RunId = runId, TriggerSource = triggerSource },
            cancellationToken: cancellationToken));

        if (affectedRows != 1)
        {
            throw new KeyNotFoundException($"Collection run {runId} was not found.");
        }
    }

    public async Task<IReadOnlyList<AdminObservation>> GetObservationsAsync(
        long parkId,
        long? attractionId,
        bool includeInvalid,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var observations = await connection.QueryAsync<AdminObservation>(new CommandDefinition(
            """
            SELECT observation.id, observation.park_id AS ParkId, park.name AS ParkName,
                   observation.attraction_id AS AttractionId,
                   attraction.name AS AttractionName,
                   observation.land_id AS LandId, land.name AS LandName,
                   observation.observed_at AS ObservedAt,
                   observation.is_open AS IsOpen,
                   observation.wait_minutes AS WaitMinutes,
                   observation.is_valid AS IsValid,
                   observation.invalid_reason AS InvalidReason,
                   run.trigger_source AS TriggerSource
            FROM public.queue_observations observation
            JOIN public.parks park ON park.id = observation.park_id
            JOIN public.attractions attraction
              ON attraction.id = observation.attraction_id
            JOIN public.queue_collection_runs run
              ON run.id = observation.collection_run_id
            LEFT JOIN public.lands land ON land.id = observation.land_id
            WHERE observation.park_id = @ParkId
              AND (@AttractionId IS NULL
                   OR observation.attraction_id = @AttractionId)
              AND (@IncludeInvalid OR observation.is_valid)
            ORDER BY observation.observed_at DESC
            LIMIT @Limit;
            """,
            new
            {
                ParkId = parkId,
                AttractionId = attractionId,
                IncludeInvalid = includeInvalid,
                Limit = limit
            },
            cancellationToken: cancellationToken));
        return observations.AsList();
    }

    public async Task<AdminObservation> CreateManualObservationAsync(
        ManualObservationCommand command,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var context = await connection.QuerySingleOrDefaultAsync<ManualObservationContext>(
                new CommandDefinition(
                    """
                    SELECT attraction.id AS AttractionId,
                           attraction.source_ride_id AS SourceRideId,
                           attraction.name AS AttractionName,
                           attraction.current_land_id AS LandId,
                           park.id AS ParkId, park.source_park_id AS SourceParkId,
                           park.name AS ParkName, park.timezone
                    FROM public.attractions attraction
                    JOIN public.parks park ON park.id = attraction.park_id
                    WHERE attraction.id = @AttractionId;
                    """,
                    new { command.AttractionId },
                    transaction,
                    cancellationToken: cancellationToken))
                ?? throw new KeyNotFoundException(
                    $"Attraction {command.AttractionId} was not found.");

            var collectionRunId = await connection.QuerySingleAsync<long>(
                new CommandDefinition(
                    """
                    INSERT INTO public.queue_collection_runs
                        (park_id, started_at, completed_at, success, trigger_source)
                    VALUES (@ParkId, @StartedAt, @StartedAt, TRUE, 'manual')
                    RETURNING id;
                    """,
                    new { context.ParkId, StartedAt = DateTimeOffset.UtcNow },
                    transaction,
                    cancellationToken: cancellationToken));

            var park = new Park
            {
                Id = context.ParkId,
                SourceParkId = context.SourceParkId,
                Name = context.ParkName,
                Timezone = context.Timezone
            };
            var ride = new QueueRideSnapshot(
                context.SourceRideId,
                context.AttractionName,
                command.IsOpen,
                command.WaitMinutes ?? 0,
                command.ObservedAt);
            var observation = observationFactory.Create(
                collectionRunId,
                park,
                context.LandId,
                context.AttractionId,
                ride,
                DateTimeOffset.UtcNow);

            var observationId = await connection.QuerySingleAsync<long>(
                new CommandDefinition(
                    """
                    INSERT INTO public.queue_observations
                        (collection_run_id, park_id, land_id, attraction_id, collected_at,
                         observed_at, observed_utc_date, observed_utc_time,
                         observed_utc_hour, observed_utc_slot_minutes,
                         observed_utc_day_of_week, observed_local_date,
                         observed_local_time, observed_local_hour,
                         observed_slot_minutes, observed_day_of_week,
                         is_open, wait_minutes, created_at)
                    VALUES
                        (@CollectionRunId, @ParkId, @LandId, @AttractionId, @CollectedAt,
                         @ObservedAt, @ObservedUtcDate, @ObservedUtcTime,
                         @ObservedUtcHour, @ObservedUtcSlotMinutes,
                         @ObservedUtcDayOfWeek, @ObservedLocalDate,
                         @ObservedLocalTime, @ObservedLocalHour,
                         @ObservedSlotMinutes, @ObservedDayOfWeek,
                         @IsOpen, @WaitMinutes, @CreatedAt)
                    RETURNING id;
                    """,
                    observation,
                    transaction,
                    cancellationToken: cancellationToken));

            await transaction.CommitAsync(cancellationToken);
            return (await GetObservationAsync(
                connection,
                observationId,
                cancellationToken))!;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<bool> SetObservationValidityAsync(
        long observationId,
        bool isValid,
        string? reason,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var affectedRows = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE public.queue_observations
            SET is_valid = @IsValid,
                invalid_reason = CASE WHEN @IsValid THEN NULL ELSE @Reason END,
                invalidated_at = CASE WHEN @IsValid THEN NULL ELSE now() END
            WHERE id = @ObservationId;
            """,
            new { ObservationId = observationId, IsValid = isValid, Reason = reason },
            cancellationToken: cancellationToken));
        return affectedRows == 1;
    }

    public async Task<int> PurgeObservationsAsync(
        long parkId,
        long? attractionId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            DELETE FROM public.queue_observations
            WHERE park_id = @ParkId
              AND observed_at >= @From
              AND observed_at <= @To
              AND (@AttractionId IS NULL OR attraction_id = @AttractionId);
            """,
            new { ParkId = parkId, AttractionId = attractionId, From = from, To = to },
            cancellationToken: cancellationToken));
    }

    private static Task<AdminPark?> GetAdminParkAsync(
        IDbConnection connection,
        long parkId,
        CancellationToken cancellationToken) =>
        connection.QuerySingleOrDefaultAsync<AdminPark>(new CommandDefinition(
            AdminParkSelect + " WHERE park.id = @ParkId " + AdminParkGroupBy + ";",
            new { ParkId = parkId },
            cancellationToken: cancellationToken));

    private static Task<AdminAttraction?> GetAttractionAsync(
        IDbConnection connection,
        long attractionId,
        CancellationToken cancellationToken) =>
        connection.QuerySingleOrDefaultAsync<AdminAttraction>(new CommandDefinition(
            """
            SELECT attraction.id, attraction.park_id AS ParkId,
                   attraction.current_land_id AS CurrentLandId, land.name AS LandName,
                   attraction.source_ride_id AS SourceRideId, attraction.name,
                   attraction.is_active AS IsActive,
                   attraction.duration_minutes AS DurationMinutes,
                   attraction.latitude, attraction.longitude
            FROM public.attractions attraction
            LEFT JOIN public.lands land ON land.id = attraction.current_land_id
            WHERE attraction.id = @AttractionId;
            """,
            new { AttractionId = attractionId },
            cancellationToken: cancellationToken));

    private static Task<AdminObservation?> GetObservationAsync(
        IDbConnection connection,
        long observationId,
        CancellationToken cancellationToken) =>
        connection.QuerySingleOrDefaultAsync<AdminObservation>(new CommandDefinition(
            """
            SELECT observation.id, observation.park_id AS ParkId, park.name AS ParkName,
                   observation.attraction_id AS AttractionId,
                   attraction.name AS AttractionName,
                   observation.land_id AS LandId, land.name AS LandName,
                   observation.observed_at AS ObservedAt,
                   observation.is_open AS IsOpen,
                   observation.wait_minutes AS WaitMinutes,
                   observation.is_valid AS IsValid,
                   observation.invalid_reason AS InvalidReason,
                   run.trigger_source AS TriggerSource
            FROM public.queue_observations observation
            JOIN public.parks park ON park.id = observation.park_id
            JOIN public.attractions attraction
              ON attraction.id = observation.attraction_id
            JOIN public.queue_collection_runs run
              ON run.id = observation.collection_run_id
            LEFT JOIN public.lands land ON land.id = observation.land_id
            WHERE observation.id = @ObservationId;
            """,
            new { ObservationId = observationId },
            cancellationToken: cancellationToken));

    private const string AdminParkSelect =
        """
        SELECT park.id, park.source_park_id AS SourceParkId, park.name, park.timezone,
               park.is_active AS IsActive,
               park.collection_enabled AS CollectionEnabled,
               park.collection_interval_minutes AS CollectionIntervalMinutes,
               latest_run.started_at AS LastCollectionStartedAt,
               latest_run.completed_at AS LastCollectionCompletedAt,
               latest_run.success AS LastCollectionSucceeded,
               latest_run.error_message AS LastCollectionError,
               COUNT(DISTINCT attraction.id)::int AS AttractionCount,
               COUNT(DISTINCT observation.id)::bigint AS ObservationCount
        FROM public.parks park
        LEFT JOIN LATERAL (
            SELECT run.started_at, run.completed_at, run.success, run.error_message
            FROM public.queue_collection_runs run
            WHERE run.park_id = park.id
            ORDER BY run.started_at DESC
            LIMIT 1
        ) latest_run ON TRUE
        LEFT JOIN public.attractions attraction ON attraction.park_id = park.id
        LEFT JOIN public.queue_observations observation ON observation.park_id = park.id
        """;

    private const string AdminParkGroupBy =
        """
        GROUP BY park.id, latest_run.started_at, latest_run.completed_at,
                 latest_run.success, latest_run.error_message
        """;

    private sealed class ManualObservationContext
    {
        public long AttractionId { get; init; }
        public int SourceRideId { get; init; }
        public string AttractionName { get; init; } = string.Empty;
        public long? LandId { get; init; }
        public long ParkId { get; init; }
        public int SourceParkId { get; init; }
        public string ParkName { get; init; } = string.Empty;
        public string Timezone { get; init; } = string.Empty;
    }
}
