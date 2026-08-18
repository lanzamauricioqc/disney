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
            await connection.ExecuteAsync(new CommandDefinition(
                "SELECT pg_advisory_xact_lock(@ParkId);",
                new { ParkId = park.Id },
                transaction,
                cancellationToken: cancellationToken));

            var returnedLandIds = snapshot.Lands.Select(x => x.SourceLandId).ToHashSet();
            var processedRideIds = new HashSet<int>();
            var observationCount = 0;

            foreach (var landSnapshot in snapshot.Lands)
            {
                var land = await UpsertLandAsync(
                    connection,
                    transaction,
                    park.Id,
                    landSnapshot,
                    cancellationToken);
                foreach (var ride in landSnapshot.Rides)
                {
                    observationCount += await SaveRideAsync(
                        connection,
                        transaction,
                        runId,
                        park,
                        land.Id,
                        ride,
                        collectedAt,
                        cancellationToken);
                    processedRideIds.Add(ride.SourceRideId);
                }
            }

            foreach (var ride in snapshot.Rides.Where(x => processedRideIds.Add(x.SourceRideId)))
            {
                observationCount += await SaveRideAsync(
                    connection,
                    transaction,
                    runId,
                    park,
                    null,
                    ride,
                    collectedAt,
                    cancellationToken);
            }

            var deactivatedLands = await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE public.lands
                SET is_active = FALSE, updated_at = @Now
                WHERE park_id = @ParkId AND is_active
                  AND NOT (source_land_id = ANY(@ReturnedLandIds));
                """,
                new
                {
                    ParkId = park.Id,
                    ReturnedLandIds = returnedLandIds.ToArray(),
                    Now = collectedAt
                },
                transaction,
                cancellationToken: cancellationToken));

            var deactivatedAttractions = await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE public.attractions
                SET is_active = FALSE, updated_at = @Now
                WHERE park_id = @ParkId AND is_active
                  AND NOT (source_ride_id = ANY(@ProcessedRideIds));
                """,
                new
                {
                    ParkId = park.Id,
                    ProcessedRideIds = processedRideIds.ToArray(),
                    Now = collectedAt
                },
                transaction,
                cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE public.queue_collection_runs
                SET completed_at = @CompletedAt, success = TRUE, error_message = NULL
                WHERE id = @RunId;
                """,
                new { RunId = runId, CompletedAt = DateTimeOffset.UtcNow },
                transaction,
                cancellationToken: cancellationToken));

            await transaction.CommitAsync(cancellationToken);
            return new CollectionResult(
                runId,
                snapshot.Lands.Count,
                processedRideIds.Count,
                observationCount,
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
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE public.queue_collection_runs
            SET completed_at = @CompletedAt, success = FALSE, error_message = @ErrorMessage
            WHERE id = @RunId;
            """,
            new { RunId = runId, CompletedAt = completedAt, ErrorMessage = errorMessage },
            cancellationToken: cancellationToken));

        if (affected != 1)
        {
            throw new InvalidOperationException($"Collection run {runId} was not found.");
        }
    }

    private async Task<int> SaveRideAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        long runId,
        Park park,
        long? landId,
        QueueRideSnapshot ride,
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
                ride.SourceRideId,
                ride.Name
            },
            transaction,
            cancellationToken: cancellationToken));

        var observation = observationFactory.Create(
            runId,
            park,
            landId,
            attractionId,
            ride,
            collectedAt);

        return await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO public.queue_observations
                (collection_run_id, park_id, land_id, attraction_id, collected_at,
                 observed_at, observed_local_date, observed_local_time,
                 observed_local_hour, observed_slot_minutes, observed_day_of_week,
                 is_open, wait_minutes, created_at)
            VALUES
                (@CollectionRunId, @ParkId, @LandId, @AttractionId, @CollectedAt,
                 @ObservedAt, @ObservedLocalDate, @ObservedLocalTime,
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
        QueueLandSnapshot land,
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
            new { ParkId = parkId, land.SourceLandId, land.Name },
            transaction,
            cancellationToken: cancellationToken));
}
