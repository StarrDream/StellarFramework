using System.Collections.Generic;
using StellarFramework.RuntimeTools;
using UnityEditor;
using UnityEngine;

namespace StellarFramework.Editor.Modules
{
    /// <summary>
    /// RuntimeTools 的轻量 ToolsHub 入口。
    /// 只提供高频 authoring/diagnostics，不复制 Runtime 行为，也不让 Runtime 依赖 Editor。
    /// </summary>
    [StellarTool("Runtime Tools", "框架核心", 12,
        RequiredAssemblyNames = new[] { "StellarFramework.Runtime.Tools" })]
    public sealed class RuntimeToolsHubModule : ToolModule
    {
        private GameObject _target;
        private TransformSnapshot _snapshot;
        private bool _hasSnapshot;
        private int _snapshotTargetId;
        private string _boundsResult = "尚未计算";
        private readonly List<Material> _materialScratch = new List<Material>(4);

        public override string Icon => "d_ToolHandleGlobal";
        public override string Description =>
            "RuntimeTools 快速挂载与诊断：Transform 快照、Bounds 检查、常用独立组件快速添加。";

        public override void OnEnable()
        {
            if (Selection.activeGameObject != null)
            {
                _target = Selection.activeGameObject;
            }
        }

        public override void OnSelectionChange()
        {
            if (Selection.activeGameObject != null)
            {
                _target = Selection.activeGameObject;
            }
        }

        public override void OnGUI()
        {
            DrawTargetSection();
            DrawTransformSection();
            DrawComponentSection();
            DrawBoundsSection();
            DrawRuntimeDiagnosticsSection();
            DrawValidationSection();
        }

        private void DrawTargetSection()
        {
            Section("Target");
            _target = (GameObject)EditorGUILayout.ObjectField(
                "GameObject",
                _target,
                typeof(GameObject),
                true);

            if (_target == null)
            {
                EditorGUILayout.HelpBox("选择一个 Scene GameObject 后可使用 RuntimeTools 快捷操作。", MessageType.Info);
            }
        }

        private void DrawTransformSection()
        {
            Section("Transform Snapshot");
            using (new EditorGUI.DisabledScope(_target == null))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("捕获当前 Transform"))
                    {
                        _snapshot = TransformSnapshot.Capture(_target.transform);
                        _hasSnapshot = true;
                        _snapshotTargetId = _target.GetInstanceID();
                    }

                    bool canRestore = _hasSnapshot &&
                                      _target != null &&
                                      _target.GetInstanceID() == _snapshotTargetId;
                    using (new EditorGUI.DisabledScope(!canRestore))
                    {
                        if (GUILayout.Button("恢复快照"))
                        {
                            Undo.RecordObject(_target.transform, "Restore RuntimeTools Transform Snapshot");
                            _snapshot.Restore(_target.transform);
                            EditorUtility.SetDirty(_target.transform);
                        }
                    }
                }

                if (GUILayout.Button("Reset Local Transform"))
                {
                    Undo.RecordObject(_target.transform, "Reset Local Transform");
                    _target.transform.ResetLocal();
                    EditorUtility.SetDirty(_target.transform);
                }
            }
        }

        private void DrawComponentSection()
        {
            Section("Quick Add Components");
            if (_target == null)
            {
                return;
            }

            DrawAddButton<FollowTarget>("Follow Target");
            DrawAddButton<Rotator>("Rotator");
            DrawAddButton<UniversalBillboard>("Universal Billboard");
            DrawAddButton<GroundChecker>("Ground Checker");
            DrawAddButton<TriggerRelay>("Trigger Relay");
            DrawAddButton<CollisionRelay>("Collision Relay");
            DrawAddButton<TransformShake>("Transform Shake");
            DrawAddButton<FrameRateMonitor>("Frame Rate Monitor");

            Renderer renderer = _target.GetComponent<Renderer>();
            bool hasPropertyBlock = _target.GetComponent<RendererPropertyBlockController>() != null;
            using (new EditorGUI.DisabledScope(renderer == null || hasPropertyBlock))
            {
                string label = renderer == null
                    ? "Property Block Controller  (需要 Renderer)"
                    : hasPropertyBlock
                        ? "Property Block Controller  ✓"
                        : "添加 Property Block Controller";
                if (GUILayout.Button(label))
                {
                    Undo.AddComponent<RendererPropertyBlockController>(_target);
                }
            }
        }

        private void DrawBoundsSection()
        {
            Section("Bounds Diagnostics");
            using (new EditorGUI.DisabledScope(_target == null))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Renderer Bounds"))
                    {
                        _boundsResult = BoundsUtility.TryCalculateRendererBounds(
                            _target.transform,
                            true,
                            out Bounds bounds)
                            ? FormatBounds(bounds)
                            : "未找到 Renderer";
                    }

                    if (GUILayout.Button("Collider Bounds"))
                    {
                        _boundsResult = BoundsUtility.TryCalculateColliderBounds(
                            _target.transform,
                            true,
                            out Bounds bounds)
                            ? FormatBounds(bounds)
                            : "未找到 Collider";
                    }
                }
            }

            EditorGUILayout.HelpBox(_boundsResult, MessageType.None);
        }

        private void DrawRuntimeDiagnosticsSection()
        {
            Section("Runtime Diagnostics");
            if (_target == null)
            {
                EditorGUILayout.HelpBox("选择目标后可查看 RuntimeTools 运行时诊断。", MessageType.Info);
                return;
            }

            FrameRateMonitor monitor = _target.GetComponent<FrameRateMonitor>();
            if (monitor == null)
            {
                EditorGUILayout.HelpBox("目标没有 FrameRateMonitor。需要帧率数据时可在上方快速添加。", MessageType.Info);
                return;
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("进入 PlayMode 后显示 Current / Avg / Min / Max FPS。", MessageType.Info);
                return;
            }

            FrameRateSnapshot snapshot = monitor.Snapshot;
            EditorGUILayout.LabelField("Current FPS", snapshot.CurrentFps.ToString("F1"));
            EditorGUILayout.LabelField("Average FPS", snapshot.AverageFps.ToString("F1"));
            EditorGUILayout.LabelField("Min / Max", $"{snapshot.MinFps:F1} / {snapshot.MaxFps:F1}");
            EditorGUILayout.LabelField("Samples", snapshot.SampleCount.ToString());
        }

        private void DrawValidationSection()
        {
            Section("Selection Validation");
            if (_target == null)
            {
                EditorGUILayout.HelpBox("No target selected.", MessageType.Info);
                return;
            }

            int issueCount = 0;
            FollowTarget[] followers = _target.GetComponents<FollowTarget>();
            for (int i = 0; i < followers.Length; i++)
            {
                if (followers[i].Target == null)
                {
                    issueCount++;
                    EditorGUILayout.HelpBox("FollowTarget 尚未指定 Target。", MessageType.Warning);
                }
            }

            if (_target.GetComponent<TriggerRelay>() != null)
            {
                Collider collider = _target.GetComponent<Collider>();
                if (collider == null || !collider.isTrigger)
                {
                    issueCount++;
                    EditorGUILayout.HelpBox(
                        "TriggerRelay 通常需要同物体存在 isTrigger=true 的 Collider。",
                        MessageType.Warning);
                }
            }

            if (_target.GetComponent<CollisionRelay>() != null && _target.GetComponent<Collider>() == null)
            {
                issueCount++;
                EditorGUILayout.HelpBox("CollisionRelay 当前物体没有 Collider。", MessageType.Warning);
            }

            RendererPropertyBlockController propertyBlock = _target.GetComponent<RendererPropertyBlockController>();
            if (propertyBlock != null)
            {
                Renderer renderer = propertyBlock.TargetRenderer;
                if (renderer == null)
                {
                    issueCount++;
                    EditorGUILayout.HelpBox("RendererPropertyBlockController 没有可用 Renderer。", MessageType.Warning);
                }
                else if (propertyBlock.MaterialIndex >= 0)
                {
                    _materialScratch.Clear();
                    renderer.GetSharedMaterials(_materialScratch);
                    if (propertyBlock.MaterialIndex >= _materialScratch.Count)
                    {
                        issueCount++;
                        EditorGUILayout.HelpBox(
                            $"PropertyBlock MaterialIndex={propertyBlock.MaterialIndex} 超出 Renderer 材质槽数量 {_materialScratch.Count}。",
                            MessageType.Warning);
                    }
                }
            }

            if (issueCount == 0)
            {
                EditorGUILayout.HelpBox("当前选择未发现 RuntimeTools 基础配置风险。", MessageType.Info);
            }
        }

        private void DrawAddButton<T>(string label) where T : Component
        {
            bool exists = _target.GetComponent<T>() != null;
            using (new EditorGUI.DisabledScope(exists))
            {
                if (GUILayout.Button(exists ? $"{label}  ✓" : $"添加 {label}"))
                {
                    Undo.AddComponent<T>(_target);
                }
            }
        }

        private static string FormatBounds(Bounds bounds)
        {
            return $"Center: {bounds.center:F3}\nSize: {bounds.size:F3}\nMin: {bounds.min:F3}\nMax: {bounds.max:F3}";
        }
    }
}
