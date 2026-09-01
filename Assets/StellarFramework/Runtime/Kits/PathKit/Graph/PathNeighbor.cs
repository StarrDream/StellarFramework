using System;

namespace StellarFramework
{
    /// <summary>One directed outgoing edge and its positive integral traversal cost.</summary>
    public readonly struct PathNeighbor
    {
        public PathNodeId Node { get; }
        public long Cost { get; }

        public PathNeighbor(PathNodeId node, long cost)
        {
            if (!node.IsValid)
            {
                throw new ArgumentException("A path neighbor must reference a valid node.", nameof(node));
            }

            if (cost <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cost), cost,
                    "Path edge costs must be positive.");
            }

            Node = node;
            Cost = cost;
        }
    }
}
