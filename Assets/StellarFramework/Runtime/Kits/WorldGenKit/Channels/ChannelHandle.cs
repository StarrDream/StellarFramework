using System;

namespace StellarFramework.WorldGenKit
{
    public readonly struct ChannelHandle<T> : IEquatable<ChannelHandle<T>>
    {
        public int Index { get; }
        public int RegistryGeneration { get; }
        public bool IsValid => Index >= 0 && RegistryGeneration > 0;

        internal ChannelHandle(int index, int registryGeneration)
        {
            Index = index;
            RegistryGeneration = registryGeneration;
        }

        public bool Equals(ChannelHandle<T> other) => Index == other.Index && RegistryGeneration == other.RegistryGeneration;
        public override bool Equals(object obj) => obj is ChannelHandle<T> other && Equals(other);
        public override int GetHashCode() => unchecked((Index * 397) ^ RegistryGeneration);
        public override string ToString() => IsValid
            ? string.Format("ChannelHandle[{0}]@{1}", Index, RegistryGeneration)
            : "InvalidChannelHandle";
        public static bool operator ==(ChannelHandle<T> left, ChannelHandle<T> right) => left.Equals(right);
        public static bool operator !=(ChannelHandle<T> left, ChannelHandle<T> right) => !left.Equals(right);
    }
}
