using System;
using UnityEditor;
using UnityEngine;
using StellarFramework.FlowKit;
using StellarFramework.FlowKit.Unity;

namespace StellarFramework.Editor.Modules.FlowKit
{
    /// <summary>仅存在于框架开发工程的 Flow Graph 检查入口；业务项目导出包不包含此 Editor 模块。</summary>
    [StellarTool("FlowKit Graph 检查", "FlowKit", 0)]
    public sealed class FlowKitHubModule : ToolModule
    {
        private TextAsset _graphAsset;
        private FlowCompileResult _lastResult;
        private FlowGraphData _lastGraph;

        public override string Icon => "d_PlayButton";
        public override string Description => "读取选中的 Flow JSON，使用显式内置 Registry 执行版本、端口、参数与依赖检查。";

        public override void OnGUI()
        {
            Section("Graph 输入");
            _graphAsset = (TextAsset)EditorGUILayout.ObjectField("Flow JSON", _graphAsset, typeof(TextAsset), false);
            EditorGUILayout.HelpBox("Graph 是文本定义；运行时只使用编译后的不可变 FlowPlan。", MessageType.Info);
            using (new GUILayout.HorizontalScope())
            {
                if (PrimaryButton("使用当前选中文件", GUILayout.Width(180)))
                    _graphAsset = Selection.activeObject as TextAsset;
                if (PrimaryButton("检查 Graph", GUILayout.Width(140))) ValidateGraph();
            }

            if (_lastResult == null) return;
            if (_lastResult.Succeeded)
            {
                EditorGUILayout.HelpBox($"检查通过：{_lastResult.Plan.NodeCount} 个节点，PlanHash 0x{_lastResult.Plan.PlanHash:X16}。", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("检查未通过，不能启动该 Graph。", MessageType.Error);
            }

            for (int i = 0; i < _lastResult.Issues.Count; i++)
            {
                FlowValidationIssue issue = _lastResult.Issues[i];
                EditorGUILayout.LabelField(issue.ToString(), issue.IsError ? EditorStyles.boldLabel : EditorStyles.label);
            }
        }

        private void ValidateGraph()
        {
            _lastResult = null;
            _lastGraph = null;
            if (_graphAsset == null)
            {
                Window.ShowNotification(new GUIContent("请先指定 Flow JSON。"));
                return;
            }

            try
            {
                _lastGraph = FlowGraphJson.FromTextAsset(_graphAsset);
                _lastResult = FlowCompiler.Compile(_lastGraph, FlowBuiltInNodes.CreateRegistry());
            }
            catch (Exception exception)
            {
                Window.ShowNotification(new GUIContent(exception.Message));
            }

            Window.Repaint();
        }
    }
}
