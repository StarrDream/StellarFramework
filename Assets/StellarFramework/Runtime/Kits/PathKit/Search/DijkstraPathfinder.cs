using System;

namespace StellarFramework
{
    /// <summary>Reusable synchronous Dijkstra shortest-path search over an application-provided graph.</summary>
    /// <remarks>
    /// The instance reuses internal workspace to reduce allocations and is not safe for concurrent calls.
    /// It never calls <see cref="IPathGraph.EstimateCost"/>, making it suitable when no admissible heuristic
    /// is available.
    /// </remarks>
    public sealed class DijkstraPathfinder : IPathfinder
    {
        private readonly PathSearchWorkspace _workspace;

        /// <summary>Creates a Dijkstra pathfinder and optionally preallocates reusable search workspace.</summary>
        /// <param name="initialCapacity">Initial record/open-set capacity. Use 0 to grow on demand.</param>
        public DijkstraPathfinder(int initialCapacity = 0)
        {
            _workspace = new PathSearchWorkspace(initialCapacity);
        }

        /// <inheritdoc />
        public PathSearchResult FindPath(IPathGraph graph, PathSearchRequest request, Span<PathNodeId> destination)
        {
            return PathSearchRunner.Run(graph, request, destination, false, _workspace);
        }
    }
}
