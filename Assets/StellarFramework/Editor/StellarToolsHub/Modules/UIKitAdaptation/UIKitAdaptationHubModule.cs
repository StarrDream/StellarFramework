using System;
using System.Collections.Generic;
using System.Linq;
using StellarFramework.UI.Adaptation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace StellarFramework.Editor.Modules
{
    [StellarTool("UIKit UI适配", "框架核心", 5,
        RequiredAssemblyNames = new[]
        {
            "StellarFramework.UIKit.Adaptation",
            "StellarFramework.UIKit"
        })]
    public sealed class UIKitAdaptationHubModule : ToolModule
    {
        private UIAdaptationProfile _profile;
        private GameObject _targetRoot;
        private int _previewWidth = 1920;
        private int _previewHeight = 1080;
        private Vector4 _safeInsets;
        private RectTransform _variantRoot;
        private string _variantBreakpointId = "default";
        private Vector2 _validationScroll;
        private readonly List<string> _validationIssues = new List<string>();

        public override string Icon => "d_RectTransformBlueprint";
        public override string Description =>
            "UIKit 多尺寸适配：Safe Area、Aspect Breakpoint、屏幕预览与布局风险检查。";

        public override void OnSelectionChange()
        {
            if (Selection.activeGameObject != null)
            {
                _targetRoot = Selection.activeGameObject;
            }
        }

        public override void OnGUI()
        {
            DrawProfileSection();
            DrawPreviewSection();
            DrawControllerSection();
            DrawLayoutVariantSection();
            DrawValidationSection();
        }

        private void DrawProfileSection()
        {
            Section("Adaptation Profile");
            _profile = (UIAdaptationProfile)EditorGUILayout.ObjectField(
                "Profile",
                _profile,
                typeof(UIAdaptationProfile),
                false);

            if (_profile == null)
            {
                EditorGUILayout.HelpBox(
                    "创建 UIAdaptationProfile 后可配置设计分辨率、默认 Match、Safe Area 与 Aspect Breakpoints。",
                    MessageType.Info);
                if (PrimaryButton("创建推荐 Adaptation Profile", GUILayout.Height(28)))
                {
                    CreateRecommendedProfile();
                }
                return;
            }

            EditorGUILayout.LabelField("Design Resolution", _profile.DesignResolution.ToString());
            EditorGUILayout.LabelField(
                "Default Match",
                _profile.DefaultMatchWidthOrHeight.ToString("0.00"));
            EditorGUILayout.LabelField(
                "Safe Area",
                _profile.ApplySafeArea ? "Enabled" : "Disabled");
            EditorGUILayout.LabelField("Breakpoints", _profile.Breakpoints.Count.ToString());
        }

        private void DrawPreviewSection()
        {
            Section("Device Preview");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("16:9"))
                {
                    SetPreview(1920, 1080);
                }
                if (GUILayout.Button("20:9"))
                {
                    SetPreview(2400, 1080);
                }
                if (GUILayout.Button("4:3"))
                {
                    SetPreview(2048, 1536);
                }
                if (GUILayout.Button("19.5:9 Portrait"))
                {
                    SetPreview(1170, 2532);
                }
            }

            _previewWidth = Mathf.Max(1, EditorGUILayout.IntField("Width", _previewWidth));
            _previewHeight = Mathf.Max(1, EditorGUILayout.IntField("Height", _previewHeight));
            _safeInsets = EditorGUILayout.Vector4Field(
                new GUIContent("Safe Insets L/B/R/T"),
                _safeInsets);

            if (_profile == null)
            {
                return;
            }

            UIAdaptationBreakpoint breakpoint =
                _profile.ResolveBreakpoint(_previewWidth, _previewHeight, out float match);
            string breakpointName = breakpoint == null ? "(Default)" : breakpoint.Id;
            float shapeAspect = UIAdaptationProfile.CalculateShapeAspect(
                _previewWidth,
                _previewHeight);
            UIAdaptationOrientation orientation = UIAdaptationProfile.ResolveOrientation(
                _previewWidth,
                _previewHeight);
            EditorGUILayout.HelpBox(
                $"Shape Aspect={shapeAspect:0.###}  Orientation={orientation}  " +
                $"Breakpoint={breakpointName}  Match={match:0.00}",
                MessageType.Info);

            Rect safeArea = BuildPreviewSafeArea();
            Vector2 anchorMin = new Vector2(
                safeArea.xMin / _previewWidth,
                safeArea.yMin / _previewHeight);
            Vector2 anchorMax = new Vector2(
                safeArea.xMax / _previewWidth,
                safeArea.yMax / _previewHeight);
            EditorGUILayout.LabelField(
                "SafeArea Anchors",
                $"{anchorMin.x:0.###},{anchorMin.y:0.###} -> {anchorMax.x:0.###},{anchorMax.y:0.###}");
        }

        private void DrawControllerSection()
        {
            Section("UIRoot");
            _targetRoot = (GameObject)EditorGUILayout.ObjectField(
                "Target Root",
                _targetRoot,
                typeof(GameObject),
                true);

            if (_targetRoot == null)
            {
                return;
            }

            UIAdaptationController controller = _targetRoot.GetComponent<UIAdaptationController>();
            CanvasScaler scaler = _targetRoot.GetComponent<CanvasScaler>();
            RectTransform[] safeAreaRoots = FindSafeAreaRoots(_targetRoot.transform);

            EditorGUILayout.LabelField("CanvasScaler", scaler == null ? "Missing" : "OK");
            EditorGUILayout.LabelField(
                "Adaptation Controller",
                controller == null ? "Missing" : "OK");
            EditorGUILayout.LabelField(
                "SafeArea Roots",
                safeAreaRoots.Length == 0
                    ? "Missing"
                    : string.Join(", ", safeAreaRoots.Select(root => BuildPath(_targetRoot.transform, root))));

            using (new EditorGUI.DisabledScope(_profile == null || scaler == null))
            {
                if (PrimaryButton(
                        controller == null ? "添加并配置 Adaptation Controller" : "更新 Controller 配置",
                        GUILayout.Height(30)))
                {
                    if (controller == null)
                    {
                        controller = Undo.AddComponent<UIAdaptationController>(_targetRoot);
                    }
                    else
                    {
                        Undo.RecordObject(controller, "Configure UI Adaptation Controller");
                    }
                    controller.ConfigureSafeAreaRoots(_profile, safeAreaRoots);
                    EditorUtility.SetDirty(controller);
                }

                if (GUILayout.Button("应用当前 Preview 到选中 UIRoot", GUILayout.Height(26)))
                {
                    if (controller == null)
                    {
                        controller = Undo.AddComponent<UIAdaptationController>(_targetRoot);
                        controller.ConfigureSafeAreaRoots(_profile, safeAreaRoots);
                    }
                    Undo.RecordObject(scaler, "Preview UI Adaptation");
                    foreach (RectTransform safeAreaRoot in safeAreaRoots)
                    {
                        Undo.RecordObject(safeAreaRoot, "Preview Safe Area");
                    }
                    controller.Apply(_previewWidth, _previewHeight, BuildPreviewSafeArea());
                    foreach (UILayoutVariant variant in
                             _targetRoot.GetComponentsInChildren<UILayoutVariant>(true))
                    {
                        Undo.RecordObject(variant, "Preview Layout Variant");
                        variant.ApplyVariant(controller.CurrentBreakpointId);
                    }
                    EditorUtility.SetDirty(controller);
                }
            }
        }

        private void DrawLayoutVariantSection()
        {
            Section("Layout Variant");
            EditorGUILayout.HelpBox(
                "Breakpoint 只决定何时切换；具体 Tablet / Tall / Landscape 布局由设计师调整后 Capture。Runtime 仅在 Breakpoint 变化时应用一次快照。",
                MessageType.Info);

            _variantRoot = (RectTransform)EditorGUILayout.ObjectField(
                "Variant Root",
                _variantRoot,
                typeof(RectTransform),
                true);
            _variantBreakpointId = EditorGUILayout.TextField(
                "Breakpoint ID",
                _variantBreakpointId);

            if (_variantRoot == null && _targetRoot != null)
            {
                _variantRoot = _targetRoot.GetComponent<RectTransform>();
            }

            using (new EditorGUI.DisabledScope(_variantRoot == null || _targetRoot == null))
            {
                if (PrimaryButton("Capture 当前布局", GUILayout.Height(30)))
                {
                    CaptureLayoutVariant();
                }
            }

            if (_variantRoot == null)
            {
                return;
            }

            UILayoutVariant layout = _variantRoot.GetComponent<UILayoutVariant>();
            if (layout == null)
            {
                EditorGUILayout.LabelField("Variant Component", "Missing");
                return;
            }

            EditorGUILayout.LabelField(
                "Captured Variants",
                layout.Variants.Count == 0
                    ? "(None)"
                    : string.Join(", ", layout.Variants
                        .Where(item => item != null)
                        .Select(item => item.BreakpointId)));
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_variantBreakpointId)))
            {
                if (GUILayout.Button("应用该 Variant 预览", GUILayout.Height(26)))
                {
                    Undo.RegisterFullObjectHierarchyUndo(
                        _variantRoot.gameObject,
                        "Preview Layout Variant");
                    if (!layout.ApplyVariant(_variantBreakpointId))
                    {
                        EditorUtility.DisplayDialog(
                            "UIKit Layout Variant",
                            $"未找到 Breakpoint '{_variantBreakpointId}' 的布局快照。",
                            "确定");
                    }
                }
            }
        }

        private void CaptureLayoutVariant()
        {
            UIAdaptationController controller =
                _targetRoot.GetComponent<UIAdaptationController>();
            UILayoutVariant layout = _variantRoot.GetComponent<UILayoutVariant>();
            if (layout == null)
            {
                layout = Undo.AddComponent<UILayoutVariant>(_variantRoot.gameObject);
            }
            else
            {
                Undo.RecordObject(layout, "Capture Layout Variant");
            }

            UILayoutTargetState[] states =
                layout.CaptureCurrentHierarchy(_variantRoot, true);
            layout.Configure(controller, layout.Variants
                .Where(item => item != null)
                .ToArray());
            layout.SetVariant(_variantBreakpointId, states);
            EditorUtility.SetDirty(layout);
        }

        private void DrawValidationSection()
        {
            Section("Validator");
            using (new EditorGUI.DisabledScope(_targetRoot == null || _profile == null))
            {
                if (GUILayout.Button("检查当前 UIRoot / Panel", GUILayout.Height(28)))
                {
                    RunValidation();
                }
            }

            if (_validationIssues.Count == 0)
            {
                EditorGUILayout.HelpBox("暂无检查结果或未发现明显适配风险。", MessageType.Info);
                return;
            }

            _validationScroll = EditorGUILayout.BeginScrollView(
                _validationScroll,
                GUILayout.MinHeight(120),
                GUILayout.MaxHeight(320));
            foreach (string issue in _validationIssues)
            {
                EditorGUILayout.HelpBox(issue, MessageType.Warning);
            }
            EditorGUILayout.EndScrollView();
        }

        private void RunValidation()
        {
            _validationIssues.Clear();
            if (_targetRoot == null || _profile == null)
            {
                return;
            }

            CanvasScaler scaler = _targetRoot.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                _validationIssues.Add("UIRoot 缺少 CanvasScaler。");
            }
            else if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                _validationIssues.Add("CanvasScaler 不是 ScaleWithScreenSize，无法按 Profile 进行一致适配。");
            }

            RectTransform[] safeAreaRoots = FindSafeAreaRoots(_targetRoot.transform);
            if (_profile.ApplySafeArea && safeAreaRoots.Length == 0)
            {
                _validationIssues.Add(
                    "Profile 启用了 Safe Area，但当前层级没有名为 SafeAreaRoot 的 RectTransform。");
            }

            Vector2 design = _profile.DesignResolution;
            foreach (RectTransform rect in _targetRoot.GetComponentsInChildren<RectTransform>(true))
            {
                if (rect == _targetRoot.transform || safeAreaRoots.Contains(rect))
                {
                    continue;
                }

                bool fixedCenterAnchor =
                    Vector2.Distance(rect.anchorMin, new Vector2(0.5f, 0.5f)) < 0.001f &&
                    Vector2.Distance(rect.anchorMax, new Vector2(0.5f, 0.5f)) < 0.001f;
                if (!fixedCenterAnchor)
                {
                    continue;
                }

                float xRisk = Mathf.Abs(rect.anchoredPosition.x) / Mathf.Max(1f, design.x);
                float yRisk = Mathf.Abs(rect.anchoredPosition.y) / Mathf.Max(1f, design.y);
                if (xRisk > 0.30f || yRisk > 0.30f)
                {
                    _validationIssues.Add(
                        $"{BuildPath(_targetRoot.transform, rect)} 靠近设计边缘但 Anchor 固定在中心；多比例屏幕可能发生漂移。");
                }
            }

            ValidateBreakpointOverlap();
        }

        private void ValidateBreakpointOverlap()
        {
            IReadOnlyList<UIAdaptationBreakpoint> breakpoints = _profile.Breakpoints;
            for (int i = 0; i < breakpoints.Count; i++)
            {
                UIAdaptationBreakpoint left = breakpoints[i];
                if (left == null)
                {
                    continue;
                }
                for (int j = i + 1; j < breakpoints.Count; j++)
                {
                    UIAdaptationBreakpoint right = breakpoints[j];
                    if (right == null)
                    {
                        continue;
                    }

                    bool sameOrientation =
                        left.Orientation == UIAdaptationOrientation.Any ||
                        right.Orientation == UIAdaptationOrientation.Any ||
                        left.Orientation == right.Orientation;
                    bool overlap =
                        Mathf.Max(left.MinAspect, right.MinAspect) <=
                        Mathf.Min(left.MaxAspect, right.MaxAspect);
                    if (sameOrientation && overlap)
                    {
                        _validationIssues.Add(
                            $"Breakpoint '{left.Id}' 与 '{right.Id}' 的 Aspect 区间重叠；当前按数组先后顺序命中。");
                    }
                }
            }
        }

        private void CreateRecommendedProfile()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create UI Adaptation Profile",
                "UIAdaptationProfile",
                "asset",
                "选择 Adaptation Profile 保存位置");
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var tablet = new UIAdaptationBreakpoint();
            tablet.Configure(
                "tablet",
                1.00f,
                1.59f,
                UIAdaptationOrientation.Any,
                0.70f);
            var phone = new UIAdaptationBreakpoint();
            phone.Configure(
                "phone",
                1.60f,
                1.94f,
                UIAdaptationOrientation.Any,
                0.50f);
            var phoneTall = new UIAdaptationBreakpoint();
            phoneTall.Configure(
                "phone_tall",
                1.95f,
                2.60f,
                UIAdaptationOrientation.Any,
                0.35f);

            UIAdaptationProfile profile = ScriptableObject.CreateInstance<UIAdaptationProfile>();
            profile.Configure(
                new Vector2(_previewWidth, _previewHeight),
                0.5f,
                true,
                new[] { tablet, phone, phoneTall });
            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();
            _profile = profile;
            Selection.activeObject = profile;
            EditorGUIUtility.PingObject(profile);
        }

        private void SetPreview(int width, int height)
        {
            _previewWidth = width;
            _previewHeight = height;
            _safeInsets = Vector4.zero;
        }

        private Rect BuildPreviewSafeArea()
        {
            float left = Mathf.Clamp(_safeInsets.x, 0f, _previewWidth);
            float bottom = Mathf.Clamp(_safeInsets.y, 0f, _previewHeight);
            float right = Mathf.Clamp(_safeInsets.z, 0f, _previewWidth - left);
            float top = Mathf.Clamp(_safeInsets.w, 0f, _previewHeight - bottom);
            return Rect.MinMaxRect(
                left,
                bottom,
                _previewWidth - right,
                _previewHeight - top);
        }

        private static RectTransform[] FindSafeAreaRoots(Transform root)
        {
            return root == null
                ? Array.Empty<RectTransform>()
                : root.GetComponentsInChildren<RectTransform>(true)
                    .Where(rect =>
                        string.Equals(
                            rect.name,
                            "SafeAreaRoot",
                            StringComparison.OrdinalIgnoreCase))
                    .ToArray();
        }

        private static string BuildPath(Transform root, Transform target)
        {
            var names = new List<string>();
            Transform current = target;
            while (current != null)
            {
                names.Add(current.name);
                if (current == root)
                {
                    break;
                }
                current = current.parent;
            }
            names.Reverse();
            return string.Join("/", names);
        }
    }
}
