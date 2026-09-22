using Dapper;
using Disney.Application;

namespace Disney.Infrastructure;

internal sealed class PostgreSqlItineraryCandidateReader(
    PostgreSqlConnectionFactory connectionFactory) : IItineraryCandidateReader
{
    public async Task<IReadOnlyList<ItineraryCandidate>> GetCandidatesAsync(
        long parkId,
        IReadOnlyCollection<long> attractionIds,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var candidates = await connection.QueryAsync<ItineraryCandidate>(
            new CommandDefinition(
                """
                SELECT attraction.id AS AttractionId,
                       attraction.name AS AttractionName,
                       attraction.is_active AS IsActive,
                       attraction.duration_minutes::integer AS DurationMinutes,
                       latest_observation.is_open AS IsOpen,
                       latest_observation.wait_minutes AS WaitMinutes
                FROM public.attractions attraction
                JOIN public.parks park
                  ON park.id = attraction.park_id
                LEFT JOIN LATERAL (
                    SELECT observation.is_open,
                           observation.wait_minutes
                    FROM public.queue_observations observation
                    WHERE observation.attraction_id = attraction.id
                      AND observation.park_id = park.id
                      AND observation.is_valid
                    ORDER BY observation.observed_at DESC
                    LIMIT 1
                ) latest_observation ON TRUE
                WHERE park.id = @ParkId
                  AND park.is_active
                  AND attraction.id = ANY(@AttractionIds)
                ORDER BY attraction.id;
                """,
                new
                {
                    ParkId = parkId,
                    AttractionIds = attractionIds.ToArray()
                },
                cancellationToken: cancellationToken));
        return candidates.AsList();
    }
}
