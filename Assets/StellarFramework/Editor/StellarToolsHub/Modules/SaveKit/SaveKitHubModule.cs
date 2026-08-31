#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                }
            }
        }

        private void DrawMigration()
        {
            Section("Migration");
            if (_snapshot == null)
            {
                EditorGUILayout.HelpBox("请先打开存档。", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox("此页面只展示存档版本与当前注册 Section 版本。Dry Run 不会执行 Restore；正式迁移仍由 Load Pipeline 在验证全部 Section 后执行。", MessageType.Info);
            foreach (SaveSectionEntry entry in _snapshot.Sections)
            {
                string current = "Unavailable";
                if (SaveKit.TryGetSection(entry.Descriptor.Id, out ISaveSection section)) current = section.SchemaVersion.ToString();
                EditorGUILayout.LabelField(entry.Descriptor.Id.Value,
                    $"Stored {entry.Descriptor.SchemaVersion}  ->  Current {current}");
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
            SaveOperationDiagnostics diagnostics = SaveKit.GetDiagnostics();
            if (diagnostics != null && diagnostics.LastError != SaveErrorCode.None)
            {
                EditorGUILayout.HelpBox($"Last Error: {diagnostics.LastError}\n{diagnostics.LastErrorMessage}", MessageType.Warning);
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
                _tab = Tab.Inspector;
            }
            catch (Exception exception) { _message = exception.Message; }
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
