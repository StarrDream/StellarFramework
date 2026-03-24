#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace StellarFramework.Editor.Modules
{
    /// <summary>
    /// ConfigKit 的 Hub 入口模块
    /// 职责: 将 ConfigKit 注册到 StellarFramework Tools Hub，提供统一的 Dashboard 入口
    /// </summary>
    [StellarTool("ConfigKit 配置中心", "框架核心", 4)]
    public class ConfigKitHubModule : ToolModule
    {
        public override string Icon => "d_SettingsIcon";
        public override string Description => "统一的配置管理入口。支持普通配置与网络配置的横向扩展、可视化编辑与环境切换。";

        public override void OnGUI()
        {
            Section("配置管理面板");

            EditorGUILayout.HelpBox(
                "ConfigKit 现已重构为模块化架构。\n" +
                "所有的配置增删改查、字段编辑以及网络环境切换，均已整合至独立的 Dashboard 中。",
                MessageType.Info);

            GUILayout.Space(15);

            // 使用 Hub 提供的 PrimaryButton 样式，保持 UI 风格统一
            if (PrimaryButton("打开 ConfigKit Dashboard", GUILayout.Height(36)))
            {
                ConfigKitWindow.ShowWindow();
            }

            GUILayout.Space(20);
            Section("快捷操作");

            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("打开本地存档目录 (PersistentDataPath)", Window.GhostButtonStyle))
                {
                    EditorUtility.RevealInFinder(Application.persistentDataPath);
                }

                if (GUILayout.Button("打开包内配置目录 (StreamingAssets)", Window.GhostButtonStyle))
                {
                    string path = System.IO.Path.Combine(Application.streamingAssetsPath, "Configs");
                    if (!System.IO.Directory.Exists(path))
                    {
                        System.IO.Directory.CreateDirectory(path);
                    }

                    EditorUtility.RevealInFinder(path);
                }
            }
        }
    }
}
#endif