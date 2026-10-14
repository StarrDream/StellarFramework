using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace StellarFramework.Editor.Modules
{
    public sealed class StellarFrameworkPackageExportWindow : EditorWindow
    {
        [SerializeField] private List<string> _selectedProfileIds = new List<string>();
        [SerializeField] private List<string> _expandedGroups = new List<string> { "ResKit" };
        [SerializeField] private string _search = string.Empty;
        [SerializeField] private bool _includeDocumentation = true;
        private StellarFrameworkPackagePublisher.DistributionProfile[] _profiles;
        private StellarFrameworkPackagePublisher.RecommendedProfile[] _presets;
        private readonly Dictionary<string, Toggle> _toggles = new Dictionary<string, Toggle>();
        private StellarFrameworkPackagePublisher.ExportSelectionPreview _preview;
        private ScrollView _profileList;
        private ScrollView _previewList;
        private Button _exportButton;

        [MenuItem("StellarFramework/Export")]
        private static void Open()
        {
            var window = GetWindow<StellarFrameworkPackageExportWindow>("StellarFramework 导出");
            window.minSize = new Vector2(860, 580);
            window.Show();
        }

        [MenuItem("StellarFramework/Export", true)]
        private static bool ValidateOpen() => StellarFrameworkPackagePublisher.IsFrameworkSourceProject();

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 14;
            rootVisualElement.style.paddingRight = 14;
            rootVisualElement.style.paddingTop = 14;
            rootVisualElement.style.paddingBottom = 14;
            if (!ValidateOpen())
            {
                rootVisualElement.Add(new HelpBox("此项目没有安装框架导出源码。", HelpBoxMessageType.Info));
                return;
            }
            _profiles = StellarFrameworkPackagePublisher.GetSourceProjectExportProfiles();
            _presets = StellarFrameworkPackagePublisher.GetRecommendedProfiles();
            _selectedProfileIds.RemoveAll(id => !_profiles.Any(profile => profile.id == id));
            rootVisualElement.Add(Heading("按需导出 Kit"));
            rootVisualElement.Add(new Label("勾选需要的功能，必需依赖会自动补齐。不同 Kit 和资源加载方式可以自由组合。"));
            BuildPresetBar();

            var panes = new TwoPaneSplitView(1, 320, TwoPaneSplitViewOrientation.Horizontal);
            panes.style.flexGrow = 1;
            panes.style.marginTop = 10;
            var picker = new VisualElement();
            picker.style.flexGrow = 1;
            picker.style.paddingRight = 12;
            var search = new ToolbarSearchField { value = _search };
            search.tooltip = "搜索 Kit 名称或功能，例如 Resources、热更、本地化。";
            search.RegisterValueChangedCallback(evt => { _search = evt.newValue; BuildProfileList(); });
            picker.Add(search);
            _profileList = new ScrollView();
            _profileList.style.flexGrow = 1;
            picker.Add(_profileList);
            panes.Add(picker);

            var summary = new VisualElement();
            summary.style.paddingLeft = 12;
            summary.Add(Heading("导出清单"));
            _previewList = new ScrollView();
            _previewList.style.flexGrow = 1;
            summary.Add(_previewList);
            var docs = new Toggle("附带所选 Kit 的使用文档") { value = _includeDocumentation };
            docs.RegisterValueChangedCallback(evt => { _includeDocumentation = evt.newValue; RefreshPreview(); });
            summary.Add(docs);
            summary.Add(new Button(() => { _selectedProfileIds.Clear(); RefreshPreview(); }) { text = "清空选择" });
            _exportButton = new Button(ExportSelection) { text = "选择保存位置并导出…" };
            _exportButton.style.height = 36;
            _exportButton.style.marginTop = 8;
            summary.Add(_exportButton);
            panes.Add(summary);
            rootVisualElement.Add(panes);
            BuildSourceActions();
            BuildProfileList();
            RefreshPreview();
        }

        private void BuildPresetBar()
        {
            var bar = new VisualElement();
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.marginTop = 10;
            var available = _presets.Where(preset => preset.profileIds.All(id => _profiles.Any(p => p.id == id))).ToArray();
            var choices = new List<string> { "选择快捷方案…" };
            choices.AddRange(available.Select(preset => preset.displayName.Replace(" Complete", " 完整功能")
                .Replace("Hot Update Full", "资源与代码热更")));
            var presets = new PopupField<string>("快捷方案", choices, 0);
            presets.style.flexGrow = 1;
            bar.Add(presets);
            void Apply(bool append)
            {
                if (presets.index <= 0) return;
                if (!append) _selectedProfileIds.Clear();
                foreach (string id in available[presets.index - 1].profileIds)
                    if (!_selectedProfileIds.Contains(id)) _selectedProfileIds.Add(id);
                RefreshPreview();
            }
            bar.Add(new Button(() => Apply(false)) { text = "使用方案" });
            bar.Add(new Button(() => Apply(true)) { text = "加入当前选择" });
            bar.Add(new Button(CreateGUI) { text = "刷新" });
            rootVisualElement.Add(bar);
        }

        private void BuildProfileList()
        {
            _profileList.Clear();
            _toggles.Clear();
            foreach (var group in _profiles.Where(MatchesSearch).GroupBy(GetKitGroup)
                         .OrderBy(group => group.Key == "ResKit" ? 0 : 1).ThenBy(group => group.Key))
            {
                var foldout = new Foldout { text = group.Key, value = !string.IsNullOrWhiteSpace(_search) || _expandedGroups.Contains(group.Key) };
                foldout.RegisterValueChangedCallback(evt =>
                {
                    if (evt.target != foldout || !string.IsNullOrEmpty(_search)) return;
                    if (evt.newValue && !_expandedGroups.Contains(group.Key)) _expandedGroups.Add(group.Key);
                    if (!evt.newValue) _expandedGroups.Remove(group.Key);
                });
                foldout.style.marginTop = 8;
                foreach (var profile in group.Where(profile => profile.kind != "tooling")) AddProfileToggle(foldout, profile);
                var tools = group.Where(profile => profile.kind == "tooling").ToArray();
                if (tools.Length > 0)
                {
                    var tooling = new Foldout { text = "可选编辑器工具", value = tools.Any(p => _selectedProfileIds.Contains(p.id)) || !string.IsNullOrEmpty(_search) };
                    foreach (var profile in tools) AddProfileToggle(tooling, profile);
                    foldout.Add(tooling);
                }
                _profileList.Add(foldout);
            }
            UpdateToggles();
        }

        private void AddProfileToggle(VisualElement parent, StellarFrameworkPackagePublisher.DistributionProfile profile)
        {
            var toggle = new Toggle(GetCapabilityLabel(profile));
            toggle.tooltip = profile.id + "\n" + string.Join("\n", profile.sourcePaths ?? Array.Empty<string>());
            toggle.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue && !_selectedProfileIds.Contains(profile.id)) _selectedProfileIds.Add(profile.id);
                if (!evt.newValue) _selectedProfileIds.Remove(profile.id);
                RefreshPreview();
            });
            toggle.style.marginTop = 4;
            _toggles[profile.id] = toggle;
            parent.Add(toggle);
        }

        private void RefreshPreview()
        {
            _previewList.Clear();
            try
            {
                _preview = StellarFrameworkPackagePublisher.PreviewExportSelection(_selectedProfileIds, _includeDocumentation);
                if (_selectedProfileIds.Count == 0)
                    _previewList.Add(new HelpBox("从左侧勾选功能，或先选择一个快捷方案。", HelpBoxMessageType.Info));
                else
                {
                    AddPreviewProfiles("你选择的功能", _preview.selectedProfiles, true);
                    AddPreviewProfiles("自动补齐的必需依赖", _preview.dependencies);
                    _previewList.Add(Heading("需要安装的第三方包"));
                    foreach (string package in _preview.requiredUpm) _previewList.Add(WrappedLabel(PackageLabel(package)));
                    if (_preview.requiredUpm.Length == 0) _previewList.Add(new Label("无"));
                    _previewList.Add(WrappedLabel($"导出 {_preview.assetCount} 个文件。安装包会先安装上述第三方包，再导入源码。"));
                }
            }
            catch (Exception exception)
            {
                _preview = null;
                _previewList.Add(new HelpBox(exception.Message, HelpBoxMessageType.Error));
            }
            UpdateToggles();
            _exportButton.SetEnabled(_preview != null && _selectedProfileIds.Count > 0);
        }

        private void AddPreviewProfiles(string title, IEnumerable<StellarFrameworkPackagePublisher.DistributionProfile> profiles,
            bool removable = false)
        {
            _previewList.Add(Heading(title));
            var entries = profiles.ToArray();
            foreach (var profile in entries)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                var label = WrappedLabel("• " + GetCapabilityLabel(profile));
                label.style.flexGrow = 1;
                row.Add(label);
                if (removable)
                    row.Add(new Button(() => { _selectedProfileIds.Remove(profile.id); RefreshPreview(); }) { text = "移除" });
                _previewList.Add(row);
            }
            if (entries.Length == 0) _previewList.Add(new Label("无"));
        }

        private void UpdateToggles()
        {
            var dependencies = new HashSet<string>(_preview?.dependencies.Select(p => p.id) ?? Enumerable.Empty<string>());
            foreach (var pair in _toggles)
            {
                bool automatic = dependencies.Contains(pair.Key);
                pair.Value.SetValueWithoutNotify(automatic || _selectedProfileIds.Contains(pair.Key));
                pair.Value.SetEnabled(!automatic);
                var profile = _profiles.First(p => p.id == pair.Key);
                pair.Value.label = GetCapabilityLabel(profile) + (automatic ? "（必需依赖）" : "");
            }
        }

        private void ExportSelection()
        {
            string path = EditorUtility.SaveFilePanel("保存 Kit 安装包", Path.GetFullPath("BuildArtifacts/StellarFramework/Kits"),
                "StellarFramework-Custom", "unitypackage");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                string output = StellarFrameworkPackagePublisher.ExportKitPackageGroupToDirectoryInternal(_selectedProfileIds,
                    Path.GetFileName(path), Path.GetDirectoryName(path), _includeDocumentation);
                EditorUtility.RevealInFinder(output);
                ShowNotification(new GUIContent("导出完成"));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("导出失败", exception.Message, "确定");
            }
        }

        private void BuildSourceActions()
        {
            var sources = new Foldout { text = "单文件源码与完整框架安装包", value = false };
            sources.style.marginTop = 10;
            sources.Add(new Button(StellarFrameworkPackagePublisher.ExportStandaloneArchitecture) { text = "导出 Architecture.cs（无 Kit 依赖）" });
            sources.Add(new Button(StellarFrameworkPackagePublisher.ExportStandaloneExtensions) { text = "导出 Extensions.cs" });
            sources.Add(new Button(StellarFrameworkPackagePublisher.ExportSinglePackageInstaller) { text = "导出完整框架安装包" });
            rootVisualElement.Add(sources);
        }

        private bool MatchesSearch(StellarFrameworkPackagePublisher.DistributionProfile profile)
        {
            return string.IsNullOrWhiteSpace(_search) ||
                   (GetKitGroup(profile) + " " + GetCapabilityLabel(profile) + " " + profile.id)
                   .IndexOf(_search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetKitGroup(StellarFrameworkPackagePublisher.DistributionProfile profile)
        {
            if (profile.id.StartsWith("reskit.")) return "ResKit";
            if (profile.id.StartsWith("runtime.")) return "框架基础";
            if (profile.id == "worldframework.tools") return "WorldKit";
            return profile.displayName.Split('.')[0];
        }

        private static string GetCapabilityLabel(StellarFrameworkPackagePublisher.DistributionProfile profile)
        {
            switch (profile.id)
            {
                case "reskit.core": return "资源加载核心";
                case "reskit.resources": return "Resources 资源加载";
                case "reskit.assetbundle": return "AssetBundle 资源加载";
                case "reskit.addressables": return "Addressables 资源加载";
                case "reskit.yooasset": return "YooAsset 资源加载";
                case "reskit.contentupdate.yooasset": return "资源热更（YooAsset）";
                case "reskit.codeupdate.hybridclr": return "代码热更（HybridCLR）";
                case "reskit.tools": return "资源工具与路径常量生成";
                case "reskit.assetbundle.tools": return "AssetBundle 构建工具";
                case "reskit.yooasset.tools": return "YooAsset 资源构建工具";
                case "reskit.codeupdate.hybridclr.tools": return "HybridCLR 代码导出工具";
                case "runtime.core": return "MSV 架构与框架基础工具";
                case "toolshub.core": return "ToolsHub 编辑器工具中心";
                case "httpkit": return "HttpKit 请求与图片下载";
                case "httpkit.ugui": return "UGUI 图片绑定（Image / RawImage）";
                default: return profile.displayName.Replace(".Core", " 核心").Replace(".Tools", " 编辑器工具").Replace("Adapter", " 适配层");
            }
        }

        private static string PackageLabel(string package)
        {
            switch (package)
            {
                case "com.cysharp.unitask": return "UniTask（异步任务）";
                case "com.unity.nuget.newtonsoft-json": return "Newtonsoft.Json（JSON 序列化）";
                case "com.unity.addressables": return "Addressables（资源加载）";
                case "com.tuyoogame.yooasset": return "YooAsset（资源加载与内容更新）";
                case "com.code-philosophy.hybridclr": return "HybridCLR（代码热更）";
                case "com.unity.ugui": return "UGUI（Image / RawImage 界面）";
                case "com.unity.textmeshpro": return "TextMeshPro（文本组件）";
                case "com.unity.ext.nunit": return "NUnit（当前 YooAsset / Addressables 的编辑器兼容依赖）";
                default: return package;
            }
        }

        private static Label Heading(string text)
        {
            var label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginTop = 10;
            label.style.marginBottom = 6;
            return label;
        }

        private static Label WrappedLabel(string text)
        {
            var label = new Label(text);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginBottom = 6;
            return label;
        }
    }
}
