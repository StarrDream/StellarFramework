using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace StellarFramework
{
    /// <summary>
    /// Fluent initialization surface for SaveKit storage, serializers, lifecycle hooks, and runtime limits.
    /// </summary>
    /// <remarks>
    /// The builder is consumed only during <see cref="SaveKit.Initialize"/>. Runtime registrations such as
    /// sections and migrations are performed through <see cref="SaveKit"/> after initialization.
    /// </remarks>
    public sealed class SaveKitBuilder
    {
        internal readonly SaveKitOptions Options = new SaveKitOptions();
        internal ISaveStorage Storage;
        internal string DefaultSerializerId = "unity-json";
        internal readonly List<ISaveSerializer> Serializers = new List<ISaveSerializer>();
        internal readonly List<ISaveLifecycleHooks> LifecycleHooks = new List<ISaveLifecycleHooks>();

        /// <summary>Uses the supplied storage backend instead of the default file-system storage.</summary>
        /// <param name="storage">Storage implementation that owns persistence I/O.</param>
        /// <returns>This builder for fluent configuration.</returns>
        public SaveKitBuilder UseStorage(ISaveStorage storage)
        {
            Storage = storage ?? throw new ArgumentNullException(nameof(storage));
            return this;
        }

        /// <summary>Adds a serializer that sections may reference by serializer id.</summary>
        /// <param name="serializer">Serializer instance to register at initialization.</param>
        /// <returns>This builder for fluent configuration.</returns>
        public SaveKitBuilder UseSerializer(ISaveSerializer serializer)
        {
            if (serializer == null) throw new ArgumentNullException(nameof(serializer));
            Serializers.Add(serializer);
            return this;
        }

        /// <summary>Adds lifecycle hooks that observe capture/restore boundaries.</summary>
        /// <param name="hooks">Lifecycle callback implementation.</param>
        /// <returns>This builder for fluent configuration.</returns>
        public SaveKitBuilder UseLifecycleHooks(ISaveLifecycleHooks hooks)
        {
            if (hooks == null) throw new ArgumentNullException(nameof(hooks));
            LifecycleHooks.Add(hooks);
            return this;
        }

        /// <summary>Sets the serializer id used by sections that rely on the framework default.</summary>
        /// <param name="serializerId">A serializer id that must also be registered on this builder.</param>
        /// <returns>This builder for fluent configuration.</returns>
        public SaveKitBuilder SetDefaultSerializer(string serializerId)
        {
            if (string.IsNullOrWhiteSpace(serializerId)) throw new ArgumentException("Serializer ID 不能为空。", nameof(serializerId));
            DefaultSerializerId = serializerId.Trim();
            return this;
        }

        /// <summary>Sets the application version written into future save metadata.</summary>
        /// <param name="applicationVersion">Application-defined version string; null becomes empty.</param>
        /// <returns>This builder for fluent configuration.</returns>
        public SaveKitBuilder SetApplicationVersion(string applicationVersion)
        {
            Options.ApplicationVersion = applicationVersion ?? string.Empty;
            return this;
        }

        /// <summary>Mutates the initialization options before SaveKit validates and clones them.</summary>
        /// <param name="configure">Optional options callback.</param>
        /// <returns>This builder for fluent configuration.</returns>
        public SaveKitBuilder Configure(Action<SaveKitOptions> configure)
        {
            configure?.Invoke(Options);
            return this;
        }
    }

    /// <summary>
    /// Process-wide facade for registering save sections and executing asynchronous save/load operations.
    /// </summary>
    /// <remarks>
    /// SaveKit serializes operations through its coordinator: concurrent save/load/delete attempts do not run
    /// against the same runtime state simultaneously. Business code owns section data and must re-register
    /// runtime-only delegates/handles after loading; SaveKit persists section payloads, not arbitrary scene state.
    /// Calling an operational API before explicit initialization performs default initialization.
    /// </remarks>
    public static class SaveKit
    {
        private const string BuiltInDefaultSerializerId = "unity-json";
        private static SaveCoordinator _coordinator;
        private static SaveKitOptions _options;
        private static SaveSectionRegistry _sections;
        private static SaveMigrationRegistry _migrations;
        private static string _defaultSerializerId;
        private static ISaveStorage _storage;

        /// <summary>Gets whether SaveKit has created its runtime coordinator.</summary>
        public static bool IsInitialized => _coordinator != null;

        /// <summary>Gets the serializer id used as the framework default.</summary>
        public static string DefaultSerializerId => _defaultSerializerId ?? BuiltInDefaultSerializerId;

        /// <summary>Gets the active storage backend, or null before initialization.</summary>
        public static ISaveStorage Storage => _storage;

        /// <summary>Gets a defensive copy of the active options, or null before initialization.</summary>
        public static SaveKitOptions Options => _options == null ? null : _options.Clone();

        /// <summary>Initializes or reinitializes SaveKit with explicit storage/serializer/options configuration.</summary>
        /// <param name="configure">Optional builder callback. When omitted, built-in serializers and file storage are used.</param>
        /// <remarks>
        /// Reinitialization replaces registries and the coordinator. Register sections/migrations after this call.
        /// </remarks>
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

        /// <summary>Registers one runtime save section.</summary>
        /// <param name="section">Section implementation that owns capture/validate/restore behavior.</param>
        /// <returns>False when the section is invalid or conflicts with an existing registration.</returns>
        public static bool Register(ISaveSection section)
        {
            EnsureInitialized();
            return _sections.TryRegister(section, out string error) || LogKit.ErrorAndReturnFalse(error);
        }

        /// <summary>Removes a previously registered section.</summary>
        public static bool Unregister(SaveSectionId id)
        {
            EnsureInitialized();
            return _sections.Unregister(id);
        }

        /// <summary>Looks up a registered section without capturing or loading data.</summary>
        public static bool TryGetSection(SaveSectionId id, out ISaveSection section)
        {
            EnsureInitialized();
            return _sections.TryGet(id, out section);
        }

        /// <summary>Registers one schema migration step for a section.</summary>
        /// <returns>False when the migration is invalid, duplicated, or conflicts with the existing chain.</returns>
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

        /// <summary>Registers an additional serializer at runtime.</summary>
        /// <returns>False when its id is invalid or already registered.</returns>
        public static bool RegisterSerializer(ISaveSerializer serializer)
        {
            EnsureInitialized();
            return _coordinator.TryRegisterSerializer(serializer, out string error) || LogKit.ErrorAndReturnFalse(error);
        }

        /// <summary>Captures all registered sections and atomically persists a new revision for the slot.</summary>
        /// <param name="slotId">Application-facing slot id using SaveKit's validated identifier grammar.</param>
        /// <param name="cancellationToken">Cancels the caller's operation at supported boundaries.</param>
        /// <returns>A result describing success, backup recovery, cancellation, validation, serialization, or storage failure.</returns>
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

        /// <summary>Reads, validates, migrates, and restores registered sections from a slot.</summary>
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

        /// <summary>Enumerates visible save slots and their current health metadata.</summary>
        public static UniTask<IReadOnlyList<SaveSlotInfo>> GetSlotsAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            EnsureInitialized();
            return _coordinator.GetSlotsAsync(cancellationToken);
        }

        /// <summary>Deletes the current save data for a validated slot id through the configured storage backend.</summary>
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

        /// <summary>Gets a defensive snapshot of diagnostics from the most recent coordinator operation.</summary>
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
