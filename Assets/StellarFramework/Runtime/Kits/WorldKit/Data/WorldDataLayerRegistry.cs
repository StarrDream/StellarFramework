using System;
using System.Collections.Generic;
using System.Threading;

namespace StellarFramework.WorldKit
{
    public enum WorldDataLayerRegistrationError
    {
        None = 0,
        InvalidId,
        InvalidScope,
        DuplicateId,
        RegistryFrozen
    }

    public enum WorldDataLayerResolveError
    {
        None = 0,
        InvalidId,
        NotFound,
        TypeMismatch
    }

    public readonly struct WorldDataLayerDescriptor
    {
        public WorldDataLayerId Id { get; }
        public WorldDataLayerScope Scope { get; }
        public int Index { get; }

        internal WorldDataLayerDescriptor(WorldDataLayerId id, WorldDataLayerScope scope, int index)
        {
            Id = id;
            Scope = scope;
            Index = index;
        }
    }

    public sealed class WorldDataLayerRegistryBuilder
    {
        private readonly int _generation;
        private readonly List<Entry> _entries;
        private readonly Dictionary<WorldDataLayerId, int> _indexById;
        private bool _built;

        public int Count => _entries.Count;
        public int Generation => _generation;
        public bool IsBuilt => _built;

        public WorldDataLayerRegistryBuilder(int initialCapacity = 0)
        {
            if (initialCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));

            _generation = WorldRegistryGenerationSource.Next();
            _entries = new List<Entry>(initialCapacity);
            _indexById = new Dictionary<WorldDataLayerId, int>(initialCapacity);
        }

        public WorldDataLayerHandle<T> Register<T>(WorldDataLayerId id, WorldDataLayerScope scope)
        {
            if (!TryRegister(id, scope, out WorldDataLayerHandle<T> handle, out WorldDataLayerRegistrationError error))
            {
                if (error == WorldDataLayerRegistrationError.InvalidId ||
                    error == WorldDataLayerRegistrationError.InvalidScope)
                    throw new ArgumentException("Invalid world data layer registration: " + error + ".");

                throw new InvalidOperationException("World data layer registration failed: " + error + ".");
            }

            return handle;
        }

        public bool TryRegister<T>(
            WorldDataLayerId id,
            WorldDataLayerScope scope,
            out WorldDataLayerHandle<T> handle,
            out WorldDataLayerRegistrationError error)
        {
            handle = default(WorldDataLayerHandle<T>);
            if (_built)
            {
                error = WorldDataLayerRegistrationError.RegistryFrozen;
                return false;
            }

            if (!id.IsValid)
            {
                error = WorldDataLayerRegistrationError.InvalidId;
                return false;
            }

            if (!IsValidScope(scope))
            {
                error = WorldDataLayerRegistrationError.InvalidScope;
                return false;
            }

            if (_indexById.ContainsKey(id))
            {
                error = WorldDataLayerRegistrationError.DuplicateId;
                return false;
            }

            int index = _entries.Count;
            _entries.Add(new Entry(id, scope, WorldRuntimeTypeToken<T>.Value));
            _indexById.Add(id, index);
            handle = new WorldDataLayerHandle<T>(index, _generation);
            error = WorldDataLayerRegistrationError.None;
            return true;
        }

        public WorldDataLayerRegistry Build()
        {
            if (_built) throw new InvalidOperationException("World data layer registry builder has already been built.");
            _built = true;
            return new WorldDataLayerRegistry(_generation, _entries.ToArray(), _indexById);
        }

        private static bool IsValidScope(WorldDataLayerScope scope) =>
            scope >= WorldDataLayerScope.World && scope <= WorldDataLayerScope.Chunk;

        internal readonly struct Entry
        {
            internal WorldDataLayerId Id { get; }
            internal WorldDataLayerScope Scope { get; }
            internal int TypeToken { get; }

            internal Entry(WorldDataLayerId id, WorldDataLayerScope scope, int typeToken)
            {
                Id = id;
                Scope = scope;
                TypeToken = typeToken;
            }
        }
    }

    public sealed class WorldDataLayerRegistry
    {
        private readonly WorldDataLayerRegistryBuilder.Entry[] _entries;
        private readonly Dictionary<WorldDataLayerId, int> _indexById;

        public int Count => _entries.Length;
        public int Generation { get; }

        internal WorldDataLayerRegistry(
            int generation,
            WorldDataLayerRegistryBuilder.Entry[] entries,
            Dictionary<WorldDataLayerId, int> indexById)
        {
            Generation = generation;
            _entries = entries;
            _indexById = new Dictionary<WorldDataLayerId, int>(indexById);
        }

        public bool IsHandleValid<T>(WorldDataLayerHandle<T> handle)
        {
            return handle.RegistryGeneration == Generation &&
                   handle.Index >= 0 &&
                   handle.Index < _entries.Length &&
                   _entries[handle.Index].TypeToken == WorldRuntimeTypeToken<T>.Value;
        }

        public bool TryResolve<T>(
            WorldDataLayerId id,
            out WorldDataLayerHandle<T> handle,
            out WorldDataLayerResolveError error)
        {
            handle = default(WorldDataLayerHandle<T>);
            if (!id.IsValid)
            {
                error = WorldDataLayerResolveError.InvalidId;
                return false;
            }

            if (!_indexById.TryGetValue(id, out int index))
            {
                error = WorldDataLayerResolveError.NotFound;
                return false;
            }

            if (_entries[index].TypeToken != WorldRuntimeTypeToken<T>.Value)
            {
                error = WorldDataLayerResolveError.TypeMismatch;
                return false;
            }

            handle = new WorldDataLayerHandle<T>(index, Generation);
            error = WorldDataLayerResolveError.None;
            return true;
        }

        public bool TryGetDescriptor<T>(
            WorldDataLayerHandle<T> handle,
            out WorldDataLayerDescriptor descriptor)
        {
            if (!IsHandleValid(handle))
            {
                descriptor = default(WorldDataLayerDescriptor);
                return false;
            }

            WorldDataLayerRegistryBuilder.Entry entry = _entries[handle.Index];
            descriptor = new WorldDataLayerDescriptor(entry.Id, entry.Scope, handle.Index);
            return true;
        }

        public bool TryGetDescriptor(int index, out WorldDataLayerDescriptor descriptor)
        {
            if ((uint)index >= (uint)_entries.Length)
            {
                descriptor = default(WorldDataLayerDescriptor);
                return false;
            }

            WorldDataLayerRegistryBuilder.Entry entry = _entries[index];
            descriptor = new WorldDataLayerDescriptor(entry.Id, entry.Scope, index);
            return true;
        }
    }

    internal static class WorldRegistryGenerationSource
    {
        private static int _next;

        internal static int Next()
        {
            int generation = Interlocked.Increment(ref _next);
            if (generation <= 0)
                throw new InvalidOperationException("World registry generation space exhausted.");
            return generation;
        }
    }

    internal static class WorldRuntimeTypeTokenSource
    {
        private static int _next;

        internal static int Next()
        {
            int token = Interlocked.Increment(ref _next);
            if (token <= 0)
                throw new InvalidOperationException("World runtime type-token space exhausted.");
            return token;
        }
    }

    internal static class WorldRuntimeTypeToken<T>
    {
        internal static readonly int Value = WorldRuntimeTypeTokenSource.Next();
    }
}
