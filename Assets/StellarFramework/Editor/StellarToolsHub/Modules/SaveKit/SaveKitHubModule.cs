#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using StellarFramework;
using UnityEditor;
using UnityEngine;

namespace StellarFramework.Editor.SaveKitTools
{
    [StellarTool("SaveKit 存档中心", "框架核心", 6,
        RequiredAssemblyNames = new[] { "StellarFramework.SaveKit.Core" })]
    public sealed class SaveKitHubModule : ToolModule
    {
        private enum Tab { Slots, Inspector, Migration, Profiler, Diagnostics }

        private Tab _tab;
        private Vector2 _scroll;
        private string _externalPath = string.Empty;
        private string _importSlot = "imported";
        private string _message = string.Empty;
        private SaveSnapshot _snapshot;
        private SaveSlotInfo[] _slots = Array.Empty<SaveSlotInfo>();
        private SaveSectionEntry _previewEntry;
        private string _previewSourcePath;
        private bool _hexPreview;
        private long _hexOffset;
        private string _hexJumpOffset = "0";

        private const int DefaultMaxPreviewBytes = 64 * 1024;
        private const int DefaultHexPageBytes = 256;

        private int PreviewLimit => SaveKit.Options == null ? DefaultMaxPreviewBytes : SaveKit.Options.MaxPreviewBytes;
        private int HexPageSize => SaveKit.Options == null ? DefaultHexPageBytes : SaveKit.Options.MaxHexPageBytes;

        public override string Icon => "d_SaveAs";
        public override string Description => "安全查看、验证和恢复 SaveKit 存档；外部存档默认只读，不会直接修改玩家原文件。";

        public override void OnEnable()
        {
            if (!SaveKit.IsInitialized) SaveKit.Initialize();
            RefreshSlots();
        }

        public override void OnGUI()
        {
            DrawHeader();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            switch (_tab)
            {
                case Tab.Slots: DrawSlots(); break;
                case Tab.Inspector: DrawInspector(); break;
                case Tab.Migration: DrawMigration(); break;
                case Tab.Profiler: DrawProfiler(); break;
                case Tab.Diagnostics: DrawDiagnostics(); break;
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            using (new GUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _tab = (Tab)GUILayout.Toolbar((int)_tab,
                    new[] { "Slots", "Inspector", "Migration", "Profiler", "Diagnostics" }, EditorStyles.toolbarButton);
                if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(54))) RefreshSlots();
            }

            EditorGUILayout.HelpBox(
                "Open External Save 只读解析外部文件；Import Copy 会创建测试副本。所有外部文件均受大小、Section 数量和容器校验限制。",
                MessageType.Info);
            if (!string.IsNullOrEmpty(_message)) EditorGUILayout.HelpBox(_message, MessageType.None);
        }

        private void DrawSlots()
        {
            Section("本地存档");
            if (_slots.Length == 0)
            {
                EditorGUILayout.HelpBox("当前没有可用的 .sav 文件。", MessageType.Info);
            }
            else
            {
                foreach (SaveSlotInfo slot in _slots)
                {
                    using (new GUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        string slotName = slot.Metadata == null ? "未知 Slot" : slot.Metadata.SlotId.Value;
                        EditorGUILayout.LabelField(slotName, EditorStyles.boldLabel);
                        EditorGUILayout.LabelField("Revision", slot.Metadata == null ? "-" : slot.Metadata.Revision.ToString());
                        EditorGUILayout.LabelField("大小", FormatBytes(slot.FileSizeBytes));
                        EditorGUILayout.LabelField("健康度", slot.IsHealthy ? "Healthy" : slot.HealthMessage ?? "Invalid");
                        EditorGUILayout.LabelField("Backup", slot.HasBackup ? "Available" : "None");
                        using (new GUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button("检查", GUILayout.Height(24))) OpenSlot(slotName);
                            if (GUILayout.Button("加载并恢复", GUILayout.Height(24))) LoadSlot(slotName);
                            if (slot.HasBackup && GUILayout.Button("恢复 Backup", GUILayout.Height(24))) RestoreBackup(slotName);
                            if (GUILayout.Button("删除", GUILayout.Height(24)) &&
                                EditorUtility.DisplayDialog("删除存档", $"确定删除 {slotName} 的 current、backup 和 tmp 吗？", "删除", "取消"))
                            {
                                SaveResult result = SaveKit.DeleteAsync(slotName).GetAwaiter().GetResult();
                                _message = result.IsSuccess ? "存档已删除。" : result.ErrorMessage;
                                RefreshSlots();
                            }
                        }
                    }
                }
            }

            Section("外部存档");
            using (new GUILayout.HorizontalScope())
            {
                _externalPath = EditorGUILayout.TextField("文件", _externalPath);
                if (GUILayout.Button("选择", GUILayout.Width(54)))
                {
                    string selected = EditorUtility.OpenFilePanel("Open External Save", string.Empty, "sav");
                    if (!string.IsNullOrEmpty(selected)) _externalPath = selected;
                }
                if (GUILayout.Button("只读打开", GUILayout.Width(76))) OpenExternal();
            }
            using (new GUILayout.HorizontalScope())
            {
                _importSlot = EditorGUILayout.TextField("导入副本 Slot", _importSlot);
                if (GUILayout.Button("Import Copy", GUILayout.Width(100))) ImportCopy();
            }
        }

        private void DrawInspector()
        {
            Section("只读 Inspector");
            if (_snapshot == null)
            {
                EditorGUILayout.HelpBox("请先在 Slots 页面打开本地或外部存档。", MessageType.Info);
                return;
            }

            SaveMetadata metadata = _snapshot.Metadata;
            EditorGUILayout.LabelField("Slot", metadata.SlotId.Value);
            EditorGUILayout.LabelField("Container", metadata.ContainerVersion.ToString());
            EditorGUILayout.LabelField("Revision", metadata.Revision.ToString());
            EditorGUILayout.LabelField("Application", metadata.ApplicationVersion);
            EditorGUILayout.LabelField("Created", metadata.CreatedUtc.ToString("u"));
            EditorGUILayout.LabelField("Updated", metadata.UpdatedUtc.ToString("u"));
            EditorGUILayout.LabelField("Sections", _snapshot.Sections.Count.ToString());
            foreach (SaveSectionEntry entry in _snapshot.Sections)
            {
                using (new GUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(entry.Descriptor.Id.Value, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Schema", entry.Descriptor.SchemaVersion.ToString());
                    EditorGUILayout.LabelField("Serializer", entry.Descriptor.SerializerId);
                    EditorGUILayout.LabelField("Payload", FormatBytes(entry.Descriptor.PayloadLength));
                    EditorGUILayout.LabelField("Checksum", entry.Descriptor.Checksum.ToString("X16"));
                    using (new GUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Raw Preview", GUILayout.Width(105))) ShowRawPreview(entry);
                        if (GUILayout.Button("Hex Preview", GUILayout.Width(105))) ShowHexPreview(entry, 0);
                    }
                }
            }

            DrawPayloadPreview();
        }

        private void DrawMigration()
        {
            Section("Migration");
            if (_snapshot == null)
            {
                EditorGUILayout.HelpBox("请先打开存档。", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox("此页面展示版本/类型链。Dry Run 只读取、校验、反序列化、迁移和验证，不执行 Restore、不写回 Save。", MessageType.Info);
            if (GUILayout.Button("Run Migration Dry Run", GUILayout.Height(26))) RunMigrationDryRun();
            foreach (SaveSectionEntry entry in _snapshot.Sections)
            {
                string current = "Unavailable";
                if (SaveKit.TryGetSection(entry.Descriptor.Id, out ISaveSection section))
                {
                    current = section.SchemaVersion.ToString();
                    EditorGUILayout.LabelField(entry.Descriptor.Id.Value,
                        $"Stored {entry.Descriptor.SchemaVersion} ({GetStoredTypeLabel(entry, section)})  ->  Current {current} ({section.DataType.FullName})");
                    if (entry.Descriptor.SchemaVersion < section.SchemaVersion)
                    {
                        if (SaveKit.TryBuildMigrationChain(entry.Descriptor.Id, entry.Descriptor.SchemaVersion,
                            section.SchemaVersion, out IReadOnlyList<ISaveMigration> chain, out string error))
                        {
                            foreach (ISaveMigration migration in chain)
                            {
                                EditorGUILayout.LabelField("  Migration",
                                    $"{migration.FromVersion} -> {migration.ToVersion}: {migration.FromType.FullName} -> {migration.ToType.FullName}");
                            }
                        }
                        else
                        {
                            EditorGUILayout.HelpBox($"Migration Graph Unavailable: {error}", MessageType.Warning);
                        }
                    }
                }
                else
                {
                    EditorGUILayout.LabelField(entry.Descriptor.Id.Value,
                        $"Stored {entry.Descriptor.SchemaVersion} -> Current Schema: Unavailable / Migration Graph: Unavailable");
                }
            }
        }

        private void DrawProfiler()
        {
            Section("最近一次操作");
            SaveOperationDiagnostics diagnostics = SaveKit.GetDiagnostics();
            if (diagnostics == null)
            {
                EditorGUILayout.HelpBox("尚未执行 SaveKit 操作。", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Operation", diagnostics.Operation);
            EditorGUILayout.LabelField("Slot", diagnostics.SlotId.Value);
            EditorGUILayout.LabelField("Capture", FormatMs(diagnostics.CaptureDurationMs));
            EditorGUILayout.LabelField("Serialize", FormatMs(diagnostics.SerializeDurationMs));
            EditorGUILayout.LabelField("Checksum", FormatMs(diagnostics.ChecksumDurationMs));
            EditorGUILayout.LabelField("IO", FormatMs(diagnostics.IoDurationMs));
            EditorGUILayout.LabelField("Commit", FormatMs(diagnostics.CommitDurationMs));
            EditorGUILayout.LabelField("Total", FormatMs(diagnostics.TotalDurationMs));
            EditorGUILayout.LabelField("Raw / Final", $"{FormatBytes(diagnostics.RawBytes)} / {FormatBytes(diagnostics.FinalBytes)}");
            EditorGUILayout.LabelField("Migrations", diagnostics.MigrationCount.ToString());
            if (diagnostics.Sections.Count > 0)
            {
                Section("Section 详情");
                foreach (SaveSectionDiagnostics section in diagnostics.Sections)
                {
                    EditorGUILayout.LabelField(section.SectionId.Value,
                        $"Stored {section.StoredSchemaVersion} -> Current {section.CurrentSchemaVersion}");
                    EditorGUILayout.LabelField("  Serializer / Type",
                        $"{section.SerializerId ?? "-"} / {section.StoredType ?? "-"} -> {section.CurrentType ?? "-"}");
                    EditorGUILayout.LabelField("  Payload / Migration",
                        $"{FormatBytes(section.PayloadBytes)} / {section.MigrationSteps}");
                }
            }
        }

        private void DrawDiagnostics()
        {
            Section("运行时诊断");
            SaveKitOptions options = SaveKit.Options;
            EditorGUILayout.LabelField("Initialized", SaveKit.IsInitialized ? "Yes" : "No");
            EditorGUILayout.LabelField("Storage", SaveKit.Storage == null ? "-" : SaveKit.Storage.Description);
            EditorGUILayout.LabelField("Default Serializer", SaveKit.DefaultSerializerId);
            EditorGUILayout.LabelField("Container Version", SaveKitOptions.CurrentContainerVersion.ToString());
            EditorGUILayout.LabelField("Max Section Count", options == null ? "-" : options.MaxSectionCount.ToString());
            EditorGUILayout.LabelField("Max Payload", options == null ? "-" : FormatBytes(options.MaxPayloadBytes));
            EditorGUILayout.LabelField("Preview Limit", options == null ? "-" : FormatBytes(options.MaxPreviewBytes));
            EditorGUILayout.LabelField("Hex Page", options == null ? "-" : options.MaxHexPageBytes + " bytes");
            SaveOperationDiagnostics diagnostics = SaveKit.GetDiagnostics();
            if (diagnostics != null && diagnostics.LastError != SaveErrorCode.None)
            {
                EditorGUILayout.HelpBox(BuildErrorDetails(diagnostics), MessageType.Warning);
                if (GUILayout.Button("Copy Details")) EditorGUIUtility.systemCopyBuffer = BuildErrorDetails(diagnostics);
            }
        }

        private void RefreshSlots()
        {
            try
            {
                _slots = SaveKit.GetSlotsAsync().GetAwaiter().GetResult().ToArray();
                _message = $"已扫描 {_slots.Length} 个 Slot。";
            }
            catch (Exception exception) { _message = exception.Message; }
        }

        private void OpenSlot(string slotName)
        {
            try
            {
                ISaveStorage storage = SaveKit.Storage;
                if (storage == null) { _message = "SaveKit Storage 尚未初始化。"; return; }
                using (Stream stream = storage.OpenReadAsync(SaveSlotId.From(slotName), SaveStorageFileKind.Current, CancellationToken.None).GetAwaiter().GetResult())
                {
                    if (!SaveContainerReader.TryRead(stream, SaveKit.Options, out _snapshot, out SaveErrorCode code, out string message))
                    {
                        _snapshot = null;
                        _message = $"{code}: {message}";
                    }
                    else _message = "已只读打开本地存档。";
                }
                _previewSourcePath = storage.GetFilePath(SaveSlotId.From(slotName), SaveStorageFileKind.Current);
                _tab = Tab.Inspector;
            }
            catch (Exception exception) { _message = exception.Message; }
        }

        private void LoadSlot(string slotName)
        {
            SaveResult result = SaveKit.LoadAsync(slotName).GetAwaiter().GetResult();
            _message = result.IsSuccess ? "存档加载成功。" : $"{result.ErrorCode}: {result.ErrorMessage}";
            RefreshSlots();
        }

        private void RestoreBackup(string slotName)
        {
            if (!EditorUtility.DisplayDialog("恢复 Backup", "将已验证的 Backup 恢复为 current，并保留 Backup。继续吗？", "恢复", "取消")) return;
            try
            {
                ISaveStorage storage = SaveKit.Storage;
                if (storage == null) { _message = "SaveKit Storage 尚未初始化。"; return; }
                storage.RestoreBackupAsync(SaveSlotId.From(slotName), CancellationToken.None).GetAwaiter().GetResult();
                _message = "Backup 已恢复。";
                RefreshSlots();
            }
            catch (Exception exception) { _message = exception.Message; }
        }

        private void OpenExternal()
        {
            if (string.IsNullOrEmpty(_externalPath) || !File.Exists(_externalPath)) { _message = "外部文件不存在。"; return; }
            try
            {
                using (var stream = new FileStream(_externalPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (!SaveContainerReader.TryRead(stream, SaveKit.Options, out _snapshot, out SaveErrorCode code, out string message))
                    {
                        _snapshot = null;
                        _message = $"{code}: {message}";
                    }
                    else _message = "已只读打开外部存档，原文件未修改。";
                }
                _previewSourcePath = _externalPath;
                _tab = Tab.Inspector;
            }
            catch (Exception exception) { _message = exception.Message; }
        }

        private void RunMigrationDryRun()
        {
            try
            {
                SaveResult result = SaveKit.RunMigrationDryRunAsync(_snapshot).GetAwaiter().GetResult();
                _message = result.IsSuccess
                    ? "Migration Dry Run 通过：未执行 Restore、未写回 Save。"
                    : BuildResultDetails(result);
                if (result.Diagnostics != null) _tab = Tab.Diagnostics;
            }
            catch (Exception exception) { _message = exception.Message; }
        }

        private void ShowRawPreview(SaveSectionEntry entry)
        {
            _previewEntry = entry;
            _hexPreview = false;
            _hexOffset = 0;
        }

        private void ShowHexPreview(SaveSectionEntry entry, long offset)
        {
            _previewEntry = entry;
            _hexPreview = true;
            long length = entry == null || entry.Descriptor == null ? 0 : entry.Descriptor.PayloadLength;
            _hexOffset = Math.Max(0L, Math.Min(offset, Math.Max(0L, length - 1L)));
            _hexOffset = (_hexOffset / HexPageSize) * HexPageSize;
        }

        private void DrawPayloadPreview()
        {
            if (_previewEntry == null || _previewEntry.Descriptor == null) return;
            Section(_hexPreview ? "Hex Preview" : "Raw Preview");
            long fullLength = _previewEntry.Descriptor.PayloadLength;
            if (!_hexPreview)
            {
                byte[] previewPayload = ReadPreviewBytes(_previewEntry, 0, PreviewLimit);
                int textCount = previewPayload.Length;
                string text = Encoding.UTF8.GetString(previewPayload, 0, textCount);
                if (fullLength > textCount) text += $"\n\n[Preview truncated at {FormatBytes(PreviewLimit)} / full {FormatBytes(fullLength)}]";
                EditorGUILayout.TextArea(text, GUILayout.MinHeight(160));
                return;
            }

            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Previous", GUILayout.Width(70))) ShowHexPreview(_previewEntry, _hexOffset - HexPageSize);
                if (GUILayout.Button("Next", GUILayout.Width(70))) ShowHexPreview(_previewEntry, _hexOffset + HexPageSize);
                _hexJumpOffset = EditorGUILayout.TextField("Jump Offset", _hexJumpOffset);
                if (GUILayout.Button("Jump", GUILayout.Width(50)) && long.TryParse(_hexJumpOffset, out long jump)) ShowHexPreview(_previewEntry, jump);
            }

            int hexCount = (int)Math.Min(HexPageSize, Math.Max(0L, fullLength - _hexOffset));
            byte[] hexPayload = ReadPreviewBytes(_previewEntry, _hexOffset, hexCount);
            StringBuilder builder = new StringBuilder(Math.Max(64, hexCount * 4));
            for (int row = 0; row < hexCount; row += 16)
            {
                int rowCount = Math.Min(16, hexCount - row);
                builder.Append((_hexOffset + row).ToString("X8")).Append("  ");
                for (int i = 0; i < 16; i++)
                {
                    builder.Append(i < rowCount ? hexPayload[row + i].ToString("X2") : "  ").Append(' ');
                }
                builder.Append(" |");
                for (int i = 0; i < rowCount; i++)
                {
                    byte value = hexPayload[row + i];
                    builder.Append(value >= 32 && value <= 126 ? (char)value : '.');
                }
                builder.Append('|').AppendLine();
            }
            EditorGUILayout.TextArea(builder.ToString(), GUILayout.MinHeight(160));
            EditorGUILayout.LabelField($"Offset {_hexOffset} - {_hexOffset + hexCount} / {fullLength} bytes");
        }

        private byte[] ReadPreviewBytes(SaveSectionEntry entry, long offset, int count)
        {
            if (count <= 0 || entry == null || entry.Descriptor == null || offset < 0 || offset >= entry.Descriptor.PayloadLength)
                return Array.Empty<byte>();
            int expected = (int)Math.Min((long)count, entry.Descriptor.PayloadLength - offset);
            if (!string.IsNullOrEmpty(_previewSourcePath) && File.Exists(_previewSourcePath))
            {
                using (var stream = new FileStream(_previewSourcePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                    4096, FileOptions.SequentialScan))
                {
                    stream.Seek(entry.Descriptor.PayloadOffset + offset, SeekOrigin.Begin);
                    byte[] buffer = new byte[expected];
                    int read = 0;
                    while (read < buffer.Length)
                    {
                        int next = stream.Read(buffer, read, buffer.Length - read);
                        if (next <= 0) break;
                        read += next;
                    }
                    if (read == buffer.Length) return buffer;
                    Array.Resize(ref buffer, read);
                    return buffer;
                }
            }

            byte[] payload = entry.Payload ?? Array.Empty<byte>();
            if (offset >= payload.LongLength) return Array.Empty<byte>();
            int fallbackLength = Math.Min(expected, payload.Length - (int)offset);
            var fallback = new byte[fallbackLength];
            Buffer.BlockCopy(payload, (int)offset, fallback, 0, fallbackLength);
            return fallback;
        }

        private static string GetStoredTypeLabel(SaveSectionEntry entry, ISaveSection section)
        {
            if (entry.Descriptor.SchemaVersion == section.SchemaVersion) return section.DataType.FullName;
            return "由 Migration Chain 决定";
        }

        private static string BuildResultDetails(SaveResult result)
        {
            if (result == null) return "SaveKit 未返回结果。";
            return $"ErrorCode: {result.ErrorCode}\nSlot: {result.SlotId}\nMessage: {result.ErrorMessage}";
        }

        private static string BuildErrorDetails(SaveOperationDiagnostics diagnostics)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"ErrorCode: {diagnostics.LastError}");
            builder.AppendLine($"Slot: {diagnostics.SlotId}");
            builder.AppendLine($"Message: {diagnostics.LastErrorMessage}");
            if (!string.IsNullOrEmpty(diagnostics.LastExceptionType)) builder.AppendLine($"Exception: {diagnostics.LastExceptionType}");
            foreach (SaveSectionDiagnostics section in diagnostics.Sections)
            {
                builder.AppendLine($"Section: {section.SectionId}");
                builder.AppendLine($"StoredVersion: {section.StoredSchemaVersion}");
                builder.AppendLine($"CurrentVersion: {section.CurrentSchemaVersion}");
                builder.AppendLine($"SerializerId: {section.SerializerId}");
            }
            return builder.ToString().TrimEnd();
        }

        private void ImportCopy()
        {
            if (string.IsNullOrEmpty(_externalPath) || !File.Exists(_externalPath)) { _message = "请先选择外部存档。"; return; }
            if (!SaveSlotId.TryCreate(_importSlot, out SaveSlotId slot, out string error)) { _message = error; return; }
            try
            {
                var storage = SaveKit.Storage as FileSystemSaveStorage;
                if (storage == null)
                {
                    _message = "Import Copy 目前只支持 FileSystemSaveStorage。";
                    return;
                }
                if (storage.ExistsAsync(slot, SaveStorageFileKind.Current, CancellationToken.None).GetAwaiter().GetResult())
                {
                    _message = "目标 Slot 已存在，未覆盖。";
                    return;
                }

                using (var source = new FileStream(_externalPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (!SaveContainerReader.TryRead(source, SaveKit.Options, out SaveSnapshot ignored, out SaveErrorCode code, out string message))
                    {
                        _message = $"{code}: {message}";
                        return;
                    }
                }

                Directory.CreateDirectory(Path.GetDirectoryName(storage.GetFilePath(slot, SaveStorageFileKind.Current)));
                File.Copy(_externalPath, storage.GetFilePath(slot, SaveStorageFileKind.Current), false);
                _message = "已导入为测试副本。";
                RefreshSlots();
            }
            catch (Exception exception) { _message = exception.Message; }
        }

        private static string FormatBytes(long value)
        {
            if (value < 1024) return value + " B";
            if (value < 1024 * 1024) return (value / 1024d).ToString("0.0") + " KB";
            return (value / (1024d * 1024d)).ToString("0.0") + " MB";
        }

        private static string FormatMs(double value) => value.ToString("0.###") + " ms";
    }
}
#endif
