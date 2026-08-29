#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace StellarFramework.Editor.Modules
{
    [StellarTool("样例构建", "样例支持", 0)]
    public class SampleBuildHubModule : ToolModule
    {
        public override string Icon => "d_SceneAsset Icon";
        public override string Description => "统一构建 KitSamples 的可运行场景和依赖资源。";

        public override void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "样例场景构建器已经收口到 Tools Hub。执行后会补齐 KitSamples 场景、测试资源和相关依赖。",
                MessageType.Info);

            if (PrimaryButton("构建 KitSamples 场景", GUILayout.Height(34)))
            {
                if (!TryInvokeSampleSceneBuilder(out string error))
                {
                    Debug.LogError(error);
                    Window.ShowNotification(new GUIContent("样例构建器不可用"));
                    return;
                }

                Window.ShowNotification(new GUIContent("KitSamples 构建完成"));
            }
        }

        private static bool TryInvokeSampleSceneBuilder(out string error)
        {
            error = null;

            Assembly sampleAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => assembly.GetName().Name == "StellarFramework.Samples.Editor");
            if (sampleAssembly == null)
            {
                error = "[SampleBuildHubModule] Could not find assembly StellarFramework.Samples.Editor.";
                return false;
            }

            Type builderType = sampleAssembly.GetType("StellarFramework.Editor.ExamplePlayableSceneBuilder");
            MethodInfo buildMethod = builderType?.GetMethod("BuildPlayableScenes", BindingFlags.Public | BindingFlags.Static);
            if (buildMethod == null)
            {
                error = "[SampleBuildHubModule] Could not find ExamplePlayableSceneBuilder.BuildPlayableScenes().";
                return false;
            }

            buildMethod.Invoke(null, null);
            return true;
        }
    }
}
#endif
