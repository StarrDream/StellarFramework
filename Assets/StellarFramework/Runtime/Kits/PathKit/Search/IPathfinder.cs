using System;

namespace StellarFramework
{
    /// <summary>Common synchronous contract implemented by the V1 pathfinders.</summary>
    public interface IPathfinder
    {
        PathSearchResult FindPath(IPathGraph graph, PathSearchRequest request, Span<PathNodeId> destination);
    }
}
