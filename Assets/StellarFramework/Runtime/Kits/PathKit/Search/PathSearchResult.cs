using System;

namespace StellarFramework
{
    /// <summary>Immutable metadata describing the outcome of one path search.</summary>
    /// <remarks>
    /// The actual path remains in the caller-provided destination span. Failure results never expose a
    /// partially written path. For <see cref="PathSearchStatus.OutputBufferTooSmall"/>, WrittenCount is 0
    /// and RequiredNodeCount contains the exact path length required for a retry.
    /// </remarks>
    public readonly struct PathSearchResult
    {
        /// <summary>Gets the final search status.</summary>
        public PathSearchStatus Status { get; }

        /// <summary>Gets whether the goal was reached and the complete path was written.</summary>
        public bool Success => Status == PathSearchStatus.Success;

        /// <summary>Gets the number of path nodes written into the caller buffer.</summary>
        public int WrittenCount { get; }

        /// <summary>Gets the required path-node count when known.</summary>
        public int RequiredNodeCount { get; }

        /// <summary>Gets the accumulated traversal cost of the discovered path, or 0 when unavailable.</summary>
        public long TotalCost { get; }

        /// <summary>Gets the number of nodes expanded while resolving this request.</summary>
        public int ExpandedNodeCount { get; }

        internal PathSearchResult(PathSearchStatus status, int writtenCount, int requiredNodeCount,
            long totalCost, int expandedNodeCount)
        {
            if (status == PathSearchStatus.None)
            {
                throw new ArgumentOutOfRangeException(nameof(status), status,
                    "An executed path search result cannot use PathSearchStatus.None.");
            }

            Status = status;
            WrittenCount = writtenCount;
            RequiredNodeCount = requiredNodeCount;
            TotalCost = totalCost;
            ExpandedNodeCount = expandedNodeCount;
        }

        internal static PathSearchResult Failure(PathSearchStatus status, int expandedNodeCount)
        {
            return new PathSearchResult(status, 0, 0, 0, expandedNodeCount);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return string.Format("{0} (written={1}, required={2}, cost={3}, expanded={4})",
                Status, WrittenCount, RequiredNodeCount, TotalCost, ExpandedNodeCount);
        }
    }
}
