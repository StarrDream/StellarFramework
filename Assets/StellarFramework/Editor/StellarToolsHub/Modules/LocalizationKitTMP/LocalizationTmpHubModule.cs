using System.Linq;
using StellarFramework.Localization.Editor;
using StellarFramework.Localization.TMP.Editor;
using StellarFramework.Localization.UnityUGUI;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace StellarFramework.Editor.Modules
{
    [StellarTool("Localization TMP", "生产力", -14,
        RequiredAssemblyNames = new[]
        {
            "StellarFramework.LocalizationKit.TMP.Editor",
            "StellarFramework.LocalizationKit.TMPAdapter"
        })]
    public sealed class LocalizationTmpHubModule : ToolModule
    {
        private LocalizationWorkspaceAsset _workspace;
        private GameObject _prefab;
        private LocalizationTableAsset _sourceTable;
        private string _sourceLocale = "zh-CN";
        private LocalizationTmpScanResult _scanResult;
        private Vector2 _scroll;

        public override string Icon => "d_TextMesh Icon";
        public override string Description =>
            "TextMeshPro 可选本地化扫描与稳定 BindingId 绑定；不让 Localization Core/UGUI 强依赖 TMP。";

        public override void OnGUI()
        {
            DrawTmpEnvironmentStatus();
            Section("TMP Scan & Bind");
            _workspace = (LocalizationWorkspaceAsset)EditorGUILayout.ObjectField(
                "Workspace",
                _workspace,
                typeof(LocalizationWorkspaceAsset),
                false);
            if (_workspace != null)
            {
                _sourceLocale = _workspace.SourceLocale;
                if (_workspace.TryGetSourceTable(out LocalizationTableAsset table, out _))
                {
                    _sourceTable = table;
                }
            }

            _prefab = (GameObject)EditorGUILayout.ObjectField(
                "UI Prefab",
                _prefab,
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
                using (new EditorGUI.DisabledScope(_prefab == null))
                {
                    if (PrimaryButton("扫描 TMP Prefab", GUILayout.Height(30)))
                    {
                        Scan();
                    }
                }
                using (new EditorGUI.DisabledScope(_scanResult == null || _sourceTable == null))
                {
                    if (GUILayout.Button("应用选中项", GUILayout.Height(30)))
                    {
                        Apply();
                    }
                }
            }

            if (_scanResult == null)
            {
                return;
            }

            EditorGUILayout.LabelField(
                $"TMP 扫描：{_scanResult.Candidates.Count} 项 | Variant={_scanResult.IsVariant}",
                EditorStyles.miniBoldLabel);
            _scroll = EditorGUILayout.BeginScrollView(
                _scroll,
                GUILayout.MinHeight(180),
                GUILayout.MaxHeight(480));
            foreach (LocalizationTmpScanCandidate candidate in _scanResult.Candidates)
            {
                DrawCandidate(candidate);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawTmpEnvironmentStatus()
        {
            Section("TMP Environment");
            TMP_Settings settings = TMP_Settings.instance;
            if (settings != null)
            {
                EditorGUILayout.HelpBox(
                    "TextMeshPro package 与 TMP Settings 已就绪。StellarFramework 只分发 Adapter/Scanner，不复制 Unity 官方 TMP Essential Resources。",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                "已安装 com.unity.textmeshpro，但当前项目没有可用的 TMP Settings。请使用 Unity 官方入口 " +
                "Window -> TextMeshPro -> Import TMP Essential Resources 完成项目级初始化；这些第三方资源不会被 StellarFramework 导出包复制。",
                MessageType.Warning);
        }

        private static void DrawCandidate(LocalizationTmpScanCandidate candidate)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUI.DisabledScope(candidate.Status == LocalizationUiScanStatus.Conflict))
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
                    EditorGUILayout.HelpBox(
                        candidate.Message,
                        candidate.Status == LocalizationUiScanStatus.Synced
                            ? MessageType.None
                            : MessageType.Warning);
                }
            }
        }

        private void Scan()
        {
            LocalizationSourceRegistry registry = LocalizationSourceRegistry.LoadOrCreate();
            if (!LocalizationTmpUiScanner.TryScanPrefab(
                    _prefab,
                    _sourceLocale,
                    registry,
                    out _scanResult,
                    out string error))
            {
                _scanResult = null;
                EditorUtility.DisplayDialog("TMP Localization Scan", error, "确定");
            }
        }

        private void Apply()
        {
            LocalizationSourceRegistry registry = LocalizationSourceRegistry.LoadOrCreate();
            if (!LocalizationTmpUiScanner.TryApply(
                    _scanResult,
                    _sourceTable,
                    registry,
                    out string error))
            {
                EditorUtility.DisplayDialog("TMP Localization Apply", error, "确定");
                return;
            }

            int applied = _scanResult.Candidates.Count(candidate => candidate.Selected);
            EditorUtility.DisplayDialog(
                "TMP Localization Apply",
                $"已应用 {applied} 个 TMP 本地化绑定。",
                "确定");
            Scan();
        }
    }
}
