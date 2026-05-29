#if UNITY_EDITOR && !UNITY_2021_2_OR_NEWER
using UnityEditor;
using UnityEngine;

namespace StellarFramework.Editor.DevTools
{
    [InitializeOnLoad]
    internal static class DeveloperQuickToolsSceneViewFallback
    {
        static DeveloperQuickToolsSceneViewFallback()
        {
#if UNITY_2019_1_OR_NEWER
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
#else
            SceneView.onSceneGUIDelegate -= OnSceneGUI;
            SceneView.onSceneGUIDelegate += OnSceneGUI;
#endif
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(8f, 8f, 420f, 30f), EditorStyles.toolbar);
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("星河 ▾", EditorStyles.toolbarDropDown, GUILayout.Width(72f)))
                {
                    var menu = new GenericMenu();
                    DeveloperQuickToolsSceneService.BuildFullMenu(menu);
                    menu.ShowAsContext();
                }

                if (!DeveloperQuickToolsStore.Preferences.CompactToolbar)
                {
                    if (GUILayout.Button("场景 ▾", EditorStyles.toolbarDropDown, GUILayout.Width(72f)))
                    {
                        var menu = new GenericMenu();
                        DeveloperQuickToolsSceneService.BuildDirectSceneSwitchMenu(menu);
                        menu.ShowAsContext();
                    }

                    string favoriteLabel = DeveloperQuickToolsSceneService.IsCurrentSceneFavorite() ? "取消收藏" : "收藏";
                    if (GUILayout.Button(favoriteLabel, EditorStyles.toolbarButton, GUILayout.Width(68f)))
                    {
                        DeveloperQuickToolsSceneService.ToggleCurrentSceneFavorite();
                    }

                    if (GUILayout.Button("重开", EditorStyles.toolbarButton, GUILayout.Width(48f)))
                    {
                        DeveloperQuickToolsSceneService.ReloadActiveScene();
                    }
                }

                if (GUILayout.Button(DeveloperQuickToolsTime.CurrentLabel + " ▾", EditorStyles.toolbarDropDown, GUILayout.Width(72f)))
                {
                    var menu = new GenericMenu();
                    DeveloperQuickToolsSceneService.BuildTimeScaleMenu(menu, "倍速");
                    menu.ShowAsContext();
                }
            }

            GUILayout.EndArea();
            Handles.EndGUI();
        }
    }
}
#endif
