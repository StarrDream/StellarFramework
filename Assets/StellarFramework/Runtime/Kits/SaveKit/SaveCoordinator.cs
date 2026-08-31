using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace StellarFramework
{
    internal sealed class SaveCoordinator
    {
        private readonly SaveKitOptions _options;
        private readonly ISaveStorage _storage;
        private readonly SaveSectionRegistry _sections;
        private readonly SaveMigrationRegistry _migrations;
        private readonly Dictionary<string, ISaveSerializer> _serializers;
        private readonly IReadOnlyList<ISaveLifecycleHooks> _lifecycleHooks;
        private readonly Dictionary<string, List<SaveSectionEntry>> _preservedUnknown =
            new Dictionary<string, List<SaveSectionEntry>>(StringComparer.Ordinal);
        private readonly object _gate = new object();
        private SaveOperationDiagnostics _lastDiagnostics;
        private bool _busy;

        public SaveCoordinator(SaveKitOptions options, ISaveStorage storage, SaveSectionRegistry sections,
            SaveMigrationRegistry migrations, IDictionary<string, ISaveSerializer> serializers,
            IReadOnlyList<ISaveLifecycleHooks> lifecycleHooks)
        {
            _options = options;
            _storage = storage;
            _sections = sections;
            _migrations = migrations;
            _serializers = new Dictionary<string, ISaveSerializer>(serializers, StringComparer.Ordinal);
            _lifecycleHooks = lifecycleHooks == null ? Array.Empty<ISaveLifecycleHooks>() : lifecycleHooks.ToArray();
        }

        public SaveOperationDiagnostics LastDiagnostics => _lastDiagnostics == null ? null : _lastDiagnostics.Clone();

        public bool TryRegisterSerializer(ISaveSerializer serializer, out string error)
        {
            error = null;
            if (serializer == null || string.IsNullOrWhiteSpace(serializer.Id) || serializer.Id.Length > 128)
            {
                error = "Serializer 或 Serializer ID 非法。";
                return false;
            }

            if (_serializers.ContainsKey(serializer.Id))
            {
                error = $"Serializer {serializer.Id} 已注册。";
                return false;
            }

            _serializers.Add(serializer.Id, serializer);
            return true;
        }

        public bool TryGetSerializer(string id, out ISaveSerializer serializer) => _serializers.TryGetValue(id ?? string.Empty, out serializer);

        public bool TryEnter()
        {
            lock (_gate)
            {
                if (_busy) return false;
                _busy = true;
                return true;
            }
        }

        public void Exit()
        {
            lock (_gate) _busy = false;
        }

        public async UniTask<SaveResult> SaveAsync(SaveSlotId slotId, CancellationToken cancellationToken)
        {
            var diagnostics = BeginDiagnostics("Save", slotId);
            Stopwatch total = Stopwatch.StartNew();
            if (!slotId.IsValid) return Fail(slotId, SaveErrorCode.InvalidSlotId, "Slot ID 非法。", diagnostics, total);
            if (!TryEnter()) return Fail(slotId, SaveErrorCode.Busy, "SaveKit 正在执行其他操作。", diagnostics, total);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                SaveMetadata previous = await TryReadMetadataAsync(slotId, cancellationToken, true);
                if (previous == null && await _storage.ExistsAsync(slotId, SaveStorageFileKind.Current, cancellationToken))
                {
                    return Fail(slotId, SaveErrorCode.ContainerCorrupted, "现有 current 存档无法验证，已阻止覆盖。", diagnostics, total);
                }

                DateTime now = DateTime.UtcNow;
                var metadata = new SaveMetadata
                {
                    SlotId = slotId,
                    Revision = previous == null ? 1L : checked(previous.Revision + 1L),
                    CreatedUtc = previous == null ? now : previous.CreatedUtc,
                    UpdatedUtc = now,
                    ApplicationVersion = _options.ApplicationVersion ?? string.Empty,
                    ContainerVersion = SaveContainerFormat.ContainerVersion
                };
                if (previous != null)
                {
                    foreach (KeyValuePair<string, string> pair in previous.CustomMetadata)
                    {
                        metadata.CustomMetadata[pair.Key] = pair.Value;
                    }
                }

                List<SaveSectionEntry> entries = new List<SaveSectionEntry>();
                PreservedUnknownResult preserved = await TryGetPreservedUnknownAsync(slotId, cancellationToken);
                if (!preserved.IsSuccess)
                {
                    return Fail(slotId, preserved.ErrorCode, preserved.ErrorMessage, diagnostics, total);
                }
                List<SaveSectionEntry> unknownEntries = preserved.Entries;

                Stopwatch captureWatch = Stopwatch.StartNew();
                SaveCaptureContext captureContext = new SaveCaptureContext(slotId, now, _options);
                try
                {
                    foreach (ISaveLifecycleHooks hooks in _lifecycleHooks) hooks.BeforeCapture(captureContext);
                    foreach (ISaveSection section in _sections.Sections.OrderBy(section => section.Id.Value, StringComparer.Ordinal))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        Stopwatch sectionCapture = Stopwatch.StartNew();
                        object data = section.CaptureUntyped(captureContext);
                        diagnostics.SectionCount++;
                        sectionCapture.Stop();
                        SaveSectionDiagnostics sectionDiagnostics = diagnostics.GetOrCreateSection(section.Id);
                        sectionDiagnostics.CaptureDurationMs = sectionCapture.Elapsed.TotalMilliseconds;

                        if (_options.ValidateAfterCapture)
                        {
                            Stopwatch validationWatch = Stopwatch.StartNew();
                            SaveValidationResult validation = section.ValidateUntyped(data,
                                new SaveValidationContext(slotId, metadata, _options));
                            validationWatch.Stop();
                            sectionDiagnostics.ValidationDurationMs += validationWatch.Elapsed.TotalMilliseconds;
                            if (validation == null || !validation.IsValid)
                            {
                                return Fail(slotId, SaveErrorCode.ValidationFailed,
                                    $"Section {section.Id} Capture 验证失败: {validation}", diagnostics, total);
                            }
                        }

                        if (!TryGetSerializer(section.SerializerId, out ISaveSerializer serializer))
                        {
                            return Fail(slotId, SaveErrorCode.SerializerMissing, $"缺少 Serializer {section.SerializerId}。", diagnostics, total);
                        }

                        Stopwatch serializeWatch = Stopwatch.StartNew();
                        byte[] payload;
                        using (var memory = new MemoryStream())
                        {
                            await serializer.SerializeAsync(section.DataType, data, memory, cancellationToken);
                            payload = memory.ToArray();
                        }
                        serializeWatch.Stop();
                        diagnostics.SerializeDurationMs += serializeWatch.Elapsed.TotalMilliseconds;
                        sectionDiagnostics.SerializeDurationMs = serializeWatch.Elapsed.TotalMilliseconds;
                        sectionDiagnostics.PayloadBytes = payload.LongLength;
                        diagnostics.CaptureDurationMs += sectionCapture.Elapsed.TotalMilliseconds;
                        diagnostics.RawBytes += payload.LongLength;

                        Stopwatch checksumWatch = Stopwatch.StartNew();
                        ulong checksum = new XxHash64Checksum().Compute(payload, 0, payload.Length);
                        checksumWatch.Stop();
                        diagnostics.ChecksumDurationMs += checksumWatch.Elapsed.TotalMilliseconds;
                        entries.Add(new SaveSectionEntry
                        {
                            Descriptor = new SaveSectionDescriptor
                            {
                                Id = section.Id,
                                SchemaVersion = section.SchemaVersion,
                                SerializerId = section.SerializerId,
                                PayloadLength = payload.LongLength,
                                Checksum = checksum,
                                Flags = 0
                            },
                            Payload = payload
                        });
                    }
                }
                finally
                {
                    foreach (ISaveLifecycleHooks hooks in _lifecycleHooks) hooks.AfterCapture(captureContext);
                }

                if (unknownEntries != null)
                {
                    // A section may have been registered after an older slot was loaded. Never
                    // copy its old raw payload back into the new container, otherwise the
                    // container directory would contain duplicate IDs.
                    entries.AddRange(unknownEntries.Where(entry => !_sections.TryGet(entry.Descriptor.Id, out ISaveSection ignored))
                        .Select(entry => new SaveSectionEntry
                    {
                        Descriptor = entry.Descriptor,
                        Payload = entry.Payload,
                        IsUnknown = true
                    }));
                }

                captureWatch.Stop();
                diagnostics.CaptureDurationMs = captureWatch.Elapsed.TotalMilliseconds;
                diagnostics.SectionCount = entries.Count;

                var container = new MemoryStream();
                SaveContainerWriter.Write(container, metadata, entries, _options);
                diagnostics.FinalBytes = container.Length;
                container.Position = 0;
                if (!SaveContainerReader.TryRead(container, _options, out SaveSnapshot verifiedSnapshot,
                    out SaveErrorCode verifyCode, out string verifyMessage))
                {
                    return Fail(slotId, verifyCode, $"临时容器验证失败: {verifyMessage}", diagnostics, total);
                }

                Stopwatch ioWatch = Stopwatch.StartNew();
                await _storage.WriteTemporaryAsync(slotId, container, cancellationToken);
                using (Stream temporary = await _storage.OpenReadAsync(slotId, SaveStorageFileKind.Temporary, cancellationToken))
                {
                    if (!SaveContainerReader.TryRead(temporary, _options, out SaveSnapshot ignored,
                        out SaveErrorCode tempCode, out string tempMessage))
                    {
                        return Fail(slotId, tempCode, $"临时文件验证失败: {tempMessage}", diagnostics, total);
                    }
                }
                ioWatch.Stop();
                diagnostics.IoDurationMs = ioWatch.Elapsed.TotalMilliseconds;

                cancellationToken.ThrowIfCancellationRequested();
                Stopwatch commitWatch = Stopwatch.StartNew();
                await _storage.CommitAsync(slotId, CancellationToken.None);
                commitWatch.Stop();
                diagnostics.CommitDurationMs = commitWatch.Elapsed.TotalMilliseconds;
                diagnostics.Revision = metadata.Revision;
                total.Stop();
                diagnostics.TotalDurationMs = total.Elapsed.TotalMilliseconds;
                diagnostics.Operation = "Save";
                return Complete(SaveResult.Success(slotId, metadata.Revision, diagnostics));
            }
            catch (OperationCanceledException)
            {
                return Fail(slotId, SaveErrorCode.Cancelled, "Save 已取消。", diagnostics, total);
            }
            catch (IOException exception)
            {
                return Fail(slotId, SaveErrorCode.StorageError, exception.Message, diagnostics, total);
            }
            catch (Exception exception)
            {
                return Fail(slotId, SaveErrorCode.UnknownError, exception.Message, diagnostics, total);
            }
            finally
            {
                Exit();
            }
        }

        public async UniTask<SaveResult> LoadAsync(SaveSlotId slotId, CancellationToken cancellationToken)
        {
            var diagnostics = BeginDiagnostics("Load", slotId);
            Stopwatch total = Stopwatch.StartNew();
            if (!slotId.IsValid) return Fail(slotId, SaveErrorCode.InvalidSlotId, "Slot ID 非法。", diagnostics, total);
            if (!TryEnter()) return Fail(slotId, SaveErrorCode.Busy, "SaveKit 正在执行其他操作。", diagnostics, total);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                bool usedBackup = false;
                SaveSnapshotReadResult currentRead = await TryReadSnapshotAsync(slotId, SaveStorageFileKind.Current, cancellationToken);
                SaveSnapshot snapshot = currentRead.Snapshot;
                if (snapshot == null)
                {
                    SaveSnapshotReadResult backupRead = await TryReadSnapshotAsync(slotId, SaveStorageFileKind.Backup, cancellationToken);
                    SaveSnapshot backup = backupRead.Snapshot;
                    if (backup == null)
                    {
                        SaveErrorCode code = currentRead.ErrorCode == SaveErrorCode.None ? SaveErrorCode.SlotNotFound : currentRead.ErrorCode;
                        string message = currentRead.ErrorMessage ?? backupRead.ErrorMessage ?? "存档不存在。";
                        return Fail(slotId, code, message, diagnostics, total);
                    }

                    snapshot = backup;
                    usedBackup = true;
                    diagnostics.BackupUsed = true;
                }

                var prepared = new Dictionary<string, object>(StringComparer.Ordinal);
                var unknown = new List<SaveSectionEntry>();
                foreach (SaveSectionEntry entry in snapshot.Sections)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!_sections.TryGet(entry.Descriptor.Id, out ISaveSection section))
                    {
                        if (_options.UnknownSectionPolicy == UnknownSectionPolicy.Fail)
                        {
                            return Fail(slotId, SaveErrorCode.SectionCorrupted, $"未知 Section {entry.Descriptor.Id}。", diagnostics, total);
                        }

                        if (_options.UnknownSectionPolicy == UnknownSectionPolicy.Preserve)
                        {
                            unknown.Add(entry);
                        }
                        continue;
                    }

                    if (!TryGetSerializer(entry.Descriptor.SerializerId, out ISaveSerializer serializer))
                    {
                        return Fail(slotId, SaveErrorCode.SerializerMissing, $"缺少 Serializer {entry.Descriptor.SerializerId}。", diagnostics, total);
                    }

                    object data;
                    try
                    {
                        using (var payload = new MemoryStream(entry.Payload, false))
                        {
                            data = await serializer.DeserializeAsync(section.DataType, payload, cancellationToken);
                        }
                    }
                    catch (Exception exception)
                    {
                        return Fail(slotId, SaveErrorCode.SectionCorrupted, $"Section {section.Id} Deserialize 失败: {exception.Message}", diagnostics, total);
                    }

                    if (entry.Descriptor.SchemaVersion > section.SchemaVersion)
                    {
                        return Fail(slotId, SaveErrorCode.UnsupportedSectionVersion,
                            $"Section {section.Id} 版本 {entry.Descriptor.SchemaVersion} 高于当前版本 {section.SchemaVersion}。", diagnostics, total);
                    }

                    if (entry.Descriptor.SchemaVersion < section.SchemaVersion)
                    {
                        SaveSectionDiagnostics sectionDiagnostics = diagnostics.GetOrCreateSection(section.Id);
                        if (!_migrations.TryBuildChain(section.Id, entry.Descriptor.SchemaVersion, section.SchemaVersion,
                            out IReadOnlyList<ISaveMigration> chain, out string migrationError))
                        {
                            return Fail(slotId, SaveErrorCode.MigrationMissing, migrationError, diagnostics, total);
                        }

                        foreach (ISaveMigration migration in chain)
                        {
                            try
                            {
                                data = migration.Migrate(data, new SaveMigrationContext(slotId, section.Id,
                                    migration.FromVersion, migration.ToVersion));
                                diagnostics.MigrationCount++;
                                sectionDiagnostics.MigrationSteps++;
                            }
                            catch (Exception exception)
                            {
                                return Fail(slotId, SaveErrorCode.MigrationFailed,
                                    $"Section {section.Id} Migration 失败: {exception.Message}", diagnostics, total);
                            }
                        }
                    }

                    SaveSectionDiagnostics validationDiagnostics = diagnostics.GetOrCreateSection(section.Id);
                    Stopwatch loadValidationWatch = Stopwatch.StartNew();
                    SaveValidationResult validation = section.ValidateUntyped(data,
                        new SaveValidationContext(slotId, snapshot.Metadata, _options));
                    loadValidationWatch.Stop();
                    validationDiagnostics.ValidationDurationMs += loadValidationWatch.Elapsed.TotalMilliseconds;
                    if (validation == null || !validation.IsValid)
                    {
                        return Fail(slotId, SaveErrorCode.ValidationFailed, $"Section {section.Id} 验证失败: {validation}", diagnostics, total);
                    }

                    prepared.Add(section.Id.Value, data);
                }

                foreach (ISaveSection section in _sections.Sections)
                {
                    if (prepared.ContainsKey(section.Id.Value)) continue;
                    MissingSectionPolicy policy = section.MissingPolicy;
                    if (policy == MissingSectionPolicy.Fail)
                    {
                        policy = _options.MissingSectionPolicy;
                    }

                    if (policy == MissingSectionPolicy.Fail)
                    {
                        return Fail(slotId, SaveErrorCode.SectionMissing, $"缺少 Section {section.Id}。", diagnostics, total);
                    }

                    if (policy == MissingSectionPolicy.UseDefault)
                    {
                        object defaultData = section.CreateDefaultUntyped(new SaveRestoreContext(slotId, snapshot.Metadata, _options));
                        SaveValidationResult validation = section.ValidateUntyped(defaultData,
                            new SaveValidationContext(slotId, snapshot.Metadata, _options));
                        if (validation == null || !validation.IsValid)
                        {
                            return Fail(slotId, SaveErrorCode.ValidationFailed, $"Section {section.Id} 默认数据验证失败: {validation}", diagnostics, total);
                        }

                        prepared.Add(section.Id.Value, defaultData);
                    }
                }

                if (!_sections.TryGetRestoreOrder(out IReadOnlyList<ISaveSection> order, out string orderError))
                {
                    return Fail(slotId, SaveErrorCode.RestoreDependencyCycle, orderError, diagnostics, total);
                }

                var restoreContext = new SaveRestoreContext(slotId, snapshot.Metadata, _options);
                try
                {
                    foreach (ISaveLifecycleHooks hooks in _lifecycleHooks) hooks.BeforeRestore(restoreContext);
                    // Cancellation is honored up to the Apply boundary. Once the first
                    // Restore starts, finish the deterministic DAG to avoid half-restored models.
                    cancellationToken.ThrowIfCancellationRequested();
                    foreach (ISaveSection section in order)
                    {
                        if (!prepared.TryGetValue(section.Id.Value, out object data)) continue;
                        try
                        {
                            section.RestoreUntyped(data, restoreContext);
                        }
                        catch (Exception exception)
                        {
                            return Fail(slotId, SaveErrorCode.RestoreFailed, $"Section {section.Id} Restore 失败: {exception.Message}", diagnostics, total);
                        }
                    }
                }
                finally
                {
                    foreach (ISaveLifecycleHooks hooks in _lifecycleHooks) hooks.AfterRestore(restoreContext);
                }

                if (_options.UnknownSectionPolicy == UnknownSectionPolicy.Preserve)
                {
                    _preservedUnknown[slotId.Value] = unknown;
                }

                if (usedBackup && _options.AutoRecoverBackup)
                {
                    await _storage.RestoreBackupAsync(slotId, CancellationToken.None);
                }

                diagnostics.Revision = snapshot.Metadata.Revision;
                total.Stop();
                diagnostics.TotalDurationMs = total.Elapsed.TotalMilliseconds;
                return Complete(SaveResult.Success(slotId, snapshot.Metadata.Revision, diagnostics, usedBackup));
            }
            catch (OperationCanceledException)
            {
                return Fail(slotId, SaveErrorCode.Cancelled, "Load 已取消。", diagnostics, total);
            }
            catch (IOException exception)
            {
                return Fail(slotId, SaveErrorCode.StorageError, exception.Message, diagnostics, total);
            }
            catch (Exception exception)
            {
                return Fail(slotId, SaveErrorCode.UnknownError, exception.Message, diagnostics, total);
            }
            finally
            {
                Exit();
            }
        }

        public async UniTask<IReadOnlyList<SaveSlotInfo>> GetSlotsAsync(CancellationToken cancellationToken)
        {
            return await _storage.ListSlotsAsync(_options, cancellationToken);
        }

        public async UniTask<SaveResult> DeleteAsync(SaveSlotId slotId, CancellationToken cancellationToken)
        {
            var diagnostics = BeginDiagnostics("Delete", slotId);
            Stopwatch total = Stopwatch.StartNew();
            if (!slotId.IsValid) return Fail(slotId, SaveErrorCode.InvalidSlotId, "Slot ID 非法。", diagnostics, total);
            if (!TryEnter()) return Fail(slotId, SaveErrorCode.Busy, "SaveKit 正在执行其他操作。", diagnostics, total);
            try
            {
                await _storage.DeleteAsync(slotId, cancellationToken);
                _preservedUnknown.Remove(slotId.Value);
                total.Stop();
                diagnostics.TotalDurationMs = total.Elapsed.TotalMilliseconds;
                return Complete(SaveResult.Success(slotId, 0, diagnostics));
            }
            catch (OperationCanceledException)
            {
                return Fail(slotId, SaveErrorCode.Cancelled, "Delete 已取消。", diagnostics, total);
            }
            catch (Exception exception)
            {
                return Fail(slotId, SaveErrorCode.StorageError, exception.Message, diagnostics, total);
            }
            finally { Exit(); }
        }

        private async UniTask<SaveMetadata> TryReadMetadataAsync(SaveSlotId slotId, CancellationToken cancellationToken, bool current)
        {
            SaveStorageFileKind kind = current ? SaveStorageFileKind.Current : SaveStorageFileKind.Backup;
            if (!await _storage.ExistsAsync(slotId, kind, cancellationToken)) return null;
            try
            {
                using (Stream stream = await _storage.OpenReadAsync(slotId, kind, cancellationToken))
                {
                    // A save must never rotate an unchecked current file into backup.
                    // Read the payloads here so checksum corruption blocks overwrite.
                    return SaveContainerReader.TryRead(stream, _options, out SaveSnapshot snapshot,
                        out SaveErrorCode ignoredCode, out string ignoredMessage) ? snapshot.Metadata : null;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { return null; }
        }

        private async UniTask<SaveSnapshotReadResult> TryReadSnapshotAsync(SaveSlotId slotId, SaveStorageFileKind kind,
            CancellationToken cancellationToken)
        {
            var result = new SaveSnapshotReadResult
            {
                ErrorCode = SaveErrorCode.SlotNotFound,
                ErrorMessage = "存档不存在。"
            };
            if (!await _storage.ExistsAsync(slotId, kind, cancellationToken)) return result;
            try
            {
                using (Stream stream = await _storage.OpenReadAsync(slotId, kind, cancellationToken))
                {
                    if (SaveContainerReader.TryRead(stream, _options, out SaveSnapshot snapshot, out SaveErrorCode errorCode, out string errorMessage))
                    {
                        result.Snapshot = snapshot;
                        result.ErrorCode = SaveErrorCode.None;
                        result.ErrorMessage = null;
                        return result;
                    }

                    result.ErrorCode = errorCode;
                    result.ErrorMessage = errorMessage;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception)
            {
                result.ErrorCode = SaveErrorCode.StorageError;
                result.ErrorMessage = exception.Message;
            }
            return result;
        }

        private async UniTask<PreservedUnknownResult> TryGetPreservedUnknownAsync(SaveSlotId slotId,
            CancellationToken cancellationToken)
        {
            if (_options.UnknownSectionPolicy != UnknownSectionPolicy.Preserve)
            {
                return PreservedUnknownResult.Success(new List<SaveSectionEntry>());
            }

            if (_preservedUnknown.TryGetValue(slotId.Value, out List<SaveSectionEntry> cached))
            {
                return PreservedUnknownResult.Success(cached);
            }

            try
            {
                // The first save has no current file yet. Avoid opening a path whose
                // parent directory has not been created by the storage implementation.
                if (!await _storage.ExistsAsync(slotId, SaveStorageFileKind.Current, cancellationToken))
                {
                    return PreservedUnknownResult.Success(new List<SaveSectionEntry>());
                }

                using (Stream stream = await _storage.OpenReadAsync(slotId, SaveStorageFileKind.Current, cancellationToken))
                {
                    if (!SaveContainerReader.TryRead(stream, _options, out SaveSnapshot snapshot, out SaveErrorCode code, out string message))
                    {
                        return PreservedUnknownResult.Failure(code, message);
                    }

                    List<SaveSectionEntry> entries = snapshot.Sections
                        .Where(entry => !_sections.TryGet(entry.Descriptor.Id, out ISaveSection ignored)).ToList();
                    _preservedUnknown[slotId.Value] = entries;
                    return PreservedUnknownResult.Success(entries);
                }
            }
            catch (FileNotFoundException) { return PreservedUnknownResult.Success(new List<SaveSectionEntry>()); }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception)
            {
                return PreservedUnknownResult.Failure(SaveErrorCode.StorageError, exception.Message);
            }
        }

        private SaveOperationDiagnostics BeginDiagnostics(string operation, SaveSlotId slotId)
        {
            var diagnostics = new SaveOperationDiagnostics { Operation = operation, SlotId = slotId };
            _lastDiagnostics = diagnostics;
            return diagnostics;
        }

        private SaveResult Complete(SaveResult result)
        {
            if (result.Diagnostics != null)
            {
                result.Diagnostics.Result = result.Status;
                _lastDiagnostics = result.Diagnostics.Clone();
            }
            return result;
        }

        private SaveResult Fail(SaveSlotId slotId, SaveErrorCode code, string message,
            SaveOperationDiagnostics diagnostics, Stopwatch total)
        {
            total.Stop();
            diagnostics.TotalDurationMs = total.Elapsed.TotalMilliseconds;
            diagnostics.LastError = code;
            diagnostics.LastErrorMessage = message;
            diagnostics.Result = SaveOperationStatus.Failed;
            _lastDiagnostics = diagnostics.Clone();
            return SaveResult.Failure(slotId, code, message, diagnostics);
        }

        private sealed class SaveSnapshotReadResult
        {
            public SaveSnapshot Snapshot;
            public SaveErrorCode ErrorCode;
            public string ErrorMessage;
        }

        private sealed class PreservedUnknownResult
        {
            public bool IsSuccess;
            public List<SaveSectionEntry> Entries;
            public SaveErrorCode ErrorCode;
            public string ErrorMessage;

            public static PreservedUnknownResult Success(List<SaveSectionEntry> entries)
            {
                return new PreservedUnknownResult { IsSuccess = true, Entries = entries };
            }

            public static PreservedUnknownResult Failure(SaveErrorCode code, string message)
            {
                return new PreservedUnknownResult { ErrorCode = code, ErrorMessage = message };
            }
        }
    }
}
