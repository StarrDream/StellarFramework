using System;

namespace StellarFramework
{
    /// <summary>Common synchronous contract implemented by PathKit pathfinders.</summary>
    /// <remarks>
    /// Searches write directly into caller-owned memory and do not allocate a result path collection.
    /// Pathfinder instances reuse internal workspace and therefore are not thread-safe for concurrent calls.
    /// </remarks>
    public interface IPathfinder
    {
        /// <summary>Finds a path from request start to goal and writes it in Start-to-Goal order.</summary>
        /// <param name="graph">Read-only graph source. It must remain stable for the duration of this call.</param>
        /// <param name="request">Start, goal, and expansion budget.</param>
        /// <param name="destination">Caller-owned output buffer.</param>
        /// <returns>
        /// Search metadata describing success or failure. If the buffer is too small, no partial path is
        /// written and <see cref="PathSearchResult.RequiredNodeCount"/> reports the required size.
        /// </returns>
        /// <exception cref="ArgumentNullException">The graph is null.</exception>
        PathSearchResult FindPath(IPathGraph graph, PathSearchRequest request, Span<PathNodeId> destination);
    }
}
