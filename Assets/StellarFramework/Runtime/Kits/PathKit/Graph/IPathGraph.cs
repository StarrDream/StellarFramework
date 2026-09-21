namespace StellarFramework
{
    /// <summary>
    /// Read-only graph boundary consumed by PathKit. Implementations own node ids,
    /// topology, traversal state and heuristic data.
    /// </summary>
    /// <remarks>
    /// PathKit never owns or mutates graph data. Implementations must return stable node identities for
    /// the duration of a search, positive edge costs, and a non-negative heuristic. A* requires the
    /// heuristic to be admissible if shortest-path optimality is required; Dijkstra ignores it entirely.
    /// </remarks>
    public interface IPathGraph
    {
        /// <summary>Checks whether a node id currently belongs to this graph.</summary>
        /// <param name="node">Caller-owned node identity.</param>
        /// <returns><see langword="true"/> when the node can participate in a search.</returns>
        bool ContainsNode(PathNodeId node);

        /// <summary>Gets the number of outgoing neighbors exposed by a node.</summary>
        /// <param name="node">A valid node contained by this graph.</param>
        /// <returns>A non-negative outgoing-neighbor count.</returns>
        int GetNeighborCount(PathNodeId node);

        /// <summary>Gets one outgoing edge by zero-based neighbor index.</summary>
        /// <param name="node">A valid node contained by this graph.</param>
        /// <param name="neighborIndex">Index in the range returned by <see cref="GetNeighborCount"/>.</param>
        /// <returns>The target node and positive traversal cost for the selected edge.</returns>
        PathNeighbor GetNeighbor(PathNodeId node, int neighborIndex);

        /// <summary>Estimates the remaining cost from one node to the requested goal.</summary>
        /// <param name="from">Node being evaluated by A*.</param>
        /// <param name="goal">Search goal.</param>
        /// <returns>A non-negative estimate. Return 0 to disable heuristic guidance.</returns>
        /// <remarks>DijkstraPathfinder never calls this member.</remarks>
        long EstimateCost(PathNodeId from, PathNodeId goal);
    }
}
