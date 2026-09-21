using System;

namespace StellarFramework.WorldKit.Streaming
{
    /// <summary>
    /// Concentric Chebyshev-distance demand policy.
    /// Radii are inclusive and must be monotonic from Metadata down to Presentation.
    /// </summary>
    public readonly struct WorldStreamingPolicy : IEquatable<WorldStreamingPolicy>
    {
        private readonly bool _initialized;

        public int MetadataRadius { get; }
        public int DataRadius { get; }
        public int SimulationRadius { get; }
        public int PresentationRadius { get; }

        public bool IsValid =>
            _initialized &&
            MetadataRadius >= DataRadius &&
            DataRadius >= SimulationRadius &&
            SimulationRadius >= PresentationRadius &&
            PresentationRadius >= 0;

        public WorldStreamingPolicy(
            int metadataRadius,
            int dataRadius,
            int simulationRadius,
            int presentationRadius)
        {
            if (presentationRadius < 0)
                throw new ArgumentOutOfRangeException(nameof(presentationRadius));
            if (simulationRadius < presentationRadius)
                throw new ArgumentOutOfRangeException(nameof(simulationRadius));
            if (dataRadius < simulationRadius)
                throw new ArgumentOutOfRangeException(nameof(dataRadius));
            if (metadataRadius < dataRadius)
                throw new ArgumentOutOfRangeException(nameof(metadataRadius));

            MetadataRadius = metadataRadius;
            DataRadius = dataRadius;
            SimulationRadius = simulationRadius;
            PresentationRadius = presentationRadius;
            _initialized = true;
        }

        public WorldStreamingTier GetTier(int chebyshevDistance)
        {
            if (!IsValid)
                throw new InvalidOperationException("WorldStreamingPolicy is not initialized.");
            if (chebyshevDistance < 0)
                throw new ArgumentOutOfRangeException(nameof(chebyshevDistance));

            if (chebyshevDistance <= PresentationRadius) return WorldStreamingTier.Presentation;
            if (chebyshevDistance <= SimulationRadius) return WorldStreamingTier.Simulation;
            if (chebyshevDistance <= DataRadius) return WorldStreamingTier.Data;
            if (chebyshevDistance <= MetadataRadius) return WorldStreamingTier.Metadata;
            return WorldStreamingTier.None;
        }

        public bool Equals(WorldStreamingPolicy other) =>
            _initialized == other._initialized &&
            MetadataRadius == other.MetadataRadius &&
            DataRadius == other.DataRadius &&
            SimulationRadius == other.SimulationRadius &&
            PresentationRadius == other.PresentationRadius;

        public override bool Equals(object obj) =>
            obj is WorldStreamingPolicy other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = MetadataRadius;
                hash = (hash * 397) ^ DataRadius;
                hash = (hash * 397) ^ SimulationRadius;
                hash = (hash * 397) ^ PresentationRadius;
                return (hash * 397) ^ (_initialized ? 1 : 0);
            }
        }

        public static bool operator ==(WorldStreamingPolicy left, WorldStreamingPolicy right) => left.Equals(right);
        public static bool operator !=(WorldStreamingPolicy left, WorldStreamingPolicy right) => !left.Equals(right);
    }
}
