using System;
using System.Collections.Generic;
using System.Threading;

namespace StellarFramework.WorldGenKit
{
    public enum WorldChannelRegistrationError
    {
        None = 0,
        InvalidId,
        InvalidStorage,
        InvalidSourceMode,
        DuplicateId,
        RegistryFrozen
    }

    public enum WorldChannelResolveError
    {
        None = 0,
        InvalidId,
        NotFound,
        TypeMismatch
    }

    public readonly struct WorldChannelDescriptor
    {
        public WorldDataChannelId Id { get; }
        public int Index { get; }
        public WorldChannelStorageDescriptor Storage { get; }
        public WorldChannelSourceMode SourceMode { get; }

        internal WorldChannelDescriptor(
            WorldDataChannelId id,
            int index,
            WorldChannelStorageDescriptor storage,
            WorldChannelSourceMode sourceMode)
        {
            Id = id;
            Index = index;
            Storage = storage;
            SourceMode = sourceMode;
        }
    }

    public sealed class WorldChannelRegistryBuilder
    {
        private readonly int _generation;
        private readonly List<Entry> _entries;
        private readonly Dictionary<WorldDataChannelId, int> _indexById;
        private bool _built;

        public int Count => _entries.Count;
        public int Generation => _generation;
        public bool IsBuilt => _built;

        public WorldChannelRegistryBuilder(int initialCapacity = 0)
        {
            if (initialCapacity < 0) throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            _generation = WorldGenRegistryGenerationSource.Next();
            _entries = new List<Entry>(initialCapacity);
            _indexById = new Dictionary<WorldDataChannelId, int>(initialCapacity);
        }

        public ChannelHandle<T> Register<T>(
            WorldDataChannelId id,
            WorldChannelStorageDescriptor storage,
            WorldChannelSourceMode sourceMode = WorldChannelSourceMode.ProducedByStage)
        {
            if (!TryRegister(id, storage, sourceMode, out ChannelHandle<T> handle, out WorldChannelRegistrationError error))
            {
                if (error == WorldChannelRegistrationError.InvalidId ||
                    error == WorldChannelRegistrationError.InvalidStorage ||
                    error == WorldChannelRegistrationError.InvalidSourceMode)
                    throw new ArgumentException("Invalid channel registration: " + error + ".");
                throw new InvalidOperationException("Channel registration failed: " + error + ".");
            }
            return handle;
        }

        public bool TryRegister<T>(
            WorldDataChannelId id,
            WorldChannelStorageDescriptor storage,
            WorldChannelSourceMode sourceMode,
            out ChannelHandle<T> handle,
            out WorldChannelRegistrationError error)
        {
            handle = default(ChannelHandle<T>);
            if (_built)
            {
                error = WorldChannelRegistrationError.RegistryFrozen;
                return false;
            }
            if (!id.IsValid)
            {
                error = WorldChannelRegistrationError.InvalidId;
                return false;
            }
            if (!storage.IsValid)
            {
                error = WorldChannelRegistrationError.InvalidStorage;
                return false;
            }
            if (sourceMode < WorldChannelSourceMode.ProducedByStage || sourceMode > WorldChannelSourceMode.ProvidedInput)
            {
                error = WorldChannelRegistrationError.InvalidSourceMode;
                return false;
            }
            if (_indexById.ContainsKey(id))
            {
                error = WorldChannelRegistrationError.DuplicateId;
                return false;
            }

            int index = _entries.Count;
            _entries.Add(new Entry(id, storage, sourceMode, WorldGenRuntimeTypeToken<T>.Value));
            _indexById.Add(id, index);
            handle = new ChannelHandle<T>(index, _generation);
            error = WorldChannelRegistrationError.None;
            return true;
        }

        public WorldChannelRegistry Build()
        {
            if (_built) throw new InvalidOperationException("World channel registry builder has already been built.");
            _built = true;
            return new WorldChannelRegistry(_generation, _entries.ToArray(), _indexById);
        }

        internal readonly struct Entry
        {
            internal WorldDataChannelId Id { get; }
            internal WorldChannelStorageDescriptor Storage { get; }
            internal WorldChannelSourceMode SourceMode { get; }
            internal int TypeToken { get; }

            internal Entry(
                WorldDataChannelId id,
                WorldChannelStorageDescriptor storage,
                WorldChannelSourceMode sourceMode,
                int typeToken)
            {
                Id = id;
                Storage = storage;
                SourceMode = sourceMode;
                TypeToken = typeToken;
            }
        }
    }

    public sealed class WorldChannelRegistry
    {
        private readonly WorldChannelRegistryBuilder.Entry[] _entries;
        private readonly Dictionary<WorldDataChannelId, int> _indexById;

        public int Count => _entries.Length;
        public int Generation { get; }

        internal WorldChannelRegistry(
            int generation,
            WorldChannelRegistryBuilder.Entry[] entries,
            Dictionary<WorldDataChannelId, int> indexById)
        {
            Generation = generation;
            _entries = entries;
            _indexById = new Dictionary<WorldDataChannelId, int>(indexById);
        }

        public bool IsHandleValid<T>(ChannelHandle<T> handle) =>
            handle.RegistryGeneration == Generation &&
            handle.Index >= 0 &&
            handle.Index < _entries.Length &&
            _entries[handle.Index].TypeToken == WorldGenRuntimeTypeToken<T>.Value;

        public bool TryResolve<T>(
            WorldDataChannelId id,
            out ChannelHandle<T> handle,
            out WorldChannelResolveError error)
        {
            handle = default(ChannelHandle<T>);
            if (!id.IsValid)
            {
                error = WorldChannelResolveError.InvalidId;
                return false;
            }
            if (!_indexById.TryGetValue(id, out int index))
            {
                error = WorldChannelResolveError.NotFound;
                return false;
            }
            if (_entries[index].TypeToken != WorldGenRuntimeTypeToken<T>.Value)
            {
                error = WorldChannelResolveError.TypeMismatch;
                return false;
            }

            handle = new ChannelHandle<T>(index, Generation);
            error = WorldChannelResolveError.None;
            return true;
        }

        public bool TryGetDescriptor<T>(ChannelHandle<T> handle, out WorldChannelDescriptor descriptor)
        {
            if (!IsHandleValid(handle))
            {
                descriptor = default(WorldChannelDescriptor);
                return false;
            }
            WorldChannelRegistryBuilder.Entry entry = _entries[handle.Index];
            descriptor = new WorldChannelDescriptor(entry.Id, handle.Index, entry.Storage, entry.SourceMode);
            return true;
        }

        public bool TryGetDescriptor(int index, out WorldChannelDescriptor descriptor)
        {
            if ((uint)index >= (uint)_entries.Length)
            {
                descriptor = default(WorldChannelDescriptor);
                return false;
            }
            WorldChannelRegistryBuilder.Entry entry = _entries[index];
            descriptor = new WorldChannelDescriptor(entry.Id, index, entry.Storage, entry.SourceMode);
            return true;
        }

        internal int GetTypeToken(int index) => _entries[index].TypeToken;
    }

    internal static class WorldGenRegistryGenerationSource
    {
        private static int _next;
        internal static int Next()
        {
            int generation = Interlocked.Increment(ref _next);
            if (generation <= 0) throw new InvalidOperationException("WorldGen registry generation space exhausted.");
            return generation;
        }
    }

    internal static class WorldGenRuntimeTypeTokenSource
    {
        private static int _next;
        internal static int Next()
        {
            int token = Interlocked.Increment(ref _next);
            if (token <= 0) throw new InvalidOperationException("WorldGen runtime type-token space exhausted.");
            return token;
        }
    }

    internal static class WorldGenRuntimeTypeToken<T>
    {
        internal static readonly int Value = WorldGenRuntimeTypeTokenSource.Next();
    }
}
