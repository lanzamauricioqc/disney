using Dapper;
using Disney.Application;
using Disney.Domain;

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
        using var results = await connection.QueryMultipleAsync(new CommandDefinition(
                """
                SELECT origin.id AS FromAttractionId,
                       origin.name AS FromAttractionName,
                       origin.latitude AS FromLatitude,
                       origin.longitude AS FromLongitude,
                       origin.route_node_id AS FromRouteNodeId,
                       destination.id AS ToAttractionId,
                       destination.name AS ToAttractionName,
                       destination.latitude AS ToLatitude,
                       destination.longitude AS ToLongitude,
                       destination.route_node_id AS ToRouteNodeId
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

                SELECT node.id
                FROM public.park_route_nodes node
                WHERE node.park_id = @ParkId
                  AND node.is_active;

                SELECT edge.from_node_id AS FromNodeId,
                       edge.to_node_id AS ToNodeId,
                       edge.distance_meters AS DistanceMeters
                FROM public.park_route_edges edge
                JOIN public.park_route_nodes origin
                  ON origin.id = edge.from_node_id
                 AND origin.park_id = edge.park_id
                 AND origin.is_active
                JOIN public.park_route_nodes destination
                  ON destination.id = edge.to_node_id
                 AND destination.park_id = edge.park_id
                 AND destination.is_active
                WHERE edge.park_id = @ParkId
                  AND edge.is_active
                UNION ALL
                SELECT edge.to_node_id AS FromNodeId,
                       edge.from_node_id AS ToNodeId,
                       edge.distance_meters AS DistanceMeters
                FROM public.park_route_edges edge
                JOIN public.park_route_nodes origin
                  ON origin.id = edge.from_node_id
                 AND origin.park_id = edge.park_id
                 AND origin.is_active
                JOIN public.park_route_nodes destination
                  ON destination.id = edge.to_node_id
                 AND destination.park_id = edge.park_id
                 AND destination.is_active
                WHERE edge.park_id = @ParkId
                  AND edge.is_active
                  AND edge.is_bidirectional;
                """,
                new
                {
                    ParkId = parkId,
                    FromAttractionId = fromAttractionId,
                    ToAttractionId = toAttractionId
                },
                cancellationToken: cancellationToken));

        var attractionData = await results.ReadSingleOrDefaultAsync<AttractionRouteRow>();
        if (attractionData is null)
        {
            return null;
        }

        var routeNodeIds = (await results.ReadAsync<long>()).ToArray();
        var routeEdges = (await results.ReadAsync<WalkableRouteEdge>()).ToArray();
        return new WalkingTimeData(
            attractionData.FromAttractionId,
            attractionData.FromAttractionName,
            attractionData.FromLatitude,
            attractionData.FromLongitude,
            attractionData.ToAttractionId,
            attractionData.ToAttractionName,
            attractionData.ToLatitude,
            attractionData.ToLongitude,
            attractionData.FromRouteNodeId,
            attractionData.ToRouteNodeId,
            routeNodeIds,
            routeEdges);
    }

    private sealed record AttractionRouteRow(
        long FromAttractionId,
        string FromAttractionName,
        decimal? FromLatitude,
        decimal? FromLongitude,
        long ToAttractionId,
        string ToAttractionName,
        decimal? ToLatitude,
        decimal? ToLongitude,
        long? FromRouteNodeId,
        long? ToRouteNodeId);
}
