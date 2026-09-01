namespace StellarFramework
{
    /// <summary>
    /// Read-only graph boundary consumed by PathKit. Implementations own node ids,
    /// topology, traversal state and heuristic data.
    /// </summary>
    public interface IPathGraph
    {
        bool ContainsNode(PathNodeId node);
        int GetNeighborCount(PathNodeId node);
        PathNeighbor GetNeighbor(PathNodeId node, int neighborIndex);
        long EstimateCost(PathNodeId from, PathNodeId goal);
    }
}
