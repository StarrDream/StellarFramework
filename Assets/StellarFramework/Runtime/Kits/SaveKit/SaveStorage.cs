using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace StellarFramework
{
    public interface ISaveStorage
    {
        string Description { get; }
        string GetFilePath(SaveSlotId slotId, SaveStorageFileKind kind);
        UniTask<IReadOnlyList<SaveSlotInfo>> ListSlotsAsync(SaveKitOptions options, CancellationToken cancellationToken);
        UniTask<bool> ExistsAsync(SaveSlotId slotId, SaveStorageFileKind kind, CancellationToken cancellationToken);
        UniTask<Stream> OpenReadAsync(SaveSlotId slotId, SaveStorageFileKind kind, CancellationToken cancellationToken);
        UniTask WriteTemporaryAsync(SaveSlotId slotId, Stream source, CancellationToken cancellationToken);
        UniTask CommitAsync(SaveSlotId slotId, CancellationToken cancellationToken);
        UniTask RestoreBackupAsync(SaveSlotId slotId, CancellationToken cancellationToken);
        UniTask DeleteAsync(SaveSlotId slotId, CancellationToken cancellationToken);
    }

    public sealed class FileSystemSaveStorage : ISaveStorage
    {
        private readonly string _rootPath;

        public FileSystemSaveStorage(string rootPath = null)
        {
            _rootPath = string.IsNullOrEmpty(rootPath)
                ? Path.Combine(Application.persistentDataPath, "Saves")
                : rootPath;
        }

        public string Description => _rootPath;

        public string GetFilePath(SaveSlotId slotId, SaveStorageFileKind kind)
        {
            if (!slotId.IsValid) throw new ArgumentException("Slot ID 非法。", nameof(slotId));
            string suffix = kind == SaveStorageFileKind.Current ? ".sav" :
                kind == SaveStorageFileKind.Backup ? ".bak" : ".tmp";
            return Path.Combine(_rootPath, slotId.Value + suffix);
        }

        public UniTask<IReadOnlyList<SaveSlotInfo>> ListSlotsAsync(SaveKitOptions options, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureRoot();
            var result = new List<SaveSlotInfo>();
            foreach (string path in Directory.GetFiles(_rootPath, "*.sav", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string name = Path.GetFileNameWithoutExtension(path);
                if (!SaveSlotId.TryCreate(name, out SaveSlotId slot, out string ignored))
                {
                    continue;
                }

                SaveSlotInfo info = new SaveSlotInfo
                {
                    FileSizeBytes = new FileInfo(path).Length,
                    HasBackup = File.Exists(GetFilePath(slot, SaveStorageFileKind.Backup)),
                    IsHealthy = false
                };

                try
                {
                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                        4096, FileOptions.SequentialScan))
                    {
                        // Slot lists are a metadata index. Do not materialize every payload
                        // just to populate the list; Inspector/Load performs full checksums.
                        if (SaveContainerReader.TryReadMetadata(stream, options, out SaveMetadata metadata,
                            out SaveErrorCode errorCode, out string errorMessage))
                        {
                            info.Metadata = metadata;
                            info.IsHealthy = true;
                            info.HealthMessage = "MetadataValid (payload checksum deferred)";
                        }
                        else
                        {
                            info.HealthMessage = $"{errorCode}: {errorMessage}";
                        }
                    }
                }
                catch (Exception exception)
                {
                    info.HealthMessage = exception.Message;
                }

                result.Add(info);
            }

            return UniTask.FromResult<IReadOnlyList<SaveSlotInfo>>(result.OrderBy(info => info.Metadata == null ? string.Empty : info.Metadata.SlotId.Value,
                StringComparer.Ordinal).ToArray());
        }

        public UniTask<bool> ExistsAsync(SaveSlotId slotId, SaveStorageFileKind kind, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return UniTask.FromResult(File.Exists(GetFilePath(slotId, kind)));
        }

        public UniTask<Stream> OpenReadAsync(SaveSlotId slotId, SaveStorageFileKind kind, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return UniTask.FromResult<Stream>(new FileStream(GetFilePath(slotId, kind), FileMode.Open, FileAccess.Read,
                FileShare.Read, 4096, FileOptions.SequentialScan));
        }

        public async UniTask WriteTemporaryAsync(SaveSlotId slotId, Stream source, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureRoot();
            string temporaryPath = GetFilePath(slotId, SaveStorageFileKind.Temporary);
            try
            {
                if (source.CanSeek) source.Position = 0;
                using (var destination = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None,
                    81920, FileOptions.WriteThrough))
                {
                    await source.CopyToAsync(destination, 81920, cancellationToken);
                    await destination.FlushAsync(cancellationToken);
                    destination.Flush(true);
                }
            }
            catch
            {
                TryDelete(temporaryPath);
                throw;
            }
        }

        public UniTask CommitAsync(SaveSlotId slotId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureRoot();
            string temporary = GetFilePath(slotId, SaveStorageFileKind.Temporary);
            string current = GetFilePath(slotId, SaveStorageFileKind.Current);
            string backup = GetFilePath(slotId, SaveStorageFileKind.Backup);
            if (!File.Exists(temporary)) throw new IOException("临时存档不存在。" );

            if (File.Exists(current))
            {
                try
                {
                    File.Replace(temporary, current, backup, true);
                }
                catch (PlatformNotSupportedException)
                {
                    FallbackCommit(temporary, current, backup);
                }
                catch (IOException)
                {
                    FallbackCommit(temporary, current, backup);
                }
            }
            else
            {
                File.Move(temporary, current);
            }

            return UniTask.CompletedTask;
        }

        public UniTask RestoreBackupAsync(SaveSlotId slotId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string backup = GetFilePath(slotId, SaveStorageFileKind.Backup);
            string current = GetFilePath(slotId, SaveStorageFileKind.Current);
            string temporary = GetFilePath(slotId, SaveStorageFileKind.Temporary);
            if (!File.Exists(backup)) throw new FileNotFoundException("Backup 存档不存在。", backup);
            File.Copy(backup, temporary, true);
            try
            {
                // Recovery must not rotate the damaged current file into .bak and
                // overwrite the only known-good backup. Copy the verified backup over
                // current, then remove tmp; backup remains available for another retry.
                File.Copy(temporary, current, true);
                TryDelete(temporary);
            }
            catch
            {
                TryDelete(temporary);
                throw;
            }
            return UniTask.CompletedTask;
        }

        public UniTask DeleteAsync(SaveSlotId slotId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TryDelete(GetFilePath(slotId, SaveStorageFileKind.Current));
            TryDelete(GetFilePath(slotId, SaveStorageFileKind.Backup));
            TryDelete(GetFilePath(slotId, SaveStorageFileKind.Temporary));
            return UniTask.CompletedTask;
        }

        private void EnsureRoot()
        {
            if (!Directory.Exists(_rootPath)) Directory.CreateDirectory(_rootPath);
        }

        private static void FallbackCommit(string temporary, string current, string backup)
        {
            if (File.Exists(current)) File.Copy(current, backup, true);
            File.Delete(current);
            File.Move(temporary, current);
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }

    /// <summary>用于 EditMode 故障注入和无文件系统测试的内存存储。</summary>
    public sealed class InMemorySaveStorage : ISaveStorage
    {
        private readonly Dictionary<string, byte[]> _current = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        private readonly Dictionary<string, byte[]> _backup = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        private readonly Dictionary<string, byte[]> _temporary = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        public bool FailWrite { get; set; }
        public bool FailCommit { get; set; }
        public string Description => "InMemory";

        public byte[] GetBytes(string slotId, SaveStorageFileKind kind)
        {
            if (slotId == null) return null;
            return GetMap(kind).TryGetValue(slotId, out byte[] bytes) ? (byte[])bytes.Clone() : null;
        }

        public void SetBytes(string slotId, SaveStorageFileKind kind, byte[] bytes)
        {
            if (string.IsNullOrEmpty(slotId)) throw new ArgumentException("Slot ID 不能为空。", nameof(slotId));
            if (bytes == null) GetMap(kind).Remove(slotId);
            else GetMap(kind)[slotId] = (byte[])bytes.Clone();
        }

        public string GetFilePath(SaveSlotId slotId, SaveStorageFileKind kind) => slotId.Value +
            (kind == SaveStorageFileKind.Current ? ".sav" : kind == SaveStorageFileKind.Backup ? ".bak" : ".tmp");

        public UniTask<IReadOnlyList<SaveSlotInfo>> ListSlotsAsync(SaveKitOptions options, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = new List<SaveSlotInfo>();
            foreach (KeyValuePair<string, byte[]> pair in _current)
            {
                if (!SaveSlotId.TryCreate(pair.Key, out SaveSlotId slot, out string ignored)) continue;
                var info = new SaveSlotInfo
                {
                    FileSizeBytes = pair.Value.Length,
                    HasBackup = _backup.ContainsKey(pair.Key)
                };
                using (var stream = new MemoryStream(pair.Value, false))
                {
                    info.IsHealthy = SaveContainerReader.TryReadMetadata(stream, options, out SaveMetadata metadata,
                        out SaveErrorCode code, out string message);
                    info.Metadata = metadata;
                    info.HealthMessage = info.IsHealthy ? "MetadataValid (payload checksum deferred)" : $"{code}: {message}";
                }
                result.Add(info);
            }

            return UniTask.FromResult<IReadOnlyList<SaveSlotInfo>>(result);
        }

        public UniTask<bool> ExistsAsync(SaveSlotId slotId, SaveStorageFileKind kind, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return UniTask.FromResult(GetMap(kind).ContainsKey(slotId.Value));
        }

        public UniTask<Stream> OpenReadAsync(SaveSlotId slotId, SaveStorageFileKind kind, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte[] bytes;
            if (!GetMap(kind).TryGetValue(slotId.Value, out bytes)) throw new FileNotFoundException(slotId.Value);
            return UniTask.FromResult<Stream>(new MemoryStream(bytes, false));
        }

        public async UniTask WriteTemporaryAsync(SaveSlotId slotId, Stream source, CancellationToken cancellationToken)
        {
            if (FailWrite) throw new IOException("模拟 Write Fail。" );
            cancellationToken.ThrowIfCancellationRequested();
            using (var memory = new MemoryStream())
            {
                if (source.CanSeek) source.Position = 0;
                await source.CopyToAsync(memory, 81920, cancellationToken);
                _temporary[slotId.Value] = memory.ToArray();
            }
        }

        public UniTask CommitAsync(SaveSlotId slotId, CancellationToken cancellationToken)
        {
            if (FailCommit) throw new IOException("模拟 Commit Fail。" );
            cancellationToken.ThrowIfCancellationRequested();
            byte[] temporary;
            if (!_temporary.TryGetValue(slotId.Value, out temporary)) throw new IOException("临时存档不存在。" );
            if (_current.TryGetValue(slotId.Value, out byte[] current)) _backup[slotId.Value] = current;
            _current[slotId.Value] = temporary;
            _temporary.Remove(slotId.Value);
            return UniTask.CompletedTask;
        }

        public UniTask RestoreBackupAsync(SaveSlotId slotId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_backup.TryGetValue(slotId.Value, out byte[] backup)) throw new FileNotFoundException(slotId.Value);
            _current[slotId.Value] = backup;
            return UniTask.CompletedTask;
        }

        public UniTask DeleteAsync(SaveSlotId slotId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _current.Remove(slotId.Value);
            _backup.Remove(slotId.Value);
            _temporary.Remove(slotId.Value);
            return UniTask.CompletedTask;
        }

        private Dictionary<string, byte[]> GetMap(SaveStorageFileKind kind) => kind == SaveStorageFileKind.Current ? _current :
            kind == SaveStorageFileKind.Backup ? _backup : _temporary;
    }
}
