#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StellarFramework.Editor.DevTools
{
    [StellarTool("开发者快捷工具", "生产力", -100)]
    public sealed class DeveloperQuickToolsHubModule : ToolModule
    {
        private string _newGroupName = "";
        private string _selectedGroupName = DeveloperQuickToolsStore.DefaultFavoriteGroupName;
        private Vector2 _sceneScroll;
        private Vector2 _favoriteScroll;
        private string _sceneSearch = "";
        private float _customTimeScale = 1f;

        public override string Icon => "d_SceneViewTools";
        public override string Description => "管理个人场景收藏、最近打开、调试倍速和常用本机开发入口。";

        public override void OnEnable()
        {
            DeveloperQuickToolsStore.Reload();
            EnsureSelectedGroup();
            _customTimeScale = DeveloperQuickToolsStore.Preferences.SelectedTimeScale;
        }

        public override void OnGUI()
        {
            DeveloperQuickToolsSceneService.CleanupMissingSceneReferences(false);

            DrawStatus();
            DrawSceneActions();
            DrawTimeScale();
            DrawFavoriteGroups();
            DrawRecentScenes();
            DrawProjectScenes();
            DrawLocalUtilities();
        }

        private void DrawStatus()
        {
            Section("当前状态");
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("当前场景", DeveloperQuickToolsSceneService.CurrentSceneName);
                EditorGUILayout.LabelField("场景路径", string.IsNullOrEmpty(DeveloperQuickToolsSceneService.CurrentScenePath)
                    ? "当前场景尚未保存"
                    : DeveloperQuickToolsSceneService.CurrentScenePath);
                EditorGUILayout.LabelField("当前倍速", DeveloperQuickToolsTime.CurrentLabel);
                DeveloperQuickToolsStore.Preferences.CompactToolbar =
                    EditorGUILayout.ToggleLeft("SceneView 工具条使用紧凑模式", DeveloperQuickToolsStore.Preferences.CompactToolbar);

                if (GUILayout.Button("保存个人工具配置", GUILayout.Height(24)))
                {
                    DeveloperQuickToolsStore.Save();
                    SceneView.RepaintAll();
                    Window.ShowNotification(new GUIContent("个人工具配置已保存"));
                }
            }
        }

        private void DrawSceneActions()
        {
            Section("场景快捷操作");
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new GUILayout.HorizontalScope())
                {
                    string favoriteLabel = DeveloperQuickToolsSceneService.IsCurrentSceneFavorite()
                        ? "取消收藏当前场景"
                        : "收藏当前场景";
                    if (PrimaryButton(favoriteLabel, GUILayout.Height(28)))
                    {
                        DeveloperQuickToolsSceneService.ToggleCurrentSceneFavorite();
                    }

                    if (GUILayout.Button("重新打开当前场景", GUILayout.Height(28)))
                    {
                        DeveloperQuickToolsSceneService.ReloadActiveScene();
                    }

                    if (GUILayout.Button("定位当前场景资源", GUILayout.Height(28)))
                    {
                        DeveloperQuickToolsSceneService.PingCurrentScene();
                    }
                }

                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("打开当前场景目录", GUILayout.Height(26)))
                    {
                        DeveloperQuickToolsSceneService.RevealCurrentSceneFolder();
                    }

                    EditorGUILayout.HelpBox("快捷切换只显示业务工程场景，不显示 StellarFramework 框架自带样例场景。", MessageType.Info);
                }
            }
        }

        private void DrawTimeScale()
        {
            Section("倍速调试");
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox(
                    "倍速会同步设置 Time.timeScale 与 Time.fixedDeltaTime。退出 PlayMode 时自动恢复 1x 和原始 fixedDeltaTime。",
                    MessageType.Info);

                using (new GUILayout.HorizontalScope())
                {
                    foreach (float preset in DeveloperQuickToolsLogic.CompactTimeScalePresets)
                    {
                        if (GUILayout.Button(DeveloperQuickToolsTime.FormatTimeScale(preset), GUILayout.Height(26)))
                        {
                            DeveloperQuickToolsTime.SetTimeScale(preset, true);
                            _customTimeScale = preset;
                        }
                    }
                }

                using (new GUILayout.HorizontalScope())
                {
                    _customTimeScale = EditorGUILayout.Slider("自定义倍速", _customTimeScale, 0f, 100f);
                    if (GUILayout.Button("应用", GUILayout.Width(80)))
                    {
                        DeveloperQuickToolsTime.SetTimeScale(_customTimeScale, true);
                    }

                    if (GUILayout.Button("恢复 1x", GUILayout.Width(80)))
                    {
                        DeveloperQuickToolsTime.SetTimeScale(1f, true);
                        _customTimeScale = 1f;
                    }
                }

                EditorGUILayout.LabelField("预设", string.Join(" / ",
                    DeveloperQuickToolsStore.Preferences.TimeScalePresets.Select(DeveloperQuickToolsTime.FormatTimeScale)));
            }
        }

        private void DrawFavoriteGroups()
        {
            Section("个人场景收藏");
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EnsureSelectedGroup();
                string[] groupNames = DeveloperQuickToolsStore.Preferences.FavoriteGroups
                    .Select(group => group.Name)
                    .ToArray();

                int selectedIndex = Mathf.Max(0, Array.IndexOf(groupNames, _selectedGroupName));
                selectedIndex = EditorGUILayout.Popup("当前分组", selectedIndex, groupNames);
                if (groupNames.Length > 0) _selectedGroupName = groupNames[selectedIndex];

                using (new GUILayout.HorizontalScope())
                {
                    _newGroupName = EditorGUILayout.TextField("新分组", _newGroupName);
                    if (GUILayout.Button("新增分组", GUILayout.Width(100)))
                    {
                        if (string.IsNullOrWhiteSpace(_newGroupName))
                        {
                            Window.ShowNotification(new GUIContent("分组名称不能为空"));
                        }
                        else
                        {
                            DeveloperQuickToolsStore.GetOrCreateGroup(_newGroupName);
                            _selectedGroupName = _newGroupName.Trim();
                            _newGroupName = "";
                        }
                    }

                    if (GUILayout.Button("删除分组", GUILayout.Width(100)))
                    {
                        if (EditorUtility.DisplayDialog("删除收藏分组", $"确定删除分组“{_selectedGroupName}”吗？", "删除", "取消"))
                        {
                            DeveloperQuickToolsStore.RemoveGroup(_selectedGroupName);
                            EnsureSelectedGroup();
                        }
                    }
                }

                _favoriteScroll = EditorGUILayout.BeginScrollView(_favoriteScroll, GUILayout.MinHeight(80), GUILayout.MaxHeight(180));
                List<DeveloperQuickSceneReference> favoriteScenes = DeveloperQuickToolsStore.Preferences.FavoriteGroups
                    .Where(group => group?.Scenes != null)
                    .SelectMany(group => group.Scenes)
                    .Where(scene => scene != null)
                    .GroupBy(scene => string.IsNullOrEmpty(scene.Guid) ? scene.Path : scene.Guid)
                    .Select(group => group.First())
                    .ToList();

                if (favoriteScenes.Count == 0)
                {
                    EditorGUILayout.HelpBox("当前分组还没有收藏场景。可以从当前场景或下方工程场景列表加入。", MessageType.Info);
                }
                else
                {
                    foreach (DeveloperQuickSceneReference scene in favoriteScenes)
                    {
                        DrawSceneReferenceRow(scene,
                            () => DeveloperQuickToolsSceneService.OpenScene(scene),
                            () => DeveloperQuickToolsSceneService.PingScene(DeveloperQuickToolsSceneService.ResolvePath(scene)),
                            () => DeveloperQuickToolsSceneService.RemoveFavoriteScene(scene),
                            "取消收藏");
                    }
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawRecentScenes()
        {
            Section("最近打开");
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DeveloperQuickToolsStore.Preferences.RecentLimit =
                    EditorGUILayout.IntSlider("记录数量", DeveloperQuickToolsStore.Preferences.RecentLimit, 3, 50);

                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("保存数量设置", GUILayout.Height(24)))
                    {
                        DeveloperQuickToolsStore.Save();
                    }

                    if (GUILayout.Button("清空最近打开", GUILayout.Height(24)))
                    {
                        DeveloperQuickToolsStore.Preferences.RecentScenes.Clear();
                        DeveloperQuickToolsStore.Save();
                    }
                }

                foreach (DeveloperQuickSceneReference scene in DeveloperQuickToolsStore.Preferences.RecentScenes.ToList())
                {
                    DrawSceneReferenceRow(scene,
                        () => DeveloperQuickToolsSceneService.OpenScene(scene),
                        () => DeveloperQuickToolsSceneService.PingScene(DeveloperQuickToolsSceneService.ResolvePath(scene)),
                        () =>
                        {
                            DeveloperQuickToolsStore.Preferences.RecentScenes.Remove(scene);
                            DeveloperQuickToolsStore.Save();
                        });
                }
            }
        }

        private void DrawProjectScenes()
        {
            Section("工程场景列表");
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _sceneSearch = EditorGUILayout.TextField("搜索场景", _sceneSearch);
                List<DeveloperQuickSceneReference> scenes = DeveloperQuickToolsSceneService.GetAllProjectScenes();
                List<DeveloperQuickSceneReference> frameworkScenes = DeveloperQuickToolsSceneService.GetFrameworkScenes();
                if (!string.IsNullOrWhiteSpace(_sceneSearch))
                {
                    scenes = scenes.Where(scene =>
                            scene.DisplayName.IndexOf(_sceneSearch, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            scene.Path.IndexOf(_sceneSearch, StringComparison.OrdinalIgnoreCase) >= 0)
                        .ToList();
                    frameworkScenes = frameworkScenes.Where(scene =>
                            scene.DisplayName.IndexOf(_sceneSearch, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            scene.Path.IndexOf(_sceneSearch, StringComparison.OrdinalIgnoreCase) >= 0)
                        .ToList();
                }

                _sceneScroll = EditorGUILayout.BeginScrollView(_sceneScroll, GUILayout.MinHeight(120), GUILayout.MaxHeight(260));
                DrawSceneRows(scenes);

                if (frameworkScenes.Count > 0)
                {
                    GUILayout.Space(6);
                    EditorGUILayout.LabelField("StellarFramework 框架场景", EditorStyles.miniBoldLabel);
                    DrawSceneRows(frameworkScenes);
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawLocalUtilities()
        {
            Section("本机调试工具");
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("打开 persistentDataPath", GUILayout.Height(28)))
                    {
                        DeveloperQuickToolsSceneService.OpenPersistentDataPath();
                    }

                    if (GUILayout.Button("打开当前场景目录", GUILayout.Height(28)))
                    {
                        DeveloperQuickToolsSceneService.RevealCurrentSceneFolder();
                    }

                    if (DangerButton("清理 PlayerPrefs", GUILayout.Height(28)))
                    {
                        DeveloperQuickToolsSceneService.ClearPlayerPrefsWithConfirm();
                    }
                }

                if (GUILayout.Button("重置开发者快捷工具个人配置", GUILayout.Height(28)))
                {
                    if (EditorUtility.DisplayDialog("重置个人配置", "确定重置场景收藏、最近打开和倍速预设吗？", "重置", "取消"))
                    {
                        DeveloperQuickToolsStore.Reset();
                        EnsureSelectedGroup();
                    }
                }
            }
        }

        private static void DrawSceneReferenceRow(
            DeveloperQuickSceneReference scene,
            Action open,
            Action ping,
            Action removeOrFavorite,
            string removeLabel = "移除")
        {
            string path = DeveloperQuickToolsSceneService.ResolvePath(scene);
            bool valid = !string.IsNullOrEmpty(path);

            using (new GUILayout.HorizontalScope())
            {
                GUI.enabled = valid;
                if (GUILayout.Button(scene.DisplayName, EditorStyles.miniButtonLeft, GUILayout.Width(180)))
                {
                    open?.Invoke();
                }

                GUILayout.Label(valid ? path : $"{scene.Path}（已失效）", EditorStyles.miniLabel);

                if (GUILayout.Button("定位", EditorStyles.miniButtonMid, GUILayout.Width(48)))
                {
                    ping?.Invoke();
                }

                GUI.enabled = true;
                if (GUILayout.Button(removeLabel, EditorStyles.miniButtonRight, GUILayout.Width(54)))
                {
                    removeOrFavorite?.Invoke();
                }
            }
        }

        private void DrawSceneRows(List<DeveloperQuickSceneReference> scenes)
        {
            foreach (DeveloperQuickSceneReference scene in scenes)
            {
                bool isFavorite = DeveloperQuickToolsLogic.IsFavorite(DeveloperQuickToolsStore.Preferences, scene);
                DrawSceneReferenceRow(scene,
                    () => DeveloperQuickToolsSceneService.OpenScene(scene),
                    () => DeveloperQuickToolsSceneService.PingScene(DeveloperQuickToolsSceneService.ResolvePath(scene)),
                    () =>
                    {
                        if (isFavorite)
                        {
                            DeveloperQuickToolsSceneService.RemoveFavoriteScene(scene);
                        }
                        else
                        {
                            DeveloperQuickToolsSceneService.AddFavoriteScene(_selectedGroupName, DeveloperQuickToolsSceneService.ResolvePath(scene));
                        }
                    },
                    isFavorite ? "取消收藏" : "收藏");
            }
        }

        private void EnsureSelectedGroup()
        {
            DeveloperQuickToolsStore.Preferences.EnsureDefaults();
            if (DeveloperQuickToolsStore.Preferences.FavoriteGroups.All(group => group.Name != _selectedGroupName))
            {
                _selectedGroupName = DeveloperQuickToolsStore.Preferences.FavoriteGroups[0].Name;
            }
        }
    }
}
#endif
