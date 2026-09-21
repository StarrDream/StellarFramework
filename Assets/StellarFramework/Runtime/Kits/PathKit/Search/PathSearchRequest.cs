using System;

namespace StellarFramework
{
    /// <summary>Immutable input for one synchronous, bounded path search.</summary>
    public readonly struct PathSearchRequest
    {
        /// <summary>Gets the requested start node.</summary>
        public PathNodeId Start { get; }

        /// <summary>Gets the requested goal node.</summary>
        public PathNodeId Goal { get; }

        /// <summary>
        /// Gets the maximum number of non-goal nodes the search may expand before returning
        /// <see cref="PathSearchStatus.ExpansionLimitReached"/>.
        /// </summary>
        public int MaxExpandedNodes { get; }

        /// <summary>Creates a bounded path-search request.</summary>
        /// <param name="start">Start node identity. Invalid ids are reported by the search result.</param>
        /// <param name="goal">Goal node identity. Invalid ids are reported by the search result.</param>
        /// <param name="maxExpandedNodes">Strictly positive expansion budget.</param>
        /// <exception cref="ArgumentOutOfRangeException">The expansion budget is not positive.</exception>
        public PathSearchRequest(PathNodeId start, PathNodeId goal, int maxExpandedNodes = int.MaxValue)
        {
            if (maxExpandedNodes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExpandedNodes), maxExpandedNodes,
                    "MaxExpandedNodes must be greater than zero.");
            }

            Start = start;
            Goal = goal;
            MaxExpandedNodes = maxExpandedNodes;
        }
    }
}
