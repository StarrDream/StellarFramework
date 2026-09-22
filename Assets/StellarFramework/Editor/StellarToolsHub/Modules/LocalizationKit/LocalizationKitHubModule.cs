using System.Linq;
using StellarFramework.Localization.Editor;
using StellarFramework.Localization.UnityUGUI;
using UnityEditor;
using UnityEngine;

namespace StellarFramework.Editor.Modules
{
    [StellarTool("Localization 本地化", "生产力", -15,
        RequiredAssemblyNames = new[]
        {
            "StellarFramework.LocalizationKit.Editor",
            "StellarFramework.LocalizationKit.UnityUGUIAdapter"
        })]
    public sealed class LocalizationKitHubModule : ToolModule
    {
        private LocalizationCatalogAsset _catalog;
        private LocalizationWorkspaceAsset _workspace;
        private GameObject _scanPrefab;
        private LocalizationTableAsset _sourceTable;
        private string _sourceLocale = "zh-CN";
        private LocalizationUiScanResult _scanResult;
        private Vector2 _scanScroll;
        private Vector2 _translationScroll;
        private string _translationSearch = string.Empty;
        private bool _showOnlyMissing;

        public override string Icon => "d_UnityEditor.InspectorWindow";
        public override string Description =>
            "LocalizationKit 的 UI 扫描/绑定、Catalog 校验、覆盖率检查与示例字体维护入口。运行时 Core 保持独立，不依赖 ToolsHub。";

        public override void OnEnable()
        {
            SyncCatalogFromSelection();
        }

        public override void OnSelectionChange()
        {
            SyncCatalogFromSelection();
        }

        public override void OnGUI()
        {
            DrawWorkspace();
            DrawScanAndBind();
            DrawTranslationMatrix();
            DrawTranslationExchange();

            Section("Catalog 校验");
            EditorGUILayout.HelpBox(
                "选择一个 LocalizationCatalogAsset 后执行校验。Validator 会检查缺失 Key、空值、重复项、Fallback 与跨语言 Placeholder Contract。",
                MessageType.Info);

            _catalog = (LocalizationCatalogAsset)EditorGUILayout.ObjectField(
                "当前选择",
                _catalog,
                typeof(LocalizationCatalogAsset),
                false);

            using (new EditorGUI.DisabledScope(_catalog == null))
            {
                if (PrimaryButton("校验当前 Localization Catalog", GUILayout.Height(32)))
                {
                    LocalizationCatalogValidator.ValidateAndShowReport(_catalog);
                }
            }

            Section("示例字体");
            bool installed = SourceHanSansExampleFontInstaller.TryVerifyInstalled(out string verifyError);
            EditorGUILayout.HelpBox(
                installed
                    ? "Source Han Sans 示例字体与 License 校验通过。"
                    : "示例字体未安装或校验未通过：" + (string.IsNullOrWhiteSpace(verifyError) ? "未知原因" : verifyError),
                installed ? MessageType.Info : MessageType.Warning);

            if (PrimaryButton(installed ? "重新校验 / 安装示例字体" : "安装示例字体", GUILayout.Height(30)))
            {
                SourceHanSansExampleFontInstaller.Install();
            }
        }

        private void DrawWorkspace()
        {
            Section("Workspace");
            EditorGUILayout.HelpBox(
                "Workspace 只存在于 Editor，用来配置源语言、目标语言和对应 Table。Runtime Core 不依赖它。",
                MessageType.Info);

            _workspace = (LocalizationWorkspaceAsset)EditorGUILayout.ObjectField(
                "Localization Workspace",
                _workspace,
                typeof(LocalizationWorkspaceAsset),
                false);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("新建 Workspace", GUILayout.Height(26)))
                {
                    string path = EditorUtility.SaveFilePanelInProject(
                        "Create Localization Workspace",
                        "LocalizationWorkspace",
                        "asset",
                        "选择 Workspace 保存位置");
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        LocalizationWorkspaceAsset workspace =
                            ScriptableObject.CreateInstance<LocalizationWorkspaceAsset>();
                        AssetDatabase.CreateAsset(workspace, path);
                        AssetDatabase.SaveAssets();
                        _workspace = workspace;
                        Selection.activeObject = workspace;
                        EditorGUIUtility.PingObject(workspace);
                    }
                }

                using (new EditorGUI.DisabledScope(_workspace == null))
                {
                    if (GUILayout.Button("定位 Workspace", GUILayout.Height(26)))
                    {
                        Selection.activeObject = _workspace;
                        EditorGUIUtility.PingObject(_workspace);
                    }
                }
            }

            if (_workspace == null)
            {
                return;
            }

            EditorGUILayout.LabelField("Source Locale", _workspace.SourceLocale);
            LocalizationWorkspaceLanguage[] enabledLanguages = _workspace.GetEnabledLanguages();
            EditorGUILayout.LabelField(
                "Enabled Languages",
                enabledLanguages.Length == 0
                    ? "(未配置)"
                    : string.Join(", ", System.Array.ConvertAll(enabledLanguages, language => language.Locale)));

            if (_workspace.Catalog != null)
            {
                _catalog = _workspace.Catalog;
            }
            _sourceLocale = _workspace.SourceLocale;
            if (_workspace.TryGetSourceTable(out LocalizationTableAsset sourceTable, out _))
            {
                _sourceTable = sourceTable;
            }
        }

        private void DrawScanAndBind()
        {
            Section("Scan & Bind");
            EditorGUILayout.HelpBox(
                "扫描 UGUI Prefab 中已有文本，Preview 后再 Apply。首次扫描生成稳定 BindingId/Key；后续 Rename、Reparent、Reorder 不会自动改变 Key。",
                MessageType.Info);

            _scanPrefab = (GameObject)EditorGUILayout.ObjectField(
                "UI Prefab",
                _scanPrefab,
                typeof(GameObject),
                false);
            _sourceTable = (LocalizationTableAsset)EditorGUILayout.ObjectField(
                "源语言表",
                _sourceTable,
                typeof(LocalizationTableAsset),
                false);
            _sourceLocale = EditorGUILayout.TextField("源语言", _sourceLocale);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_scanPrefab == null))
                {
                    if (PrimaryButton("扫描 Prefab", GUILayout.Height(30)))
                    {
                        ScanPrefab();
                    }
                }

                using (new EditorGUI.DisabledScope(_scanResult == null || _sourceTable == null))
                {
                    if (GUILayout.Button("应用选中项", GUILayout.Height(30)))
                    {
                        ApplyScan();
                    }
                }
            }

            if (_scanResult == null)
            {
                return;
            }

            EditorGUILayout.LabelField(
                $"扫描结果：{_scanResult.Candidates.Count} 项 | Prefab Variant={_scanResult.IsVariant}",
                EditorStyles.miniBoldLabel);
            _scanScroll = EditorGUILayout.BeginScrollView(
                _scanScroll,
                GUILayout.MinHeight(180),
                GUILayout.MaxHeight(420));
            foreach (LocalizationUiScanCandidate candidate in _scanResult.Candidates)
            {
                DrawScanCandidate(candidate);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawTranslationMatrix()
        {
            Section("Translation Matrix");
            LocalizationTableAsset sourceTable = null;
            string sourceError = null;
            if (_workspace == null ||
                !_workspace.TryGetSourceTable(out sourceTable, out sourceError))
            {
                EditorGUILayout.HelpBox(
                    string.IsNullOrWhiteSpace(sourceError)
                        ? "选择 Workspace 后可查看翻译矩阵。"
                        : sourceError,
                    MessageType.Info);
                return;
            }

            LocalizationWorkspaceLanguage[] targetLanguages = _workspace.GetEnabledLanguages()
                .Where(language =>
                    !string.Equals(
                        language.Locale,
                        _workspace.SourceLocale,
                        System.StringComparison.OrdinalIgnoreCase))
                .ToArray();

            using (new EditorGUILayout.HorizontalScope())
            {
                _translationSearch = EditorGUILayout.TextField("搜索", _translationSearch);
                _showOnlyMissing = EditorGUILayout.ToggleLeft(
                    "仅缺失",
                    _showOnlyMissing,
                    GUILayout.Width(72));
            }

            int missingCount = 0;
            foreach (LocalizationAuthoringEntry sourceEntry in sourceTable.Entries)
            {
                if (sourceEntry == null)
                {
                    continue;
                }

                foreach (LocalizationWorkspaceLanguage language in targetLanguages)
                {
                    if (language.Table == null ||
                        !TryGetTranslation(language.Table, sourceEntry.Key, out string value) ||
                        string.IsNullOrWhiteSpace(value))
                    {
                        missingCount++;
                    }
                }
            }

            EditorGUILayout.LabelField(
                $"Source={_workspace.SourceLocale} | Keys={sourceTable.Entries.Count} | Missing={missingCount}",
                EditorStyles.miniBoldLabel);

            _translationScroll = EditorGUILayout.BeginScrollView(
                _translationScroll,
                GUILayout.MinHeight(180),
                GUILayout.MaxHeight(520));
            foreach (LocalizationAuthoringEntry sourceEntry in sourceTable.Entries)
            {
                if (sourceEntry == null || string.IsNullOrWhiteSpace(sourceEntry.Key))
                {
                    continue;
                }

                bool matchesSearch =
                    string.IsNullOrWhiteSpace(_translationSearch) ||
                    sourceEntry.Key.IndexOf(
                        _translationSearch,
                        System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    sourceEntry.Value.IndexOf(
                        _translationSearch,
                        System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (!matchesSearch)
                {
                    continue;
                }

                bool hasMissing = targetLanguages.Any(language =>
                    language.Table == null ||
                    !TryGetTranslation(language.Table, sourceEntry.Key, out string value) ||
                    string.IsNullOrWhiteSpace(value));
                if (_showOnlyMissing && !hasMissing)
                {
                    continue;
                }

                DrawTranslationRow(sourceEntry, targetLanguages, hasMissing);
            }
            EditorGUILayout.EndScrollView();
        }

        private static void DrawTranslationRow(
            LocalizationAuthoringEntry sourceEntry,
            LocalizationWorkspaceLanguage[] targetLanguages,
            bool hasMissing)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(sourceEntry.Key, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "Source",
                    sourceEntry.Value,
                    EditorStyles.wordWrappedLabel);

                foreach (LocalizationWorkspaceLanguage language in targetLanguages)
                {
                    if (language.Table == null)
                    {
                        EditorGUILayout.HelpBox(
                            $"{language.Locale}: 未配置 LocalizationTableAsset。",
                            MessageType.Warning);
                        continue;
                    }

                    TryGetTranslation(language.Table, sourceEntry.Key, out string current);
                    EditorGUI.BeginChangeCheck();
                    string next = EditorGUILayout.TextField(
                        string.IsNullOrWhiteSpace(language.DisplayName)
                            ? language.Locale
                            : $"{language.DisplayName} ({language.Locale})",
                        current ?? string.Empty);
                    if (EditorGUI.EndChangeCheck())
                    {
                        SetTranslation(language.Table, sourceEntry.Key, next);
                    }
                }

                if (hasMissing)
                {
                    EditorGUILayout.LabelField(
                        "Status: Missing Translation",
                        EditorStyles.miniLabel);
                }
            }
        }

        private static bool TryGetTranslation(
            LocalizationTableAsset table,
            string key,
            out string value)
        {
            value = null;
            if (table == null || string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            foreach (LocalizationAuthoringEntry entry in table.Entries)
            {
                if (entry != null &&
                    string.Equals(entry.Key, key, System.StringComparison.Ordinal))
                {
                    value = entry.Value;
                    return true;
                }
            }
            return false;
        }

        private static void SetTranslation(
            LocalizationTableAsset table,
            string key,
            string value)
        {
            var entries = new System.Collections.Generic.Dictionary<string, string>(
                System.StringComparer.Ordinal);
            foreach (LocalizationAuthoringEntry entry in table.Entries)
            {
                if (entry != null && !string.IsNullOrWhiteSpace(entry.Key))
                {
                    entries[entry.Key] = entry.Value;
                }
            }
            entries[key] = value ?? string.Empty;

            Undo.RecordObject(table, "Edit Localization Translation");
            table.Configure(
                table.Locale,
                entries
                    .OrderBy(pair => pair.Key, System.StringComparer.Ordinal)
                    .Select(pair => new LocalizationAuthoringEntry(pair.Key, pair.Value))
                    .ToArray());
            EditorUtility.SetDirty(table);
        }

        private void DrawTranslationExchange()
        {
            Section("Import / Export");
            EditorGUILayout.HelpBox(
                "Unity 不调用任何 AI 服务。导出 JSON / CSV 后可交给人工或外部 AI 翻译；导入时会校验 sourceHash，源文本变化的旧翻译不会静默覆盖。",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(_workspace == null))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("导出 JSON", GUILayout.Height(28)))
                    {
                        ExportTranslations(false);
                    }
                    if (GUILayout.Button("导出 CSV", GUILayout.Height(28)))
                    {
                        ExportTranslations(true);
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("导入 JSON", GUILayout.Height(28)))
                    {
                        ImportTranslations(false);
                    }
                    if (GUILayout.Button("导入 CSV", GUILayout.Height(28)))
                    {
                        ImportTranslations(true);
                    }
                }
            }
        }

        private void ExportTranslations(bool csv)
        {
            string extension = csv ? "csv" : "json";
            string path = EditorUtility.SaveFilePanel(
                "Export Localization Translations",
                Application.dataPath,
                "LocalizationTranslations",
                extension);
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            LocalizationSourceRegistry registry = LocalizationSourceRegistry.LoadOrCreate();
            bool success;
            string error;
            if (csv)
            {
                success = LocalizationTranslationExchange.TryExportCsv(
                    _workspace,
                    registry,
                    path,
                    out error);
            }
            else
            {
                success = LocalizationTranslationExchange.TryExportJson(
                    _workspace,
                    registry,
                    path,
                    out error);
            }

            if (!success)
            {
                EditorUtility.DisplayDialog("Localization Export", error, "确定");
                return;
            }

            EditorUtility.RevealInFinder(path);
        }

        private void ImportTranslations(bool csv)
        {
            string extension = csv ? "csv" : "json";
            string path = EditorUtility.OpenFilePanel(
                "Import Localization Translations",
                Application.dataPath,
                extension);
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            LocalizationSourceRegistry registry = LocalizationSourceRegistry.LoadOrCreate();
            bool success;
            LocalizationTranslationImportReport report;
            string error;
            if (csv)
            {
                success = LocalizationTranslationExchange.TryImportCsv(
                    _workspace,
                    registry,
                    path,
                    out report,
                    out error);
            }
            else
            {
                success = LocalizationTranslationExchange.TryImportJson(
                    _workspace,
                    registry,
                    path,
                    out report,
                    out error);
            }

            if (!success)
            {
                EditorUtility.DisplayDialog("Localization Import", error, "确定");
                return;
            }

            string issueSummary = report.Issues.Count == 0
                ? "无冲突。"
                : $"发现 {report.Issues.Count} 个需处理项（例如 SourceChanged / 缺少目标语言表）。";
            EditorUtility.DisplayDialog(
                "Localization Import",
                $"已写入 {report.AppliedCount} 条译文，跳过 {report.SkippedCount} 条。\n{issueSummary}",
                "确定");
        }

        private static void DrawScanCandidate(LocalizationUiScanCandidate candidate)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                bool selectable = candidate.Status != LocalizationUiScanStatus.Conflict;
                using (new EditorGUI.DisabledScope(!selectable))
                {
                    candidate.Selected = EditorGUILayout.ToggleLeft(
                        $"{candidate.Status}  {candidate.SourceText}",
                        candidate.Selected,
                        EditorStyles.boldLabel);
                }

                EditorGUILayout.LabelField(candidate.Key, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField(candidate.HierarchyPath, EditorStyles.wordWrappedMiniLabel);
                if (!string.IsNullOrWhiteSpace(candidate.Message))
                {
                    MessageType messageType =
                        candidate.Status == LocalizationUiScanStatus.Conflict ||
                        candidate.Status == LocalizationUiScanStatus.DynamicCandidate
                            ? MessageType.Warning
                            : MessageType.None;
                    EditorGUILayout.HelpBox(candidate.Message, messageType);
                }
            }
        }

        private void ScanPrefab()
        {
            LocalizationSourceRegistry registry = LocalizationSourceRegistry.LoadOrCreate();
            if (!LocalizationUiScanner.TryScanPrefab(
                    _scanPrefab,
                    _sourceLocale,
                    registry,
                    out _scanResult,
                    out string error))
            {
                _scanResult = null;
                EditorUtility.DisplayDialog("Localization Scan", error, "确定");
                return;
            }

            if (_sourceTable == null && _catalog != null)
            {
                foreach (LocalizationTableAsset table in _catalog.Tables)
                {
                    if (table != null &&
                        string.Equals(
                            table.Locale,
                            _scanResult.SourceLocale,
                            System.StringComparison.OrdinalIgnoreCase))
                    {
                        _sourceTable = table;
                        break;
                    }
                }
            }
        }

        private void ApplyScan()
        {
            LocalizationSourceRegistry registry = LocalizationSourceRegistry.LoadOrCreate();
            if (!LocalizationUiScanner.TryApply(_scanResult, _sourceTable, registry, out string error))
            {
                EditorUtility.DisplayDialog("Localization Apply", error, "确定");
                return;
            }

            EditorUtility.DisplayDialog(
                "Localization Apply",
                "已更新 Prefab Binding、源语言表与 LocalizationSourceRegistry。",
                "确定");
            ScanPrefab();
        }

        private void SyncCatalogFromSelection()
        {
            if (Selection.activeObject is LocalizationCatalogAsset selectedCatalog)
            {
                _catalog = selectedCatalog;
            }
        }
    }
}
