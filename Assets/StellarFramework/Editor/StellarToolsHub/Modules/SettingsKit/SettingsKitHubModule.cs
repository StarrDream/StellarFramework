#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace StellarFramework.Editor.Modules
{
    [StellarTool("SettingsKit 设置中心", "框架核心", 6)]
    public class SettingsKitHubModule : ToolModule
    {
        private const string ScenePath =
            "Assets/StellarFramework/Samples/KitSamples/Scenes/SettingsKit_Playable.unity";

        private const string RuntimeExamplePath =
            "Assets/StellarFramework/Samples/KitSamples/Example_SettingsKit/Example_SettingsKit.cs";

        private const string ProviderExamplePath =
            "Assets/StellarFramework/Samples/KitSamples/Example_SettingsKit/ExampleSettingsExtensionsProvider.cs";

        private const string RuntimeFolderPath =
            "Assets/StellarFramework/Runtime/Kits/SettingsKit";

        public override string Icon => "d_SettingsIcon";
        public override string Description =>
            "统一打开 SettingsKit 文档、示例脚本、Playable 场景与配套构建入口。";

        public override void OnGUI()
        {
            Section("总览");
            EditorGUILayout.HelpBox(
                "SettingsKit 负责设置定义、存储、应用策略、回滚以及页面级扩展，核心层保持与具体 UI 解耦。",
                MessageType.Info);

            Section("快捷入口");
            using (new GUILayout.HorizontalScope())
            {
                if (PrimaryButton("打开指南", GUILayout.Height(32)))
                {
                    OpenGuide();
                }

                if (PrimaryButton("打开 Playable 场景", GUILayout.Height(32)))
                {
                    OpenScene(ScenePath);
                }
            }

            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("打开示例脚本", GUILayout.Height(28)))
                {
                    OpenAsset(RuntimeExamplePath);
                }

                if (GUILayout.Button("打开扩展 Provider", GUILayout.Height(28)))
                {
                    OpenAsset(ProviderExamplePath);
                }
            }

            Section("工作流");
            EditorGUILayout.HelpBox(
                "如果 SettingsKit 的 Playable 场景缺失或内容过旧，可在这里重建 KitSamples。",
                MessageType.None);

            if (PrimaryButton("重建 KitSamples 场景", GUILayout.Height(34)))
            {
                if (!TryInvokeSampleSceneBuilder(out string error))
                {
                    Debug.LogError(error);
                    Window.ShowNotification(new GUIContent("SettingsKit 样例构建失败"));
                    return;
                }

                Window.ShowNotification(new GUIContent("KitSamples 构建完成"));
            }

            Section("目录");
            if (GUILayout.Button("定位 SettingsKit 目录", GUILayout.Height(28)))
            {
                string absolutePath = Path.Combine(
                    Directory.GetParent(Application.dataPath).FullName,
                    RuntimeFolderPath.Replace('/', Path.DirectorySeparatorChar));
                EditorUtility.RevealInFinder(absolutePath);
            }
        }

        private static void OpenGuide()
        {
            string[] guideGuids = AssetDatabase.FindAssets("SettingsKit t:TextAsset", new[] { RuntimeFolderPath });
            string guidePath = guideGuids
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(path => path.EndsWith(".md", StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrEmpty(guidePath))
            {
                Debug.LogError("[SettingsKitHubModule] 未找到 SettingsKit 指南文档。");
                return;
            }

            OpenAsset(guidePath);
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

        private static void OpenScene(string scenePath)
        {
            if (!File.Exists(scenePath))
            {
                Debug.LogWarning($"[SettingsKitHubModule] 未找到场景，建议先重建样例。Path={scenePath}");
                return;
            }

            EditorSceneManager.OpenScene(scenePath);
        }

        private static bool TryInvokeSampleSceneBuilder(out string error)
        {
            error = null;

            Assembly sampleAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => assembly.GetName().Name == "StellarFramework.Samples.Editor");
            if (sampleAssembly == null)
            {
                error = "[SettingsKitHubModule] 未找到程序集 StellarFramework.Samples.Editor。";
                return false;
            }

            Type builderType = sampleAssembly.GetType("StellarFramework.Editor.ExamplePlayableSceneBuilder");
            MethodInfo buildMethod = builderType?.GetMethod("BuildPlayableScenes", BindingFlags.Public | BindingFlags.Static);
            if (buildMethod == null)
            {
                error = "[SettingsKitHubModule] 未找到 ExamplePlayableSceneBuilder.BuildPlayableScenes()。";
                return false;
            }

            buildMethod.Invoke(null, null);
            return true;
        }
    }
}
#endif
