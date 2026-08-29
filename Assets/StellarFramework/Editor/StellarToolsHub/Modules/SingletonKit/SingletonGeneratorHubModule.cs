#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace StellarFramework.Editor.Modules
{
    [StellarTool("SingletonKit 注册表", "框架核心", 3,
        RequiredAssemblyNames = new[] { "StellarFramework.Singleton.Editor" })]
    public sealed class SingletonGeneratorHubModule : ToolModule
    {
        public override string Icon => "d_ScriptableObject Icon";
        public override string Description => "生成 SingletonRegister，确保运行时静态注册表与当前代码保持一致。";

        public override void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "构建前会自动执行一次。这里保留手动入口，方便在编辑器内立即刷新单例静态注册表。",
                MessageType.Info);

            if (PrimaryButton("立即生成 SingletonRegister", GUILayout.Height(34)))
            {
                SingletonGenerator.Generate();
            }
        }
    }
}
#endif
