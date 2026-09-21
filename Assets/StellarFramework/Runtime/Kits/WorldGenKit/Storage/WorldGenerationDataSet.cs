using System;

namespace StellarFramework.WorldGenKit
{
    public enum WorldChannelBindingError
    {
        None = 0,
        InvalidHandle,
        NullStorage,
        StorageKindMismatch,
        AlreadyBound
    }

    /// <summary>
    /// Per-run channel storage bindings indexed directly by compiled Channel handle.
    /// Stable string lookup is not used in stage hot paths.
    /// </summary>
    public sealed class WorldGenerationDataSet
    {
        private readonly WorldChannelRegistry _registry;
        private readonly object[] _storages;

        public WorldChannelRegistry Registry => _registry;
        public int BoundCount { get; private set; }

        public WorldGenerationDataSet(WorldChannelRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _storages = new object[registry.Count];
        }

        public void Bind<T>(ChannelHandle<T> handle, IWorldChannelStorage<T> storage)
        {
            if (!TryBind(handle, storage, out WorldChannelBindingError error))
            {
                if (error == WorldChannelBindingError.InvalidHandle || error == WorldChannelBindingError.NullStorage ||
                    error == WorldChannelBindingError.StorageKindMismatch)
                    throw new ArgumentException("Channel binding failed: " + error + ".");
                throw new InvalidOperationException("Channel binding failed: " + error + ".");
            }
        }

        public bool TryBind<T>(
            ChannelHandle<T> handle,
            IWorldChannelStorage<T> storage,
            out WorldChannelBindingError error)
        {
            if (!_registry.TryGetDescriptor(handle, out WorldChannelDescriptor descriptor))
            {
                error = WorldChannelBindingError.InvalidHandle;
                return false;
            }
            if (storage == null)
            {
                error = WorldChannelBindingError.NullStorage;
                return false;
            }
            if (storage.Kind != descriptor.Storage.Kind)
            {
                error = WorldChannelBindingError.StorageKindMismatch;
                return false;
            }
            if (_storages[handle.Index] != null)
            {
                error = WorldChannelBindingError.AlreadyBound;
                return false;
            }

            _storages[handle.Index] = storage;
            BoundCount++;
            error = WorldChannelBindingError.None;
            return true;
        }

        public TStorage GetStorage<T, TStorage>(ChannelHandle<T> handle)
            where TStorage : class, IWorldChannelStorage<T>
        {
            if (!TryGetStorage(handle, out TStorage storage))
                throw new InvalidOperationException("Requested channel storage is unbound, belongs to another registry, or has a different concrete storage type.");
            return storage;
        }

        public bool TryGetStorage<T, TStorage>(ChannelHandle<T> handle, out TStorage storage)
            where TStorage : class, IWorldChannelStorage<T>
        {
            storage = null;
            if (!_registry.IsHandleValid(handle)) return false;
            storage = _storages[handle.Index] as TStorage;
            return storage != null;
        }

        public bool Unbind<T>(ChannelHandle<T> handle)
        {
            if (!_registry.IsHandleValid(handle)) return false;
            if (_storages[handle.Index] == null) return false;
            _storages[handle.Index] = null;
            BoundCount--;
            return true;
        }

        public bool IsBound<T>(ChannelHandle<T> handle)
        {
            return _registry.IsHandleValid(handle) && _storages[handle.Index] != null;
        }

        internal bool IsBoundIndex(int index)
        {
            return (uint)index < (uint)_storages.Length && _storages[index] != null;
        }

        public void ClearBindings()
        {
            Array.Clear(_storages, 0, _storages.Length);
            BoundCount = 0;
        }
    }
}
