using System;

namespace StellarFramework.WorldGenKit
{
    public readonly struct WorldGenerationStageId : IEquatable<WorldGenerationStageId>
    {
        public const int MaxLength = 128;
        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);

        private WorldGenerationStageId(string value) => Value = value;

        public static WorldGenerationStageId From(string value)
        {
            if (!TryCreate(value, out WorldGenerationStageId result, out string error))
                throw new ArgumentException(error, nameof(value));
            return result;
        }

        public static bool TryCreate(string value, out WorldGenerationStageId result, out string error)
        {
            result = default(WorldGenerationStageId);
            if (!WorldGenStableIdUtility.TryValidate(value, MaxLength, "World generation stage ID", out error)) return false;
            result = new WorldGenerationStageId(value);
            return true;
        }

        public bool Equals(WorldGenerationStageId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is WorldGenerationStageId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(WorldGenerationStageId left, WorldGenerationStageId right) => left.Equals(right);
        public static bool operator !=(WorldGenerationStageId left, WorldGenerationStageId right) => !left.Equals(right);
    }
}
