#if UNITY_EDITOR && UNITY_2021_2_OR_NEWER
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace StellarFramework.Editor.DevTools
{
    [Overlay(typeof(SceneView), "星河开发工具", true)]
    public sealed class DeveloperQuickToolsOverlay : ToolbarOverlay
    {
        private const string MenuId = "StellarFramework/DevTools/Menu";
        private const string SceneId = "StellarFramework/DevTools/Scene";
        private const string FavoriteId = "StellarFramework/DevTools/Favorite";
        private const string ReloadId = "StellarFramework/DevTools/Reload";
        private const string TimeScaleId = "StellarFramework/DevTools/TimeScale";
        private const string ToolsHubId = "StellarFramework/DevTools/ToolsHub";

        public DeveloperQuickToolsOverlay()
            : base(MenuId, SceneId, FavoriteId, ReloadId, TimeScaleId, ToolsHubId)
        {
        }

        [EditorToolbarElement(MenuId, typeof(SceneView))]
        private sealed class MainMenuDropdown : EditorToolbarDropdown
        {
            public MainMenuDropdown()
            {
                text = "星河";
                tooltip = "星河开发工具：场景切换、倍速调试和常用本机工具";
                clicked += ShowMenu;
            }

            private static void ShowMenu()
            {
                var menu = new GenericMenu();
                DeveloperQuickToolsSceneService.BuildFullMenu(menu);
                menu.ShowAsContext();
            }
        }

        [EditorToolbarElement(SceneId, typeof(SceneView))]
        private sealed class SceneDropdown : EditorToolbarDropdown
        {
            public SceneDropdown()
            {
                text = "场景";
                tooltip = "快速切换所有场景、最近打开、收藏和 Build Settings 场景";
                clicked += ShowMenu;
                RegisterCallback<AttachToPanelEvent>(_ => RefreshText());
                schedule.Execute(RefreshText).Every(500);
            }

            private void RefreshText()
            {
                bool compact = DeveloperQuickToolsStore.Preferences.CompactToolbar;
                style.display = compact ? DisplayStyle.None : DisplayStyle.Flex;
                text = $"场景：{DeveloperQuickToolsSceneService.CurrentSceneName}";
            }

            private void ShowMenu()
            {
                RefreshText();
                var menu = new GenericMenu();
                DeveloperQuickToolsSceneService.BuildDirectSceneSwitchMenu(menu);
                menu.ShowAsContext();
            }
        }

        [EditorToolbarElement(FavoriteId, typeof(SceneView))]
        private sealed class FavoriteButton : EditorToolbarButton
        {
            public FavoriteButton()
            {
                text = "收藏";
                tooltip = "收藏或取消收藏当前场景";
                clicked += () =>
                {
                    DeveloperQuickToolsSceneService.ToggleCurrentSceneFavorite();
                    Refresh();
                };
                RegisterCallback<AttachToPanelEvent>(_ => Refresh());
                schedule.Execute(Refresh).Every(500);
            }

            private void Refresh()
            {
                style.display = DeveloperQuickToolsStore.Preferences.CompactToolbar ? DisplayStyle.None : DisplayStyle.Flex;
                text = DeveloperQuickToolsSceneService.IsCurrentSceneFavorite() ? "取消收藏" : "收藏";
            }
        }

        [EditorToolbarElement(ReloadId, typeof(SceneView))]
        private sealed class ReloadButton : EditorToolbarButton
        {
            public ReloadButton()
            {
                text = "重开";
                tooltip = "重新打开当前场景，打开前会提示保存未保存内容";
                clicked += () => DeveloperQuickToolsSceneService.ReloadActiveScene();
                RegisterCallback<AttachToPanelEvent>(_ => RefreshVisibility());
                schedule.Execute(RefreshVisibility).Every(500);
            }

            private void RefreshVisibility()
            {
                style.display = DeveloperQuickToolsStore.Preferences.CompactToolbar ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        [EditorToolbarElement(TimeScaleId, typeof(SceneView))]
        private sealed class TimeScaleDropdown : EditorToolbarDropdown
        {
            public TimeScaleDropdown()
            {
                text = "倍速";
                tooltip = "设置 Time.timeScale，并同步调整 Time.fixedDeltaTime";
                clicked += ShowMenu;
                RegisterCallback<AttachToPanelEvent>(_ => RefreshText());
                schedule.Execute(RefreshText).Every(500);
            }

            private void RefreshText()
            {
                text = DeveloperQuickToolsStore.Preferences.CompactToolbar
                    ? DeveloperQuickToolsTime.CurrentLabel
                    : $"倍速：{DeveloperQuickToolsTime.CurrentLabel}";
            }

            private void ShowMenu()
            {
                RefreshText();
                var menu = new GenericMenu();
                DeveloperQuickToolsSceneService.BuildTimeScaleMenu(menu, "倍速");
                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("恢复 1x"), false, () => DeveloperQuickToolsTime.SetTimeScale(1f, true));
                menu.ShowAsContext();
            }
        }

        [EditorToolbarElement(ToolsHubId, typeof(SceneView))]
        private sealed class ToolsHubButton : EditorToolbarButton
        {
            public ToolsHubButton()
            {
                text = "ToolsHub";
                tooltip = "打开 StellarFramework Tools Hub";
                clicked += StellarFrameworkTools.ShowWindow;
                RegisterCallback<AttachToPanelEvent>(_ => RefreshVisibility());
                schedule.Execute(RefreshVisibility).Every(500);
            }

            private void RefreshVisibility()
            {
                style.display = DeveloperQuickToolsStore.Preferences.CompactToolbar ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }
    }
}
#endif
