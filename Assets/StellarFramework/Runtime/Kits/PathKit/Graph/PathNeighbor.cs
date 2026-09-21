using System;

namespace StellarFramework
{
    /// <summary>One directed outgoing edge and its positive integral traversal cost.</summary>
    public readonly struct PathNeighbor
    {
        /// <summary>Gets the node reached by this directed edge.</summary>
        public PathNodeId Node { get; }

        /// <summary>Gets the strictly positive traversal cost for this edge.</summary>
        public long Cost { get; }

        /// <summary>Creates one outgoing graph edge.</summary>
        /// <param name="node">Destination node. Zero/invalid identities are rejected.</param>
        /// <param name="cost">Strictly positive integral traversal cost.</param>
        /// <exception cref="ArgumentException">The destination node is invalid.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The cost is zero or negative.</exception>
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
