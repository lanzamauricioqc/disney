namespace Disney.Domain;

public sealed record WalkableRouteEdge(
    long FromNodeId,
    long ToNodeId,
    int DistanceMeters);

public sealed record WalkableRoute(
    IReadOnlyList<long> NodeIds,
    int DistanceMeters);

public sealed class WalkableParkGraph
{
    private readonly IReadOnlyDictionary<long, IReadOnlyList<WalkableRouteEdge>>
        edgesByNode;

    public WalkableParkGraph(
        IEnumerable<long> nodeIds,
        IEnumerable<WalkableRouteEdge> edges)
    {
        var nodes = nodeIds.ToHashSet();
        var edgeList = edges.ToArray();

        if (nodes.Any(nodeId => nodeId <= 0))
        {
            throw new ArgumentOutOfRangeException(
                nameof(nodeIds),
                "Route node ids must be greater than zero.");
        }

        if (edgeList.Any(edge => edge.DistanceMeters <= 0))
        {
            throw new ArgumentOutOfRangeException(
                nameof(edges),
                "Route edge distances must be greater than zero.");
        }

        if (edgeList.Any(edge =>
                !nodes.Contains(edge.FromNodeId) ||
                !nodes.Contains(edge.ToNodeId)))
        {
            throw new ArgumentException(
                "Every route edge must reference nodes in the graph.",
                nameof(edges));
        }

        edgesByNode = nodes.ToDictionary(
            nodeId => nodeId,
            nodeId => (IReadOnlyList<WalkableRouteEdge>)edgeList
                .Where(edge => edge.FromNodeId == nodeId)
                .OrderBy(edge => edge.ToNodeId)
                .ThenBy(edge => edge.DistanceMeters)
                .ToArray());
    }

    public WalkableRoute? FindShortestRoute(long fromNodeId, long toNodeId)
    {
        if (!edgesByNode.ContainsKey(fromNodeId) ||
            !edgesByNode.ContainsKey(toNodeId))
        {
            return null;
        }

        if (fromNodeId == toNodeId)
        {
            return new WalkableRoute([fromNodeId], 0);
        }

        var distances = new Dictionary<long, int> { [fromNodeId] = 0 };
        var previousNodes = new Dictionary<long, long>();
        var pendingNodes = new PriorityQueue<long, (int Distance, long NodeId)>();
        pendingNodes.Enqueue(fromNodeId, (0, fromNodeId));

        while (pendingNodes.TryDequeue(out var currentNodeId, out var queuedPriority))
        {
            var queuedDistance = queuedPriority.Distance;
            if (queuedDistance > distances[currentNodeId])
            {
                continue;
            }

            if (currentNodeId == toNodeId)
            {
                return BuildRoute(
                    fromNodeId,
                    toNodeId,
                    distances[toNodeId],
                    previousNodes);
            }

            foreach (var edge in edgesByNode[currentNodeId])
            {
                var candidateDistance = checked(queuedDistance + edge.DistanceMeters);
                if (distances.TryGetValue(edge.ToNodeId, out var existingDistance) &&
                    existingDistance <= candidateDistance)
                {
                    continue;
                }

                distances[edge.ToNodeId] = candidateDistance;
                previousNodes[edge.ToNodeId] = currentNodeId;
                pendingNodes.Enqueue(
                    edge.ToNodeId,
                    (candidateDistance, edge.ToNodeId));
            }
        }

        return null;
    }

    private static WalkableRoute BuildRoute(
        long fromNodeId,
        long toNodeId,
        int distanceMeters,
        IReadOnlyDictionary<long, long> previousNodes)
    {
        var nodeIds = new List<long> { toNodeId };
        var currentNodeId = toNodeId;

        while (currentNodeId != fromNodeId)
        {
            currentNodeId = previousNodes[currentNodeId];
            nodeIds.Add(currentNodeId);
        }

        nodeIds.Reverse();
        return new WalkableRoute(nodeIds, distanceMeters);
    }
}
