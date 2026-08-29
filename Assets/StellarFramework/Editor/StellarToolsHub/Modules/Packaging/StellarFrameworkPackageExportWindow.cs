using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace StellarFramework.Editor.Modules
{
    /// <summary>
    /// Source-project-only exporter. This window lives in the Packaging directory, which is excluded from all consumer
    /// distribution profiles, so a business project never receives this authoring tool.
    /// </summary>
    internal sealed class StellarFrameworkPackageExportWindow : EditorWindow
    {
        private const int KitTab = 0;
        private const int SampleTab = 1;
        private const int StandaloneTab = 2;

        private readonly HashSet<string> _selectedProfileIds = new HashSet<string>(StringComparer.Ordinal);
        private Vector2 _scrollPosition;
        private string _outputFileName = "StellarFramework-CombinedKits.unitypackage";
        private StellarFrameworkPackagePublisher.DistributionProfile[] _profiles = Array.Empty<StellarFrameworkPackagePublisher.DistributionProfile>();
        private int _activeTab;

        [MenuItem("StellarFramework/Framework Source/Kit Package Exporter")]
        private static void Open()
        {
            StellarFrameworkPackageExportWindow window = GetWindow<StellarFrameworkPackageExportWindow>("Kit Package Exporter");
            window.minSize = new Vector2(720f, 640f);
            window.Show();
        }

        [MenuItem("StellarFramework/Framework Source/Kit Package Exporter", true)]
        private static bool ValidateOpen()
        {
            return StellarFrameworkPackagePublisher.IsFrameworkSourceProject();
        }

        private void OnEnable()
        {
            ReloadProfiles();
        }

        private void OnGUI()
        {
            if (!StellarFrameworkPackagePublisher.IsFrameworkSourceProject())
            {
                EditorGUILayout.HelpBox("此窗口只在 StellarFramework 原始框架工程中可用，不会随导出包进入业务项目。", MessageType.Info);
                return;
            }

            DrawHeader();
            EditorGUI.BeginChangeCheck();
            _activeTab = GUILayout.Toolbar(_activeTab, new[] { "Kit 组合包", "样例包", "独立文件" });
            if (EditorGUI.EndChangeCheck())
            {
                ReloadProfiles();
            }

            EditorGUILayout.Space(6f);
            switch (_activeTab)
            {
                case KitTab:
                    DrawProfileExportPage("Kit", "选择一个或多个 Kit；所有直接与间接依赖会自动合并、去重。\n" +
                        "“独立 Kit”不额外带入 StellarFramework Kit；“有依赖 / 适配器”会在下方明确列出自动包含项。");
                    break;
                case SampleTab:
                    DrawProfileExportPage("样例", "样例包会自动包含其演示所需的 Kit；这不会把热更或 HybridCLR 加入其他样例，除非你明确选择对应样例。");
                    break;
                default:
                    DrawStandalonePage();
                    break;
            }
        }

        private void ReloadProfiles()
        {
            _profiles = _activeTab == SampleTab
                ? StellarFrameworkPackagePublisher.GetSourceProjectSampleProfiles()
                : StellarFrameworkPackagePublisher.GetSourceProjectExportProfiles();
            _selectedProfileIds.RemoveWhere(profileId => _profiles.All(profile => profile.id != profileId));
            Repaint();
        }

        private static void DrawHeader()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("StellarFramework Kit Package Exporter", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("原框架工程专用 · 导出工具不会进入业务项目", EditorStyles.miniLabel);
                EditorGUILayout.HelpBox(
                    "用一个窗口完成 Kit、样例和独立源码导出。导出的 .unitypackage 会附带同名依赖说明文件。",
                    MessageType.Info);
            }
        }

        private void DrawProfileExportPage(string profileLabel, string introduction)
        {
            EditorGUILayout.HelpBox(introduction, MessageType.None);
            using (new EditorGUILayout.HorizontalScope())
            {
                _outputFileName = EditorGUILayout.TextField("组合包文件名", _outputFileName);
                if (GUILayout.Button("刷新列表", GUILayout.Width(74f)))
                {
                    ReloadProfiles();
                }
            }

            EditorGUILayout.Space(4f);
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            if (_activeTab == KitTab)
            {
                DrawProfileSection("独立 Kit", "不会额外带入其他 StellarFramework Kit。", false);
                DrawProfileSection("有依赖 / 适配器", "导出时会自动包含下列依赖。", true);
            }
            else
            {
                DrawProfileSection("可选样例", "导出时会自动带入对应 Kit 与必要依赖。", null);
            }

            EditorGUILayout.EndScrollView();
            DrawExportFooter(profileLabel);
        }

        private void DrawProfileSection(string title, string description, bool? hasDependencies)
        {
            StellarFrameworkPackagePublisher.DistributionProfile[] sectionProfiles = _profiles
                .Where(profile => !hasDependencies.HasValue || HasProfileDependencies(profile) == hasDependencies.Value)
                .ToArray();
            if (sectionProfiles.Length == 0)
            {
                return;
            }

            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(description, EditorStyles.miniLabel);
            foreach (StellarFrameworkPackagePublisher.DistributionProfile profile in sectionProfiles)
            {
                DrawProfileCard(profile);
            }

            EditorGUILayout.Space(6f);
        }

        private void DrawProfileCard(StellarFrameworkPackagePublisher.DistributionProfile profile)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                bool selected = _selectedProfileIds.Contains(profile.id);
                bool nextSelected = EditorGUILayout.ToggleLeft(
                    $"{profile.displayName}    {GetProfileBadge(profile)}", selected, EditorStyles.boldLabel);
                if (nextSelected != selected)
                {
                    if (nextSelected)
                    {
                        _selectedProfileIds.Add(profile.id);
                    }
                    else
                    {
                        _selectedProfileIds.Remove(profile.id);
                    }
                }

                EditorGUILayout.LabelField(profile.id, EditorStyles.miniLabel);
                string[] requiredProfiles = profile.requiredProfileIds ?? Array.Empty<string>();
                EditorGUILayout.LabelField(requiredProfiles.Length == 0
                    ? "自动包含：无其他 StellarFramework Kit"
                    : "自动包含：" + string.Join("、", requiredProfiles), EditorStyles.wordWrappedMiniLabel);

                string[] requiredUpm = profile.requiredUpm ?? Array.Empty<string>();
                if (requiredUpm.Length > 0)
                {
                    EditorGUILayout.LabelField("额外 UPM：" + string.Join("、", requiredUpm),
                        EditorStyles.wordWrappedMiniLabel);
                }
            }
        }

        private void DrawExportFooter(string profileLabel)
        {
            string[] selectedIds = _selectedProfileIds.OrderBy(profileId => profileId).ToArray();
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField($"已选择 {selectedIds.Length} 个 {profileLabel}", EditorStyles.miniBoldLabel);
            DrawDependencyPreview(selectedIds);

            using (new EditorGUI.DisabledScope(selectedIds.Length == 0))
            {
                if (GUILayout.Button($"导出所选 {profileLabel}（自动合并依赖）", GUILayout.Height(34f)))
                {
                    ExportSelectedProfiles(selectedIds);
                }
            }
        }

        private static bool HasProfileDependencies(StellarFrameworkPackagePublisher.DistributionProfile profile)
        {
            return profile.requiredProfileIds != null && profile.requiredProfileIds.Length > 0;
        }

        private static string GetProfileBadge(StellarFrameworkPackagePublisher.DistributionProfile profile)
        {
            return HasProfileDependencies(profile) ? "[ 自动带依赖 ]" : "[ 独立 ]";
        }

        private static void DrawStandalonePage()
        {
            EditorGUILayout.HelpBox(
                "下面两个导出不会包含任何 StellarFramework Kit，也不会携带 ToolsHub、ResKit、热更或 HybridCLR。\n" +
                "LogKit 调用会替换为 UnityEngine.Debug，适合只想复制一个 .cs 文件的项目。",
                MessageType.Info);

            DrawStandaloneCard(
                "Architecture.cs",
                "框架的架构基类单文件版。适合只使用 Architecture 设计模式，不引入其他 Kit。",
                "导出 Architecture.cs",
                StellarFrameworkPackagePublisher.ExportStandaloneArchitecture);
            DrawStandaloneCard(
                "Extensions.cs",
                "全部静态扩展的单文件版。适合只需要集合、字符串、Transform、颜色等扩展方法。",
                "导出 Extensions.cs",
                StellarFrameworkPackagePublisher.ExportStandaloneExtensions);

            EditorGUILayout.Space(4f);
            if (GUILayout.Button("同时导出 Architecture.cs + Extensions.cs", GUILayout.Height(34f)))
            {
                StellarFrameworkPackagePublisher.ExportAllStandaloneSources();
            }

            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField("完整框架", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "需要完整框架时可导出单包安装版。该包包含完整能力与安装器，和按需 Kit 导出是两种不同的交付方式。",
                MessageType.None);
            if (GUILayout.Button("导出完整单包安装版", GUILayout.Height(30f)))
            {
                StellarFrameworkPackagePublisher.ExportSinglePackageInstaller();
            }
        }

        private static void DrawStandaloneCard(string title, string description, string buttonLabel, Action exportAction)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(title + "    [ 无 Kit 依赖 ]", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
                if (GUILayout.Button(buttonLabel, GUILayout.Height(28f)))
                {
                    exportAction();
                }
            }
        }

        private static void DrawDependencyPreview(IEnumerable<string> selectedIds)
        {
            try
            {
                string[] closureIds = selectedIds
                    .SelectMany(StellarFrameworkPackagePublisher.ResolveKitProfileClosureIds)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(profileId => profileId)
                    .ToArray();
                EditorGUILayout.HelpBox(
                    $"导出内容：{closureIds.Length} 个去重 Profile\n{string.Join("、", closureIds)}",
                    MessageType.None);
            }
            catch (Exception exception)
            {
                EditorGUILayout.HelpBox(exception.Message, MessageType.Error);
            }
        }

        private void ExportSelectedProfiles(IEnumerable<string> selectedIds)
        {
            try
            {
                string outputPath = StellarFrameworkPackagePublisher.ExportKitPackageGroupInternal(
                    selectedIds, _outputFileName);
                EditorUtility.RevealInFinder(outputPath);
                ShowNotification(new GUIContent("组合 Kit 导出完成"));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Kit 导出失败", exception.Message, "确定");
            }
        }
    }
}
