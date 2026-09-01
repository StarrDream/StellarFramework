using System;

namespace StellarFramework
{
    /// <summary>Immutable result metadata for one path search.</summary>
    public readonly struct PathSearchResult
    {
        public PathSearchStatus Status { get; }
        public bool Success => Status == PathSearchStatus.Success;
        public int WrittenCount { get; }
        public int RequiredNodeCount { get; }
        public long TotalCost { get; }
        public int ExpandedNodeCount { get; }

        internal PathSearchResult(PathSearchStatus status, int writtenCount, int requiredNodeCount,
            long totalCost, int expandedNodeCount)
        {
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

        public override string ToString()
        {
            return string.Format("{0} (written={1}, required={2}, cost={3}, expanded={4})",
                Status, WrittenCount, RequiredNodeCount, TotalCost, ExpandedNodeCount);
        }
    }
}
