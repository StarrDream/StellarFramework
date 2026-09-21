using System;

namespace StellarFramework
{
    /// <summary>Reusable synchronous A* pathfinder over an application-provided <see cref="IPathGraph"/>.</summary>
    /// <remarks>
    /// The instance reuses internal arrays between searches to reduce GC pressure. It is not safe to use the
    /// same instance concurrently. Closed nodes may be reopened when a cheaper path is found, allowing
    /// correct behavior with admissible but inconsistent heuristics.
    /// </remarks>
    public sealed class AStarPathfinder : IPathfinder
    {
        private readonly PathSearchWorkspace _workspace;

        /// <summary>Creates an A* pathfinder and optionally preallocates reusable search workspace.</summary>
        /// <param name="initialCapacity">Initial record/open-set capacity. Use 0 to grow on demand.</param>
        public AStarPathfinder(int initialCapacity = 0)
        {
            _workspace = new PathSearchWorkspace(initialCapacity);
        }

        /// <inheritdoc />
        public PathSearchResult FindPath(IPathGraph graph, PathSearchRequest request, Span<PathNodeId> destination)
        {
            return PathSearchRunner.Run(graph, request, destination, true, _workspace);
        }
    }
}
