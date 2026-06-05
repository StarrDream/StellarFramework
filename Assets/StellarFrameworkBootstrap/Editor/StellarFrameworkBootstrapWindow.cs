using UnityEditor;
using UnityEngine;

namespace StellarFrameworkBootstrap
{
    internal sealed class StellarFrameworkBootstrapWindow : EditorWindow
    {
        private Vector2 _scroll;

        [MenuItem("StellarFramework/单包安装器")]
        public static void Open()
        {
            StellarFrameworkBootstrapWindow window = GetWindow<StellarFrameworkBootstrapWindow>("单包安装器");
            window.minSize = new Vector2(620, 420);
            window.Show();
        }

        private void Update()
        {
            StellarFrameworkBootstrapInstaller.Tick();
            if (StellarFrameworkBootstrapInstaller.IsBusy)
            {
                Repaint();
            }
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawHeader();
            DrawInstallActions();
            DrawStatus();
            EditorGUILayout.EndScrollView();
        }

        private static void DrawHeader()
        {
            EditorGUILayout.LabelField("StellarFramework 单包安装器", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "这个安装器面向干净工程。你只需要先导入一个 `StellarFramework.unitypackage`，然后点击下面的一键安装按钮，安装器会自动补齐依赖并导入完整框架。",
                MessageType.Info);
        }

        private static void DrawInstallActions()
        {
            using (new GUILayout.VerticalScope("box"))
            {
                using (new EditorGUI.DisabledScope(StellarFrameworkBootstrapInstaller.IsBusy))
                {
                    if (GUILayout.Button("一键安装 StellarFramework", GUILayout.Height(40)))
                    {
                        StellarFrameworkBootstrapInstaller.StartSinglePackageInstall();
                    }
                }
            }
        }

        private static void DrawStatus()
        {
            GUILayout.Space(12f);
            EditorGUILayout.LabelField("安装状态", EditorStyles.boldLabel);

            foreach (string message in StellarFrameworkBootstrapInstaller.Messages)
            {
                EditorGUILayout.HelpBox(message, MessageType.Info);
            }

            foreach (string error in StellarFrameworkBootstrapInstaller.Errors)
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }
        }
    }
}
