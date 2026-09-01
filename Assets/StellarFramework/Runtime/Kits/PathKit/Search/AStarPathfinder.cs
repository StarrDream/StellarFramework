using System;

namespace StellarFramework
{
    /// <summary>Graph A* search with admissible-heuristic validation and closed-node reopen.</summary>
    public sealed class AStarPathfinder : IPathfinder
    {
        private readonly PathSearchWorkspace _workspace;

        public AStarPathfinder(int initialCapacity = 0)
        {
            _workspace = new PathSearchWorkspace(initialCapacity);
        }

        public PathSearchResult FindPath(IPathGraph graph, PathSearchRequest request, Span<PathNodeId> destination)
        {
            return PathSearchRunner.Run(graph, request, destination, true, _workspace);
        }
    }
}
