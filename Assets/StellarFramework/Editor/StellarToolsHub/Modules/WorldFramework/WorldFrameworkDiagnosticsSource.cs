using System;
using System.Collections.Generic;
using StellarFramework.WorldGenKit;
using StellarFramework.WorldKit;

namespace StellarFramework.Editor.Modules.WorldFramework
{
    public readonly struct WorldFrameworkRuntimeSnapshot
    {
        public WorldId WorldId { get; }
        public WorldExtent Extent { get; }
        public int RegisteredChunks { get; }
        public int UnloadedChunks { get; }
        public int MetadataChunks { get; }
        public int DataReadyChunks { get; }
        public int ActiveChunks { get; }
        public int StreamingMetadataChunks { get; }
        public int StreamingDataChunks { get; }
        public int StreamingSimulationChunks { get; }
        public int StreamingPresentationChunks { get; }
        public int DirtyChunks { get; }
        public int DeltaCount { get; }
        public long ApproximateManagedBytes { get; }

        public WorldFrameworkRuntimeSnapshot(
            WorldId worldId,
            WorldExtent extent,
            int registeredChunks,
            int unloadedChunks,
            int metadataChunks,
            int dataReadyChunks,
            int activeChunks,
            int streamingMetadataChunks,
            int streamingDataChunks,
            int streamingSimulationChunks,
            int streamingPresentationChunks,
            int dirtyChunks,
            int deltaCount,
            long approximateManagedBytes)
        {
            if (!worldId.IsValid) throw new ArgumentException("World ID must be valid.", nameof(worldId));
            if (!extent.IsValid) throw new ArgumentException("World extent must be valid.", nameof(extent));
            ValidateNonNegative(registeredChunks, nameof(registeredChunks));
            ValidateNonNegative(unloadedChunks, nameof(unloadedChunks));
            ValidateNonNegative(metadataChunks, nameof(metadataChunks));
            ValidateNonNegative(dataReadyChunks, nameof(dataReadyChunks));
            ValidateNonNegative(activeChunks, nameof(activeChunks));
            ValidateNonNegative(streamingMetadataChunks, nameof(streamingMetadataChunks));
            ValidateNonNegative(streamingDataChunks, nameof(streamingDataChunks));
            ValidateNonNegative(streamingSimulationChunks, nameof(streamingSimulationChunks));
            ValidateNonNegative(streamingPresentationChunks, nameof(streamingPresentationChunks));
            ValidateNonNegative(dirtyChunks, nameof(dirtyChunks));
            ValidateNonNegative(deltaCount, nameof(deltaCount));
            if (approximateManagedBytes < 0L) throw new ArgumentOutOfRangeException(nameof(approximateManagedBytes));

            int lifecycleTotal = checked(unloadedChunks + metadataChunks + dataReadyChunks + activeChunks);
            if (lifecycleTotal != registeredChunks)
                throw new ArgumentException("Chunk lifecycle counts must sum to RegisteredChunks.", nameof(registeredChunks));

            WorldId = worldId;
            Extent = extent;
            RegisteredChunks = registeredChunks;
            UnloadedChunks = unloadedChunks;
            MetadataChunks = metadataChunks;
            DataReadyChunks = dataReadyChunks;
            ActiveChunks = activeChunks;
            StreamingMetadataChunks = streamingMetadataChunks;
            StreamingDataChunks = streamingDataChunks;
            StreamingSimulationChunks = streamingSimulationChunks;
            StreamingPresentationChunks = streamingPresentationChunks;
            DirtyChunks = dirtyChunks;
            DeltaCount = deltaCount;
            ApproximateManagedBytes = approximateManagedBytes;
        }

        private static void ValidateNonNegative(int value, string name)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(name);
        }
    }

    /// <summary>
    /// Editor-only bridge from a project's world service into ToolsHub. Runtime kits never discover or own this source.
    /// </summary>
    public interface IWorldFrameworkDiagnosticsSource
    {
        string DisplayName { get; }
        bool TryCaptureRuntimeSnapshot(out WorldFrameworkRuntimeSnapshot snapshot, out string error);
        WorldGenerationPlan GenerationPlan { get; }
        WorldGenerationReport LastGenerationReport { get; }
    }

    public readonly struct WorldChunkDiagnosticSnapshot
    {
        public WorldChunkCoord Coord { get; }
        public WorldRegionCoord Region { get; }
        public WorldChunkState LifecycleState { get; }
        public string StreamingState { get; }
        public bool Dirty { get; }
        public long ApproximateManagedBytes { get; }

        public WorldChunkDiagnosticSnapshot(
            WorldChunkCoord coord,
            WorldRegionCoord region,
            WorldChunkState lifecycleState,
            string streamingState,
            bool dirty,
            long approximateManagedBytes)
        {
            if (lifecycleState < WorldChunkState.Unloaded || lifecycleState > WorldChunkState.Active)
                throw new ArgumentOutOfRangeException(nameof(lifecycleState));
            if (string.IsNullOrWhiteSpace(streamingState))
                throw new ArgumentException("Streaming state label is required.", nameof(streamingState));
            if (approximateManagedBytes < 0L)
                throw new ArgumentOutOfRangeException(nameof(approximateManagedBytes));
            Coord = coord;
            Region = region;
            LifecycleState = lifecycleState;
            StreamingState = streamingState;
            Dirty = dirty;
            ApproximateManagedBytes = approximateManagedBytes;
        }
    }

    public readonly struct WorldDataLayerDiagnosticSnapshot
    {
        public WorldDataLayerId LayerId { get; }
        public WorldDataLayerScope Scope { get; }
        public string Owner { get; }
        public string ValueType { get; }
        public long ApproximateManagedBytes { get; }

        public WorldDataLayerDiagnosticSnapshot(
            WorldDataLayerId layerId,
            WorldDataLayerScope scope,
            string owner,
            string valueType,
            long approximateManagedBytes)
        {
            if (!layerId.IsValid) throw new ArgumentException("Layer ID must be valid.", nameof(layerId));
            if (scope < WorldDataLayerScope.World || scope > WorldDataLayerScope.Chunk)
                throw new ArgumentOutOfRangeException(nameof(scope));
            if (string.IsNullOrWhiteSpace(owner))
                throw new ArgumentException("Layer owner label is required.", nameof(owner));
            if (string.IsNullOrWhiteSpace(valueType))
                throw new ArgumentException("Layer value type label is required.", nameof(valueType));
            if (approximateManagedBytes < 0L)
                throw new ArgumentOutOfRangeException(nameof(approximateManagedBytes));
            LayerId = layerId;
            Scope = scope;
            Owner = owner;
            ValueType = valueType;
            ApproximateManagedBytes = approximateManagedBytes;
        }
    }

    public readonly struct WorldDeltaDiagnosticSnapshot
    {
        public ulong Sequence { get; }
        public WorldDeltaTypeId TypeId { get; }
        public WorldDeltaVersion Version { get; }
        public WorldDeltaTarget Target { get; }
        public long ApproximateManagedBytes { get; }

        public WorldDeltaDiagnosticSnapshot(
            ulong sequence,
            WorldDeltaTypeId typeId,
            WorldDeltaVersion version,
            WorldDeltaTarget target,
            long approximateManagedBytes)
        {
            if (sequence == 0UL) throw new ArgumentOutOfRangeException(nameof(sequence));
            if (!typeId.IsValid) throw new ArgumentException("Delta type ID must be valid.", nameof(typeId));
            if (!version.IsValid) throw new ArgumentException("Delta version must be valid.", nameof(version));
            if (!target.IsValid) throw new ArgumentException("Delta target must be valid.", nameof(target));
            if (approximateManagedBytes < 0L)
                throw new ArgumentOutOfRangeException(nameof(approximateManagedBytes));
            Sequence = sequence;
            TypeId = typeId;
            Version = version;
            Target = target;
            ApproximateManagedBytes = approximateManagedBytes;
        }
    }

    public sealed class WorldFrameworkDetailSnapshot
    {
        private readonly WorldChunkDiagnosticSnapshot[] _chunks;
        private readonly WorldDataLayerDiagnosticSnapshot[] _layers;
        private readonly WorldDeltaDiagnosticSnapshot[] _deltas;

        public int ChunkCount => _chunks.Length;
        public int DataLayerCount => _layers.Length;
        public int DeltaCount => _deltas.Length;

        public WorldFrameworkDetailSnapshot(
            WorldChunkDiagnosticSnapshot[] chunks,
            WorldDataLayerDiagnosticSnapshot[] layers,
            WorldDeltaDiagnosticSnapshot[] deltas)
        {
            _chunks = chunks == null
                ? Array.Empty<WorldChunkDiagnosticSnapshot>()
                : (WorldChunkDiagnosticSnapshot[])chunks.Clone();
            _layers = layers == null
                ? Array.Empty<WorldDataLayerDiagnosticSnapshot>()
                : (WorldDataLayerDiagnosticSnapshot[])layers.Clone();
            _deltas = deltas == null
                ? Array.Empty<WorldDeltaDiagnosticSnapshot>()
                : (WorldDeltaDiagnosticSnapshot[])deltas.Clone();
        }

        public WorldChunkDiagnosticSnapshot GetChunk(int index)
        {
            if ((uint)index >= (uint)_chunks.Length) throw new ArgumentOutOfRangeException(nameof(index));
            return _chunks[index];
        }

        public WorldDataLayerDiagnosticSnapshot GetDataLayer(int index)
        {
            if ((uint)index >= (uint)_layers.Length) throw new ArgumentOutOfRangeException(nameof(index));
            return _layers[index];
        }

        public WorldDeltaDiagnosticSnapshot GetDelta(int index)
        {
            if ((uint)index >= (uint)_deltas.Length) throw new ArgumentOutOfRangeException(nameof(index));
            return _deltas[index];
        }
    }

    /// <summary>
    /// Optional Editor-only extension for detailed chunk/data-layer/delta inspection.
    /// Projects explicitly capture immutable diagnostic DTOs; WorldKit Core remains non-enumerable by ToolsHub.
    /// </summary>
    public interface IWorldFrameworkDetailDiagnosticsSource
    {
        bool TryCaptureDetailSnapshot(
            out WorldFrameworkDetailSnapshot snapshot,
            out string error);
    }

    public static class WorldFrameworkDiagnosticsRegistry
    {
        private static readonly List<IWorldFrameworkDiagnosticsSource> Sources =
            new List<IWorldFrameworkDiagnosticsSource>();

        public static int Count => Sources.Count;

        public static void Register(IWorldFrameworkDiagnosticsSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (string.IsNullOrWhiteSpace(source.DisplayName))
                throw new ArgumentException("Diagnostics source DisplayName must not be empty.", nameof(source));
            if (Sources.Contains(source))
                throw new InvalidOperationException("Diagnostics source is already registered.");
            Sources.Add(source);
        }

        public static bool Unregister(IWorldFrameworkDiagnosticsSource source)
        {
            if (source == null) return false;
            return Sources.Remove(source);
        }

        public static IWorldFrameworkDiagnosticsSource GetAt(int index)
        {
            if ((uint)index >= (uint)Sources.Count) throw new ArgumentOutOfRangeException(nameof(index));
            return Sources[index];
        }

        public static void Clear() => Sources.Clear();
    }
}
