using System;

namespace StellarFramework
{
    /// <summary>Input for one synchronous, bounded path search.</summary>
    public readonly struct PathSearchRequest
    {
        public PathNodeId Start { get; }
        public PathNodeId Goal { get; }
        public int MaxExpandedNodes { get; }

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
