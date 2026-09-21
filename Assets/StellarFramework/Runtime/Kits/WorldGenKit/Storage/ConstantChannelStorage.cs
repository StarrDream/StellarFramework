namespace StellarFramework.WorldGenKit
{
    public sealed class ConstantChannelStorage<T> : IWorldChannelStorage<T>
    {
        public WorldChannelStorageKind Kind => WorldChannelStorageKind.Constant;
        public T Value { get; set; }

        public ConstantChannelStorage(T value) => Value = value;
    }
}
