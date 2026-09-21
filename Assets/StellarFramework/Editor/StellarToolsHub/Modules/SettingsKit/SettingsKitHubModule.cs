#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace StellarFramework.Editor.Modules
{
    [StellarTool("SettingsKit 设置中心", "框架核心", 6)]
    public class SettingsKitHubModule : ToolModule
    {
        private const string RuntimeFolderPath =
            "Assets/StellarFramework/Runtime/Kits/SettingsKit";
        private const string GuidePath =
            "Assets/StellarFramework/FrameworkDoc/02-Kits/SettingsKit/SettingsKit-设置系统-说明文档-Guide.md";

        public override string Icon => "d_SettingsIcon";
        public override string Description =>
            "统一打开 SettingsKit 文档并定位 Runtime 源码；Kit 用法由 FrameworkDoc 维护。";

        public override void OnGUI()
        {
            Section("总览");
            EditorGUILayout.HelpBox(
                "SettingsKit 负责设置定义、存储、应用策略、回滚以及页面级扩展，核心层保持与具体 UI 解耦。",
                MessageType.Info);

            Section("快捷入口");
            if (PrimaryButton("打开指南", GUILayout.Height(32)))
            {
                OpenAsset(GuidePath);
            }

            EditorGUILayout.HelpBox(
                "SettingsKit 不再维护独立 Sample 场景。最小接入、Provider 扩展、存储和 ApplyStrategy 用法统一以 FrameworkDoc 为准。",
                MessageType.None);

            Section("目录");
            if (GUILayout.Button("定位 SettingsKit 目录", GUILayout.Height(28)))
            {
                string absolutePath = Path.Combine(
                    Directory.GetParent(Application.dataPath).FullName,
                    RuntimeFolderPath.Replace('/', Path.DirectorySeparatorChar));
                EditorUtility.RevealInFinder(absolutePath);
            }
        }

        private static void OpenAsset(string assetPath)
        {
            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (asset == null)
            {
                Debug.LogError($"[SettingsKitHubModule] 未找到资源: {assetPath}");
                return;
            }

            AssetDatabase.OpenAsset(asset);
            EditorGUIUtility.PingObject(asset);
        }

    }
}
#endif
