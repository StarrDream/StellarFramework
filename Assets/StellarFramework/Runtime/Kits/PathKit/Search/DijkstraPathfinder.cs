using System;

namespace StellarFramework
{
    /// <summary>Dijkstra shortest-path search. It never calls IPathGraph.EstimateCost.</summary>
    public sealed class DijkstraPathfinder : IPathfinder
    {
        private readonly PathSearchWorkspace _workspace;

        public DijkstraPathfinder(int initialCapacity = 0)
        {
            _workspace = new PathSearchWorkspace(initialCapacity);
        }

        public PathSearchResult FindPath(IPathGraph graph, PathSearchRequest request, Span<PathNodeId> destination)
        {
            return PathSearchRunner.Run(graph, request, destination, false, _workspace);
        }
    }
}
