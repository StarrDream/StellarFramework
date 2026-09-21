using System;

namespace StellarFramework.WorldKit
{
    public readonly struct WorldDataLayerHandle<T> : IEquatable<WorldDataLayerHandle<T>>
    {
        public int Index { get; }
        public int RegistryGeneration { get; }
        public bool IsValid => Index >= 0 && RegistryGeneration > 0;

        internal WorldDataLayerHandle(int index, int registryGeneration)
        {
            Index = index;
            RegistryGeneration = registryGeneration;
        }

        public bool Equals(WorldDataLayerHandle<T> other) =>
            Index == other.Index && RegistryGeneration == other.RegistryGeneration;

        public override bool Equals(object obj) => obj is WorldDataLayerHandle<T> other && Equals(other);
        public override int GetHashCode() => unchecked((Index * 397) ^ RegistryGeneration);
        public override string ToString() => IsValid
            ? string.Format("LayerHandle[{0}]@{1}", Index, RegistryGeneration)
            : "InvalidLayerHandle";

        public static bool operator ==(WorldDataLayerHandle<T> left, WorldDataLayerHandle<T> right) => left.Equals(right);
        public static bool operator !=(WorldDataLayerHandle<T> left, WorldDataLayerHandle<T> right) => !left.Equals(right);
    }
}
