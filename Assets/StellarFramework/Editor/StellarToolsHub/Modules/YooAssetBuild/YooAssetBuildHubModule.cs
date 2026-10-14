using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace StellarFramework.Editor.ResourceBuild
{
    [StellarTool("YooAsset 资源构建", "资源管理", 1, RequiredAssemblyNames = new[] { "YooAsset.Editor" })]
    public sealed class YooAssetBuildHubModule : ToolModule
    {
        private static readonly BuildTarget[] Targets =
        {
            BuildTarget.StandaloneWindows64, BuildTarget.Android, BuildTarget.iOS,
            BuildTarget.WebGL, BuildTarget.StandaloneOSX, BuildTarget.StandaloneLinux64
        };
        private static readonly List<string> TargetLabels = new List<string>
        { "Windows（64 位）", "Android", "iOS", "WebGL", "macOS", "Linux（64 位）" };
        private string PrefsKey => "StellarFramework.ResourceBuild." + Application.dataPath;
        private YooAssetResourceBuildRequest _request = new YooAssetResourceBuildRequest();
        private YooAssetResourceBuildResult _result;
        private VisualElement _form;
        private DropdownField _packages;
        private Label _packageSummary;
        private Label _outputPreview;
        private Label _resultSummary;
        private TextField _resultPath;
        private TextField _diagnostics;
        private HelpBox _validation;
        private Button _build;
        private Button _openOutput;
        private bool _queued;
        private bool _wasBusy;

        public override string Description => "选择资源包、平台和版本，构建本地 YooAsset 资源并查看产物。";
        public override string Icon => "d_PreMatCube";

        [MenuItem("Tools/StellarFramework/资源构建/YooAsset")]
        private static void Open() => StellarFrameworkTools.OpenModule<YooAssetBuildHubModule>();

        public override void OnEnable()
        {
            try
            {
                _request = JsonUtility.FromJson<YooAssetResourceBuildRequest>(EditorPrefs.GetString(PrefsKey + ".request", "")) ?? new YooAssetResourceBuildRequest();
                _result = JsonUtility.FromJson<YooAssetResourceBuildResult>(EditorPrefs.GetString(PrefsKey + ".result", ""));
            }
            catch { _request = new YooAssetResourceBuildRequest(); _result = null; }
            EditorApplication.projectChanged += RefreshPackages;
        }

        public override void OnDisable() => EditorApplication.projectChanged -= RefreshPackages;
        public override void OnGUI() { }

        public override VisualElement CreateView()
        {
            var root = new ScrollView();
            root.style.paddingLeft = root.style.paddingRight = 12;
            root.style.paddingBottom = 16;
            root.style.maxWidth = 1120;
            var intro = Card("构建资源包", "使用项目现有的 YooAsset 采集配置。完成后可将资源产物用于 ResKit 的加载或内容更新。");
            root.Add(intro);
            _form = Card("构建设置", "按顺序确认资源包、目标平台和版本。");
            root.Add(_form);
            _packages = new DropdownField("资源包", new List<string> { "（尚无资源包）" }, 0) { name = "resource-package" };
            _packages.RegisterValueChangedCallback(evt => { _request.PackageName = evt.newValue == "（尚无资源包）" ? "" : evt.newValue; Changed(); });
            _form.Add(_packages);
            _packageSummary = Hint("");
            _form.Add(_packageSummary);
            var collectorActions = new VisualElement();
            collectorActions.style.flexDirection = FlexDirection.Row;
            collectorActions.Add(new Button(() => EditorApplication.ExecuteMenuItem("YooAsset/AssetBundle Collector")) { text = "配置采集规则" });
            collectorActions.Add(new Button(RefreshPackages) { text = "刷新资源包" });
            _form.Add(collectorActions);
            int targetIndex = Math.Max(0, Array.IndexOf(Targets, _request.Target));
            _request.Target = Targets[targetIndex];
            var platform = new DropdownField("目标平台", TargetLabels, targetIndex) { name = "resource-platform" };
            platform.RegisterValueChangedCallback(evt => { _request.Target = Targets[TargetLabels.IndexOf(evt.newValue)]; Changed(); });
            _form.Add(platform);
            var version = new TextField("资源版本") { value = _request.Version, name = "resource-version", isDelayed = true };
            version.RegisterValueChangedCallback(evt => { _request.Version = evt.newValue; Changed(); });
            _form.Add(version);
            _form.Add(Hint("版本区分同一资源包的不同内容，例如 1.0.0、1.0.1。已有版本不会被覆盖。"));
            var outputRow = new VisualElement();
            outputRow.style.flexDirection = FlexDirection.Row;
            var output = new TextField("输出目录") { value = _request.OutputRoot, name = "resource-output-root", isDelayed = true };
            output.style.flexGrow = 1;
            output.RegisterValueChangedCallback(evt => { _request.OutputRoot = evt.newValue; Changed(); });
            outputRow.Add(output);
            outputRow.Add(new Button(() =>
            {
                string selected = EditorUtility.OpenFolderPanel("选择资源构建输出目录", Directory.GetParent(Application.dataPath).FullName, "");
                if (!string.IsNullOrEmpty(selected)) output.value = selected;
            }) { text = "选择…" });
            _form.Add(outputRow);
            var advanced = new Foldout { text = "高级设置", value = false };
            var compression = new DropdownField("压缩方式", new List<string> { "LZ4（推荐）", "LZMA", "不压缩" },
                _request.Compression == YooAsset.Editor.ECompressOption.LZMA ? 1 : _request.Compression == YooAsset.Editor.ECompressOption.Uncompressed ? 2 : 0);
            compression.RegisterValueChangedCallback(evt =>
            {
                _request.Compression = evt.newValue == "LZMA" ? YooAsset.Editor.ECompressOption.LZMA :
                    evt.newValue == "不压缩" ? YooAsset.Editor.ECompressOption.Uncompressed : YooAsset.Editor.ECompressOption.LZ4;
                Changed();
            });
            advanced.Add(compression);
            advanced.Add(Hint("采用 YooAsset BuiltinBuildPipeline，保留构建缓存并校验产物。此页面仅生成本地资源，不修改 StreamingAssets。加密、内置资源拷贝和其他管线请使用 YooAsset 原生构建器。"));
            advanced.Add(new Button(() => EditorApplication.ExecuteMenuItem("YooAsset/AssetBundle Builder")) { text = "打开 YooAsset 原生构建器" });
            _form.Add(advanced);
            var actions = Card("确认并构建", "最终目录按平台 / 资源包 / 版本组织。");
            _outputPreview = Hint("");
            _outputPreview.name = "resource-output-preview";
            actions.Add(_outputPreview);
            _validation = new HelpBox("", HelpBoxMessageType.Info);
            actions.Add(_validation);
            _build = new Button(QueueBuild) { text = "开始构建资源", name = "resource-build-button" };
            _build.style.height = 38;
            _build.style.marginTop = 8;
            _build.style.backgroundColor = new Color(0.16f, 0.42f, 0.8f);
            _build.style.color = Color.white;
            _build.style.unityFontStyleAndWeight = FontStyle.Bold;
            actions.Add(_build);
            root.Add(actions);
            var results = Card("上次构建", "结果保存在当前项目的本机编辑器设置中。");
            _resultSummary = Hint("");
            results.Add(_resultSummary);
            _resultPath = new TextField("产物目录") { isReadOnly = true, name = "resource-result-path" };
            results.Add(_resultPath);
            _openOutput = new Button(() => { if (_result != null && Directory.Exists(_result.OutputDirectory)) EditorUtility.RevealInFinder(_result.OutputDirectory); }) { text = "打开产物目录" };
            results.Add(_openOutput);
            _diagnostics = new TextField("错误详情") { isReadOnly = true, multiline = true, name = "resource-build-diagnostics" };
            results.Add(_diagnostics);
            root.Add(results);
            RefreshPackages();
            RefreshResult();
            root.schedule.Execute(() => { if (IsEditorBusy() != _wasBusy) RefreshValidation(); }).Every(1500);
            return root;
        }

        private void Changed()
        {
            EditorPrefs.SetString(PrefsKey + ".request", JsonUtility.ToJson(_request));
            RefreshValidation();
        }

        private void RefreshPackages()
        {
            if (_packages == null || _queued || YooAssetResourceBuilder.IsBuilding) return;
            try
            {
                var setting = YooAssetResourceBuilder.FindCollectorSetting();
                List<string> names = setting?.Packages?.Where(package => package != null).Select(package => package.PackageName).ToList() ?? new List<string>();
                _packages.choices = names.Count > 0 ? names : new List<string> { "（尚无资源包）" };
                if (!names.Contains(_request.PackageName)) _request.PackageName = names.FirstOrDefault() ?? "";
                _packages.SetValueWithoutNotify(names.Count > 0 ? _request.PackageName : "（尚无资源包）");
                var selected = setting?.Packages?.FirstOrDefault(package => package != null && package.PackageName == _request.PackageName);
                int groups = selected?.Groups?.Count ?? 0;
                int collectors = selected?.Groups?.Where(group => group?.Collectors != null).Sum(group => group.Collectors.Count) ?? 0;
                _packageSummary.text = selected == null ? "打开采集规则，创建资源包并添加资源目录后保存。" : $"{groups} 个分组 · {collectors} 个采集项";
            }
            catch (Exception exception) { _packageSummary.text = exception.Message; }
            Changed();
        }

        private void RefreshValidation()
        {
            if (_build == null) return;
            bool busy = IsEditorBusy();
            _wasBusy = busy;
            _form.SetEnabled(!busy);
            string issue = busy ? "正在处理，请等待完成。YooAsset 的构建阶段进度会显示在 Unity 进度窗口。" : YooAssetResourceBuilder.Validate(_request);
            _validation.text = string.IsNullOrEmpty(issue) ? "设置已就绪，可以开始构建。" : issue;
            _validation.messageType = string.IsNullOrEmpty(issue) || busy ? HelpBoxMessageType.Info : HelpBoxMessageType.Warning;
            _build.SetEnabled(!busy && string.IsNullOrEmpty(issue));
            try { _outputPreview.text = YooAssetResourceBuilder.GetOutputDirectory(_request); }
            catch { _outputPreview.text = "填写有效的资源包、版本和输出目录后显示完整路径。"; }
        }

        private bool IsEditorBusy() => _queued || YooAssetResourceBuilder.IsBuilding || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || UnityEditor.BuildPipeline.isBuildingPlayer;

        private void QueueBuild()
        {
            if (_queued || YooAssetResourceBuilder.IsBuilding) return;
            var snapshot = JsonUtility.FromJson<YooAssetResourceBuildRequest>(JsonUtility.ToJson(_request));
            _queued = true;
            _build.text = "正在构建…";
            RefreshValidation();
            EditorApplication.delayCall += () =>
            {
                try
                {
                    _result = YooAssetResourceBuilder.Build(snapshot);
                    EditorPrefs.SetString(PrefsKey + ".result", JsonUtility.ToJson(_result));
                    if (_result.Success) Debug.Log($"[资源构建] 完成：{_result.OutputDirectory}");
                    else Debug.LogError("[资源构建] " + _result.Error);
                }
                finally
                {
                    _queued = false;
                    _build.text = "开始构建资源";
                    RefreshResult();
                    RefreshValidation();
                }
            };
        }

        private void RefreshResult()
        {
            _resultSummary.text = _result == null ? "尚未构建。" : _result.Success ?
                $"构建成功 · {_result.BundleCount} 个 Bundle · {EditorUtility.FormatBytes(_result.TotalBytes)} · {_result.ElapsedSeconds:F1} 秒" : "构建失败，请查看下方错误详情。";
            _resultPath.SetValueWithoutNotify(_result?.OutputDirectory ?? "");
            _openOutput.SetEnabled(_result?.Success == true && Directory.Exists(_result.OutputDirectory));
            _diagnostics.SetValueWithoutNotify(_result?.Error ?? "");
            _diagnostics.style.display = string.IsNullOrEmpty(_result?.Error) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private static VisualElement Card(string title, string description)
        {
            var card = new VisualElement();
            card.style.marginTop = 12;
            card.style.paddingLeft = card.style.paddingRight = 16;
            card.style.paddingTop = card.style.paddingBottom = 14;
            card.style.borderTopLeftRadius = card.style.borderTopRightRadius = card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 6;
            card.style.backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.14f, 0.16f, 0.19f) : new Color(0.91f, 0.92f, 0.94f);
            card.style.borderLeftWidth = 3;
            card.style.borderLeftColor = new Color(0.24f, 0.52f, 0.86f);
            var heading = new Label(title);
            heading.style.fontSize = 15;
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            heading.style.marginBottom = 6;
            card.Add(heading);
            card.Add(Hint(description));
            return card;
        }

        private static Label Hint(string text)
        {
            var label = new Label(text);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginBottom = 8;
            return label;
        }
    }
}
