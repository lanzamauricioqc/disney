using Dapper;
using Disney.Application;

namespace Disney.Infrastructure;

internal sealed class PostgreSqlWalkingTimeReader(
    PostgreSqlConnectionFactory connectionFactory) : IWalkingTimeReader
{
    public async Task<WalkingTimeData?> GetWalkingTimeDataAsync(
        long parkId,
        long fromAttractionId,
        long toAttractionId,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<WalkingTimeData>(
            new CommandDefinition(
                """
                SELECT origin.id AS FromAttractionId,
                       origin.name AS FromAttractionName,
                       origin.latitude AS FromLatitude,
                       origin.longitude AS FromLongitude,
                       destination.id AS ToAttractionId,
                       destination.name AS ToAttractionName,
                       destination.latitude AS ToLatitude,
                       destination.longitude AS ToLongitude
                FROM public.attractions origin
                JOIN public.attractions destination
                  ON destination.park_id = origin.park_id
                JOIN public.parks park
                  ON park.id = origin.park_id
                WHERE park.id = @ParkId
                  AND park.is_active
                  AND origin.id = @FromAttractionId
                  AND origin.is_active
                  AND destination.id = @ToAttractionId
                  AND destination.is_active;
                """,
                new
                {
                    ParkId = parkId,
                    FromAttractionId = fromAttractionId,
                    ToAttractionId = toAttractionId
                },
                cancellationToken: cancellationToken));
    }
}
