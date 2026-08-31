using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace StellarFramework
{
    public sealed class SaveKitBuilder
    {
        internal readonly SaveKitOptions Options = new SaveKitOptions();
        internal ISaveStorage Storage;
        internal string DefaultSerializerId = "unity-json";
        internal readonly List<ISaveSerializer> Serializers = new List<ISaveSerializer>();
        internal readonly List<ISaveLifecycleHooks> LifecycleHooks = new List<ISaveLifecycleHooks>();

        public SaveKitBuilder UseStorage(ISaveStorage storage)
        {
            Storage = storage ?? throw new ArgumentNullException(nameof(storage));
            return this;
        }

        public SaveKitBuilder UseSerializer(ISaveSerializer serializer)
        {
            if (serializer == null) throw new ArgumentNullException(nameof(serializer));
            Serializers.Add(serializer);
            return this;
        }

        public SaveKitBuilder UseLifecycleHooks(ISaveLifecycleHooks hooks)
        {
            if (hooks == null) throw new ArgumentNullException(nameof(hooks));
            LifecycleHooks.Add(hooks);
            return this;
        }

        public SaveKitBuilder SetDefaultSerializer(string serializerId)
        {
            if (string.IsNullOrWhiteSpace(serializerId)) throw new ArgumentException("Serializer ID 不能为空。", nameof(serializerId));
            DefaultSerializerId = serializerId.Trim();
            return this;
        }

        public SaveKitBuilder SetApplicationVersion(string applicationVersion)
        {
            Options.ApplicationVersion = applicationVersion ?? string.Empty;
            return this;
        }

        public SaveKitBuilder Configure(Action<SaveKitOptions> configure)
        {
            configure?.Invoke(Options);
            return this;
        }
    }

    public static class SaveKit
    {
        private const string BuiltInDefaultSerializerId = "unity-json";
        private static SaveCoordinator _coordinator;
        private static SaveKitOptions _options;
        private static SaveSectionRegistry _sections;
        private static SaveMigrationRegistry _migrations;
        private static string _defaultSerializerId;
        private static ISaveStorage _storage;

        public static bool IsInitialized => _coordinator != null;
        public static string DefaultSerializerId => _defaultSerializerId ?? BuiltInDefaultSerializerId;
        public static ISaveStorage Storage => _storage;
        public static SaveKitOptions Options => _options == null ? null : _options.Clone();

        public static void Initialize(Action<SaveKitBuilder> configure = null)
        {
            var builder = new SaveKitBuilder()
                .UseSerializer(new UnityJsonSaveSerializer())
                .UseSerializer(new RawBytesSaveSerializer());
            configure?.Invoke(builder);
            if (!builder.Options.Validate(out string optionsError))
            {
                throw new ArgumentException(optionsError, nameof(configure));
            }

            if (builder.Storage == null) builder.Storage = new FileSystemSaveStorage();
            var serializers = new Dictionary<string, ISaveSerializer>(StringComparer.Ordinal);
            foreach (ISaveSerializer serializer in builder.Serializers)
            {
                if (serializers.ContainsKey(serializer.Id)) continue;
                serializers.Add(serializer.Id, serializer);
            }

            if (!serializers.ContainsKey(builder.DefaultSerializerId))
            {
                throw new InvalidOperationException($"默认 Serializer {builder.DefaultSerializerId} 未注册。" );
            }

            _options = builder.Options.Clone();
            _defaultSerializerId = builder.DefaultSerializerId;
            _storage = builder.Storage;
            _sections = new SaveSectionRegistry();
            _migrations = new SaveMigrationRegistry();
            _coordinator = new SaveCoordinator(_options, builder.Storage, _sections, _migrations, serializers,
                builder.LifecycleHooks);
        }

        public static bool Register(ISaveSection section)
        {
            EnsureInitialized();
            return _sections.TryRegister(section, out string error) || LogKit.ErrorAndReturnFalse(error);
        }

        public static bool Unregister(SaveSectionId id)
        {
            EnsureInitialized();
            return _sections.Unregister(id);
        }

        public static bool TryGetSection(SaveSectionId id, out ISaveSection section)
        {
            EnsureInitialized();
            return _sections.TryGet(id, out section);
        }

        public static bool RegisterMigration(SaveSectionId sectionId, ISaveMigration migration)
        {
            EnsureInitialized();
            return _migrations.TryRegister(sectionId, migration, out string error) || LogKit.ErrorAndReturnFalse(error);
        }

        /// <summary>Returns a validated version/type chain for the currently registered Section.</summary>
        public static bool TryBuildMigrationChain(SaveSectionId sectionId, int fromVersion, int toVersion,
            out IReadOnlyList<ISaveMigration> chain, out string error)
        {
            EnsureInitialized();
            if (!_sections.TryGet(sectionId, out ISaveSection section))
            {
                chain = Array.Empty<ISaveMigration>();
                error = $"Section {sectionId} 未注册。";
                return false;
            }

            return _migrations.TryBuildChain(sectionId, fromVersion, toVersion, section.DataType,
                out chain, out error);
        }

        public static bool RegisterSerializer(ISaveSerializer serializer)
        {
            EnsureInitialized();
            return _coordinator.TryRegisterSerializer(serializer, out string error) || LogKit.ErrorAndReturnFalse(error);
        }

        public static UniTask<SaveResult> SaveAsync(string slotId, CancellationToken cancellationToken = default(CancellationToken))
        {
            EnsureInitialized();
            if (!SaveSlotId.TryCreate(slotId, out SaveSlotId id, out string error))
            {
                return UniTask.FromResult(new SaveResult
                {
                    Status = SaveOperationStatus.Failed,
                    ErrorCode = SaveErrorCode.InvalidSlotId,
                    ErrorMessage = error
                });
            }

            return _coordinator.SaveAsync(id, cancellationToken);
        }

        public static UniTask<SaveResult> LoadAsync(string slotId, CancellationToken cancellationToken = default(CancellationToken))
        {
            EnsureInitialized();
            if (!SaveSlotId.TryCreate(slotId, out SaveSlotId id, out string error))
            {
                return UniTask.FromResult(new SaveResult
                {
                    Status = SaveOperationStatus.Failed,
                    ErrorCode = SaveErrorCode.InvalidSlotId,
                    ErrorMessage = error
                });
            }

            return _coordinator.LoadAsync(id, cancellationToken);
        }

        public static UniTask<IReadOnlyList<SaveSlotInfo>> GetSlotsAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            EnsureInitialized();
            return _coordinator.GetSlotsAsync(cancellationToken);
        }

        public static UniTask<SaveResult> DeleteAsync(string slotId, CancellationToken cancellationToken = default(CancellationToken))
        {
            EnsureInitialized();
            if (!SaveSlotId.TryCreate(slotId, out SaveSlotId id, out string error))
            {
                return UniTask.FromResult(new SaveResult
                {
                    Status = SaveOperationStatus.Failed,
                    ErrorCode = SaveErrorCode.InvalidSlotId,
                    ErrorMessage = error
                });
            }

            return _coordinator.DeleteAsync(id, cancellationToken);
        }

        public static SaveOperationDiagnostics GetDiagnostics()
        {
            EnsureInitialized();
            return _coordinator.LastDiagnostics;
        }

        /// <summary>
        /// Executes read/checksum/deserialize/migration/validate only. It never restores,
        /// saves, or modifies the supplied snapshot/source file.
        /// </summary>
        public static UniTask<SaveResult> RunMigrationDryRunAsync(SaveSnapshot snapshot,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            EnsureInitialized();
            return _coordinator.DryRunMigrationAsync(snapshot, cancellationToken);
        }

        internal static void EnsureInitialized()
        {
            if (_coordinator == null) Initialize();
        }

        internal static void ResetForTests()
        {
            _coordinator = null;
            _options = null;
            _sections = null;
            _migrations = null;
            _defaultSerializerId = null;
            _storage = null;
        }
    }
}
