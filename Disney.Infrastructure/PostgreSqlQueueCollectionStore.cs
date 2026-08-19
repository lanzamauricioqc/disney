using System.Data;
using Dapper;
using Disney.Application;
using Disney.Domain;

namespace Disney.Infrastructure;

internal sealed class PostgreSqlQueueCollectionStore(
    PostgreSqlConnectionFactory connectionFactory,
    QueueObservationFactory observationFactory) : IQueueCollectionStore
{
    public async Task<long> StartRunAsync(
        long parkId,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleAsync<long>(new CommandDefinition(
            """
            INSERT INTO public.queue_collection_runs (park_id, started_at, success)
            VALUES (@ParkId, @StartedAt, FALSE)
            RETURNING id;
            """,
            new { ParkId = parkId, StartedAt = startedAt },
            cancellationToken: cancellationToken));
    }

    public async Task<CollectionResult> PersistSuccessfulRunAsync(
        long runId,
        Park park,
        QueueTimesSnapshot snapshot,
        DateTimeOffset collectedAt,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await AcquireParkLockAsync(
                connection,
                transaction,
                park.Id,
                cancellationToken);

            var returnedLandIds = snapshot.Lands
                .Select(land => land.SourceLandId)
                .ToHashSet();
            var processedRideIds = new HashSet<int>();
            var landObservationCount = await SaveLandRidesAsync(
                connection,
                transaction,
                runId,
                park,
                snapshot.Lands,
                processedRideIds,
                collectedAt,
                cancellationToken);
            var topLevelObservationCount = await SaveTopLevelRidesAsync(
                connection,
                transaction,
                runId,
                park,
                snapshot.Rides,
                processedRideIds,
                collectedAt,
                cancellationToken);
            var deactivatedLands = await DeactivateMissingLandsAsync(
                connection,
                transaction,
                park.Id,
                returnedLandIds,
                collectedAt,
                cancellationToken);
            var deactivatedAttractions = await DeactivateMissingAttractionsAsync(
                connection,
                transaction,
                park.Id,
                processedRideIds,
                collectedAt,
                cancellationToken);

            await CompleteRunAsync(
                connection,
                transaction,
                runId,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return new CollectionResult(
                runId,
                snapshot.Lands.Count,
                processedRideIds.Count,
                landObservationCount + topLevelObservationCount,
                deactivatedLands,
                deactivatedAttractions);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task FailRunAsync(
        long runId,
        DateTimeOffset completedAt,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var affectedRowCount = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE public.queue_collection_runs
            SET completed_at = @CompletedAt, success = FALSE, error_message = @ErrorMessage
            WHERE id = @RunId;
            """,
            new { RunId = runId, CompletedAt = completedAt, ErrorMessage = errorMessage },
            cancellationToken: cancellationToken));

        if (affectedRowCount != 1)
        {
            throw new InvalidOperationException($"Collection run {runId} was not found.");
        }
    }

    private static Task AcquireParkLockAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        long parkId,
        CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition(
            "SELECT pg_advisory_xact_lock(@ParkId);",
            new { ParkId = parkId },
            transaction,
            cancellationToken: cancellationToken));

    private async Task<int> SaveLandRidesAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        long runId,
        Park park,
        IReadOnlyList<QueueLandSnapshot> lands,
        ISet<int> processedRideIds,
        DateTimeOffset collectedAt,
        CancellationToken cancellationToken)
    {
        var observationCount = 0;

        foreach (var landSnapshot in lands)
        {
            observationCount += await SaveLandRidesAsync(
                connection,
                transaction,
                runId,
                park,
                landSnapshot,
                processedRideIds,
                collectedAt,
                cancellationToken);
        }

        return observationCount;
    }

    private async Task<int> SaveLandRidesAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        long runId,
        Park park,
        QueueLandSnapshot landSnapshot,
        ISet<int> processedRideIds,
        DateTimeOffset collectedAt,
        CancellationToken cancellationToken)
    {
        var land = await UpsertLandAsync(
            connection,
            transaction,
            park.Id,
            landSnapshot,
            cancellationToken);
        var observationCount = 0;

        foreach (var rideSnapshot in landSnapshot.Rides)
        {
            observationCount += await SaveRideAsync(
                connection,
                transaction,
                runId,
                park,
                land.Id,
                rideSnapshot,
                collectedAt,
                cancellationToken);
            processedRideIds.Add(rideSnapshot.SourceRideId);
        }

        return observationCount;
    }

    private async Task<int> SaveTopLevelRidesAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        long runId,
        Park park,
        IReadOnlyList<QueueRideSnapshot> rides,
        ISet<int> processedRideIds,
        DateTimeOffset collectedAt,
        CancellationToken cancellationToken)
    {
        var observationCount = 0;

        foreach (var rideSnapshot in rides)
        {
            if (!processedRideIds.Add(rideSnapshot.SourceRideId))
            {
                continue;
            }

            observationCount += await SaveRideAsync(
                connection,
                transaction,
                runId,
                park,
                null,
                rideSnapshot,
                collectedAt,
                cancellationToken);
        }

        return observationCount;
    }

    private static Task<int> DeactivateMissingLandsAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        long parkId,
        IReadOnlyCollection<int> returnedLandIds,
        DateTimeOffset collectedAt,
        CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE public.lands
            SET is_active = FALSE, updated_at = @CollectedAt
            WHERE park_id = @ParkId AND is_active
              AND NOT (source_land_id = ANY(@ReturnedLandIds));
            """,
            new
            {
                ParkId = parkId,
                ReturnedLandIds = returnedLandIds.ToArray(),
                CollectedAt = collectedAt
            },
            transaction,
            cancellationToken: cancellationToken));

    private static Task<int> DeactivateMissingAttractionsAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        long parkId,
        IReadOnlyCollection<int> processedRideIds,
        DateTimeOffset collectedAt,
        CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE public.attractions
            SET is_active = FALSE, updated_at = @CollectedAt
            WHERE park_id = @ParkId AND is_active
              AND NOT (source_ride_id = ANY(@ProcessedRideIds));
            """,
            new
            {
                ParkId = parkId,
                ProcessedRideIds = processedRideIds.ToArray(),
                CollectedAt = collectedAt
            },
            transaction,
            cancellationToken: cancellationToken));

    private static Task CompleteRunAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        long runId,
        CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE public.queue_collection_runs
            SET completed_at = @CompletedAt, success = TRUE, error_message = NULL
            WHERE id = @RunId;
            """,
            new { RunId = runId, CompletedAt = DateTimeOffset.UtcNow },
            transaction,
            cancellationToken: cancellationToken));

    private async Task<int> SaveRideAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        long runId,
        Park park,
        long? landId,
        QueueRideSnapshot rideSnapshot,
        DateTimeOffset collectedAt,
        CancellationToken cancellationToken)
    {
        var attractionId = await connection.QuerySingleAsync<long>(new CommandDefinition(
            """
            INSERT INTO public.attractions
                (park_id, current_land_id, source_ride_id, name, is_active)
            VALUES (@ParkId, @LandId, @SourceRideId, @Name, TRUE)
            ON CONFLICT (park_id, source_ride_id) DO UPDATE
            SET current_land_id = EXCLUDED.current_land_id,
                name = EXCLUDED.name,
                is_active = TRUE,
                updated_at = now()
            RETURNING id;
            """,
            new
            {
                ParkId = park.Id,
                LandId = landId,
                rideSnapshot.SourceRideId,
                rideSnapshot.Name
            },
            transaction,
            cancellationToken: cancellationToken));

        var observation = observationFactory.Create(
            runId,
            park,
            landId,
            attractionId,
            rideSnapshot,
            collectedAt);

        return await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO public.queue_observations
                (collection_run_id, park_id, land_id, attraction_id, collected_at,
                 observed_at, observed_utc_date, observed_utc_time,
                 observed_utc_hour, observed_utc_slot_minutes,
                 observed_utc_day_of_week, observed_local_date, observed_local_time,
                 observed_local_hour, observed_slot_minutes, observed_day_of_week,
                 is_open, wait_minutes, created_at)
            VALUES
                (@CollectionRunId, @ParkId, @LandId, @AttractionId, @CollectedAt,
                 @ObservedAt, @ObservedUtcDate, @ObservedUtcTime,
                 @ObservedUtcHour, @ObservedUtcSlotMinutes,
                 @ObservedUtcDayOfWeek, @ObservedLocalDate, @ObservedLocalTime,
                 @ObservedLocalHour, @ObservedSlotMinutes, @ObservedDayOfWeek,
                 @IsOpen, @WaitMinutes, @CreatedAt)
            ON CONFLICT (attraction_id, observed_at) DO NOTHING;
            """,
            observation,
            transaction,
            cancellationToken: cancellationToken));
    }

    private static Task<Land> UpsertLandAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        long parkId,
        QueueLandSnapshot landSnapshot,
        CancellationToken cancellationToken) =>
        connection.QuerySingleAsync<Land>(new CommandDefinition(
            """
            INSERT INTO public.lands (park_id, source_land_id, name, is_active)
            VALUES (@ParkId, @SourceLandId, @Name, TRUE)
            ON CONFLICT (park_id, source_land_id) DO UPDATE
            SET name = EXCLUDED.name, is_active = TRUE, updated_at = now()
            RETURNING id, park_id AS ParkId, source_land_id AS SourceLandId, name,
                      is_active AS IsActive, created_at AS CreatedAt, updated_at AS UpdatedAt;
            """,
            new { ParkId = parkId, landSnapshot.SourceLandId, landSnapshot.Name },
            transaction,
            cancellationToken: cancellationToken));
}
