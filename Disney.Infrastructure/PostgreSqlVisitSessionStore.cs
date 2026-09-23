using Dapper;
using Disney.Application;

namespace Disney.Infrastructure;

internal sealed class PostgreSqlVisitSessionStore(
    PostgreSqlConnectionFactory connectionFactory) : IVisitSessionStore
{
    public async Task CreateAsync(
        VisitSession session,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO public.visit_sessions
                (id, park_id, visit_start_at, visit_end_at, party_size,
                 started_at, updated_at, status, total_walking_minutes,
                 total_queue_minutes, total_attraction_minutes, algorithm_version)
            VALUES
                (@Id, @ParkId, @VisitStartAt, @VisitEndAt, @PartySize,
                 @StartedAt, @UpdatedAt, @Status, @TotalWalkingMinutes,
                 @TotalQueueMinutes, @TotalAttractionMinutes, @AlgorithmVersion);
            """,
            new
            {
                session.Id,
                session.ParkId,
                session.VisitStartAt,
                session.VisitEndAt,
                session.PartySize,
                session.StartedAt,
                session.UpdatedAt,
                Status = session.Status.ToString(),
                session.TotalWalkingMinutes,
                session.TotalQueueMinutes,
                session.TotalAttractionMinutes,
                session.AlgorithmVersion
            },
            transaction,
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO public.visit_session_stops
                (session_id, sequence, attraction_id, attraction_name, preference,
                 travel_starts_at, walking_minutes, queue_starts_at, queue_minutes,
                 attraction_starts_at, attraction_duration_minutes, completes_at,
                 status, status_changed_at)
            VALUES
                (@SessionId, @Sequence, @AttractionId, @AttractionName, @Preference,
                 @TravelStartsAt, @WalkingMinutes, @QueueStartsAt, @QueueMinutes,
                 @AttractionStartsAt, @AttractionDurationMinutes, @CompletesAt,
                 @Status, @StatusChangedAt);
            """,
            session.Stops.Select(stop => new
            {
                SessionId = session.Id,
                stop.Sequence,
                stop.AttractionId,
                stop.AttractionName,
                Preference = stop.Preference.ToString(),
                stop.TravelStartsAt,
                stop.WalkingMinutes,
                stop.QueueStartsAt,
                stop.QueueMinutes,
                stop.AttractionStartsAt,
                stop.AttractionDurationMinutes,
                stop.CompletesAt,
                Status = stop.Status.ToString(),
                stop.StatusChangedAt
            }),
            transaction,
            cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<VisitSession?> GetAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        using var results = await connection.QueryMultipleAsync(new CommandDefinition(
            """
            SELECT id AS Id,
                   park_id AS ParkId,
                   visit_start_at AS VisitStartAt,
                   visit_end_at AS VisitEndAt,
                   party_size AS PartySize,
                   started_at AS StartedAt,
                   updated_at AS UpdatedAt,
                   status AS Status,
                   total_walking_minutes AS TotalWalkingMinutes,
                   total_queue_minutes AS TotalQueueMinutes,
                   total_attraction_minutes AS TotalAttractionMinutes,
                   algorithm_version AS AlgorithmVersion
            FROM public.visit_sessions
            WHERE id = @SessionId;

            SELECT sequence AS Sequence,
                   attraction_id AS AttractionId,
                   attraction_name AS AttractionName,
                   preference AS Preference,
                   travel_starts_at AS TravelStartsAt,
                   walking_minutes AS WalkingMinutes,
                   queue_starts_at AS QueueStartsAt,
                   queue_minutes AS QueueMinutes,
                   attraction_starts_at AS AttractionStartsAt,
                   attraction_duration_minutes AS AttractionDurationMinutes,
                   completes_at AS CompletesAt,
                   status AS Status,
                   status_changed_at AS StatusChangedAt
            FROM public.visit_session_stops
            WHERE session_id = @SessionId
            ORDER BY sequence;
            """,
            new { SessionId = sessionId },
            cancellationToken: cancellationToken));

        var session = await results.ReadSingleOrDefaultAsync<VisitSessionRow>();
        if (session is null)
        {
            return null;
        }

        var stops = (await results.ReadAsync<VisitSessionStopRow>())
            .Select(MapStop)
            .ToArray();
        return new VisitSession(
            session.Id,
            session.ParkId,
            session.VisitStartAt,
            session.VisitEndAt,
            session.PartySize,
            session.StartedAt,
            session.UpdatedAt,
            Enum.Parse<VisitSessionStatus>(session.Status),
            stops,
            session.TotalWalkingMinutes,
            session.TotalQueueMinutes,
            session.TotalAttractionMinutes,
            session.AlgorithmVersion);
    }

    public async Task<bool> TrySetStopStatusAsync(
        Guid sessionId,
        long attractionId,
        VisitSessionStopStatus status,
        DateTimeOffset changedAt,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var sessionExists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT EXISTS (
                SELECT 1
                FROM public.visit_sessions
                WHERE id = @SessionId
                FOR UPDATE
            );
            """,
            new { SessionId = sessionId },
            transaction,
            cancellationToken: cancellationToken));
        if (!sessionExists)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        var changed = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE public.visit_session_stops
            SET status = @Status,
                status_changed_at = @ChangedAt
            WHERE session_id = @SessionId
              AND attraction_id = @AttractionId
              AND status = 'Pending';
            """,
            new
            {
                SessionId = sessionId,
                AttractionId = attractionId,
                Status = status.ToString(),
                ChangedAt = changedAt
            },
            transaction,
            cancellationToken: cancellationToken));
        if (changed == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE public.visit_sessions
            SET updated_at = @ChangedAt,
                status = CASE
                    WHEN EXISTS (
                        SELECT 1
                        FROM public.visit_session_stops
                        WHERE session_id = @SessionId
                          AND status = 'Pending'
                    )
                    THEN 'Active'
                    ELSE 'Completed'
                END
            WHERE id = @SessionId;
            """,
            new { SessionId = sessionId, ChangedAt = changedAt },
            transaction,
            cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> TryReplacePendingStopsAsync(
        Guid sessionId,
        DateTimeOffset expectedUpdatedAt,
        IReadOnlyList<VisitSessionStop> pendingStops,
        int totalWalkingMinutes,
        int totalQueueMinutes,
        int totalAttractionMinutes,
        string algorithmVersion,
        DateTimeOffset changedAt,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var sessionIsCurrent = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                """
                SELECT EXISTS (
                    SELECT 1
                    FROM public.visit_sessions
                    WHERE id = @SessionId
                      AND updated_at = @ExpectedUpdatedAt
                    FOR UPDATE
                );
                """,
                new { SessionId = sessionId, ExpectedUpdatedAt = expectedUpdatedAt },
                transaction,
                cancellationToken: cancellationToken));
        if (!sessionIsCurrent)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            """
            DELETE FROM public.visit_session_stops
            WHERE session_id = @SessionId
              AND status = 'Pending';
            """,
            new { SessionId = sessionId },
            transaction,
            cancellationToken: cancellationToken));

        if (pendingStops.Count > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO public.visit_session_stops
                    (session_id, sequence, attraction_id, attraction_name, preference,
                     travel_starts_at, walking_minutes, queue_starts_at, queue_minutes,
                     attraction_starts_at, attraction_duration_minutes, completes_at,
                     status, status_changed_at)
                VALUES
                    (@SessionId, @Sequence, @AttractionId, @AttractionName, @Preference,
                     @TravelStartsAt, @WalkingMinutes, @QueueStartsAt, @QueueMinutes,
                     @AttractionStartsAt, @AttractionDurationMinutes, @CompletesAt,
                     'Pending', NULL);
                """,
                pendingStops.Select(stop => new
                {
                    SessionId = sessionId,
                    stop.Sequence,
                    stop.AttractionId,
                    stop.AttractionName,
                    Preference = stop.Preference.ToString(),
                    stop.TravelStartsAt,
                    stop.WalkingMinutes,
                    stop.QueueStartsAt,
                    stop.QueueMinutes,
                    stop.AttractionStartsAt,
                    stop.AttractionDurationMinutes,
                    stop.CompletesAt
                }),
                transaction,
                cancellationToken: cancellationToken));
        }

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE public.visit_sessions
            SET updated_at = @ChangedAt,
                status = CASE
                    WHEN @PendingStopCount > 0 THEN 'Active'
                    ELSE 'Completed'
                END,
                total_walking_minutes = @TotalWalkingMinutes,
                total_queue_minutes = @TotalQueueMinutes,
                total_attraction_minutes = @TotalAttractionMinutes,
                algorithm_version = @AlgorithmVersion
            WHERE id = @SessionId;
            """,
            new
            {
                SessionId = sessionId,
                ChangedAt = changedAt,
                PendingStopCount = pendingStops.Count,
                TotalWalkingMinutes = totalWalkingMinutes,
                TotalQueueMinutes = totalQueueMinutes,
                TotalAttractionMinutes = totalAttractionMinutes,
                AlgorithmVersion = algorithmVersion
            },
            transaction,
            cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static VisitSessionStop MapStop(VisitSessionStopRow stop) =>
        new(
            stop.Sequence,
            stop.AttractionId,
            stop.AttractionName,
            Enum.Parse<AttractionPreferenceLevel>(stop.Preference),
            stop.TravelStartsAt,
            stop.WalkingMinutes,
            stop.QueueStartsAt,
            stop.QueueMinutes,
            stop.AttractionStartsAt,
            stop.AttractionDurationMinutes,
            stop.CompletesAt,
            Enum.Parse<VisitSessionStopStatus>(stop.Status),
            stop.StatusChangedAt);

    private sealed record VisitSessionRow(
        Guid Id,
        long ParkId,
        DateTimeOffset VisitStartAt,
        DateTimeOffset VisitEndAt,
        int PartySize,
        DateTimeOffset StartedAt,
        DateTimeOffset UpdatedAt,
        string Status,
        int TotalWalkingMinutes,
        int TotalQueueMinutes,
        int TotalAttractionMinutes,
        string AlgorithmVersion);

    private sealed record VisitSessionStopRow(
        int Sequence,
        long AttractionId,
        string AttractionName,
        string Preference,
        DateTimeOffset TravelStartsAt,
        int WalkingMinutes,
        DateTimeOffset QueueStartsAt,
        int QueueMinutes,
        DateTimeOffset AttractionStartsAt,
        int AttractionDurationMinutes,
        DateTimeOffset CompletesAt,
        string Status,
        DateTimeOffset? StatusChangedAt);
}
