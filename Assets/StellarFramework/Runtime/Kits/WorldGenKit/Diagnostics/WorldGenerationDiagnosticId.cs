using System;

namespace StellarFramework.WorldGenKit
{
    public readonly struct WorldGenerationDiagnosticId : IEquatable<WorldGenerationDiagnosticId>
    {
        public const int MaxLength = 128;
        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);

        private WorldGenerationDiagnosticId(string value) => Value = value;

        public static WorldGenerationDiagnosticId From(string value)
        {
            if (!TryCreate(value, out WorldGenerationDiagnosticId result, out string error))
                throw new ArgumentException(error, nameof(value));
            return result;
        }

        public static bool TryCreate(string value, out WorldGenerationDiagnosticId result, out string error)
        {
            result = default(WorldGenerationDiagnosticId);
            if (!WorldGenStableIdUtility.TryValidate(value, MaxLength, "World generation diagnostic ID", out error)) return false;
            result = new WorldGenerationDiagnosticId(value);
            return true;
        }

        public bool Equals(WorldGenerationDiagnosticId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is WorldGenerationDiagnosticId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(WorldGenerationDiagnosticId left, WorldGenerationDiagnosticId right) => left.Equals(right);
        public static bool operator !=(WorldGenerationDiagnosticId left, WorldGenerationDiagnosticId right) => !left.Equals(right);
    }

    public static class WorldGenerationDiagnosticIds
    {
        public static readonly WorldGenerationDiagnosticId InvalidStageId = WorldGenerationDiagnosticId.From("pipeline.invalid_stage_id");
        public static readonly WorldGenerationDiagnosticId DuplicateStageId = WorldGenerationDiagnosticId.From("pipeline.duplicate_stage_id");
        public static readonly WorldGenerationDiagnosticId InvalidChannelHandle = WorldGenerationDiagnosticId.From("pipeline.invalid_channel_handle");
        public static readonly WorldGenerationDiagnosticId DuplicateChannelReference = WorldGenerationDiagnosticId.From("pipeline.duplicate_channel_reference");
        public static readonly WorldGenerationDiagnosticId MissingSeedScope = WorldGenerationDiagnosticId.From("pipeline.missing_seed_scope");
        public static readonly WorldGenerationDiagnosticId MissingProducer = WorldGenerationDiagnosticId.From("pipeline.missing_producer");
        public static readonly WorldGenerationDiagnosticId DuplicateProducer = WorldGenerationDiagnosticId.From("pipeline.duplicate_producer");
        public static readonly WorldGenerationDiagnosticId DuplicateMutator = WorldGenerationDiagnosticId.From("pipeline.duplicate_mutator");
        public static readonly WorldGenerationDiagnosticId ProvidedChannelProduced = WorldGenerationDiagnosticId.From("pipeline.provided_channel_produced");
        public static readonly WorldGenerationDiagnosticId SelfDependency = WorldGenerationDiagnosticId.From("pipeline.self_dependency");
        public static readonly WorldGenerationDiagnosticId DependencyCycle = WorldGenerationDiagnosticId.From("pipeline.dependency_cycle");
        public static readonly WorldGenerationDiagnosticId BufferTooSmall = WorldGenerationDiagnosticId.From("runtime.buffer_too_small");
        public static readonly WorldGenerationDiagnosticId UnboundChannel = WorldGenerationDiagnosticId.From("runtime.unbound_channel");
        public static readonly WorldGenerationDiagnosticId RegistryMismatch = WorldGenerationDiagnosticId.From("runtime.registry_mismatch");
    }
}
