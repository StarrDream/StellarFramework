using System;
using UnityEditor;
using UnityEngine;
using StellarFramework.FlowKit;
using StellarFramework.FlowKit.Unity;

namespace StellarFramework.Editor.Modules.FlowKit
{
    /// <summary>独立的 Graph 检查窗口，随框架开发工程提供；不会进入 Runtime 导出包。</summary>
    public sealed class FlowGraphWindow : EditorWindow
    {
        private TextAsset _graph;
        private FlowCompileResult _result;
        private Vector2 _scroll;

        [MenuItem("StellarFramework/FlowKit/Graph Validator")]
        public static void ShowWindow() => GetWindow<FlowGraphWindow>("FlowKit Graph");

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("FlowKit Graph Validator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("只检查文本 Graph，不修改源文件。编译结果是不可变 FlowPlan。", MessageType.Info);
            _graph = (TextAsset)EditorGUILayout.ObjectField("Graph JSON", _graph, typeof(TextAsset), false);
            if (GUILayout.Button("Validate", GUILayout.Height(28f))) Validate();
            if (_result == null) return;

            MessageType type = _result.Succeeded ? MessageType.Info : MessageType.Error;
            EditorGUILayout.HelpBox(_result.Succeeded
                ? $"Valid · Nodes: {_result.Plan.NodeCount} · PlanHash: 0x{_result.Plan.PlanHash:X16}"
                : "Invalid · 不允许启动。", type);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < _result.Issues.Count; i++)
            {
                FlowValidationIssue issue = _result.Issues[i];
                EditorGUILayout.LabelField(issue.ToString(), issue.IsError ? EditorStyles.boldLabel : EditorStyles.label);
            }
            EditorGUILayout.EndScrollView();
        }

        private void Validate()
        {
            _result = null;
            if (_graph == null)
            {
                ShowNotification(new GUIContent("请选择 Flow JSON。"));
                return;
            }

            try
            {
                FlowGraphData data = FlowGraphJson.FromTextAsset(_graph);
                _result = FlowCompiler.Compile(data, FlowBuiltInNodes.CreateRegistry());
            }
            catch (Exception exception)
            {
                ShowNotification(new GUIContent(exception.Message));
            }
        }
    }
}
