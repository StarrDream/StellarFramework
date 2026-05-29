#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace StellarFramework.Editor.DevTools
{
    [Serializable]
    public sealed class DeveloperQuickSceneReference
    {
        public string Guid;
        public string Path;
        public string Name;

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(Name)) return Name;
                if (!string.IsNullOrEmpty(Path)) return System.IO.Path.GetFileNameWithoutExtension(Path);
                return "未命名场景";
            }
        }
    }

    [Serializable]
    public sealed class DeveloperQuickSceneGroup
    {
        public string Name = DeveloperQuickToolsStore.DefaultFavoriteGroupName;
        public List<DeveloperQuickSceneReference> Scenes = new List<DeveloperQuickSceneReference>();
    }

    [Serializable]
    public sealed class DeveloperQuickToolsPreferences
    {
        public bool CompactToolbar;
        public int RecentLimit = 12;
        public float SelectedTimeScale = 1f;
        public bool SyncFixedDeltaTime = true;
        public List<float> TimeScalePresets = new List<float>();
        public List<DeveloperQuickSceneReference> RecentScenes = new List<DeveloperQuickSceneReference>();
        public List<DeveloperQuickSceneGroup> FavoriteGroups = new List<DeveloperQuickSceneGroup>();

        public void EnsureDefaults()
        {
            RecentLimit = Mathf.Clamp(RecentLimit <= 0 ? 12 : RecentLimit, 3, 50);
            SelectedTimeScale = DeveloperQuickToolsLogic.ClampTimeScale(SelectedTimeScale);
            SyncFixedDeltaTime = true;

            if (TimeScalePresets == null) TimeScalePresets = new List<float>();
            if (TimeScalePresets.Count == 0)
            {
                TimeScalePresets.AddRange(DeveloperQuickToolsLogic.DefaultTimeScalePresets);
            }

            TimeScalePresets = TimeScalePresets
                .Select(DeveloperQuickToolsLogic.ClampTimeScale)
                .Distinct()
                .OrderBy(value => value)
                .ToList();

            if (RecentScenes == null) RecentScenes = new List<DeveloperQuickSceneReference>();
            RecentScenes = DeveloperQuickToolsLogic.SanitizeSceneList(RecentScenes)
                .Take(RecentLimit)
                .ToList();

            if (FavoriteGroups == null) FavoriteGroups = new List<DeveloperQuickSceneGroup>();
            if (FavoriteGroups.Count == 0)
            {
                FavoriteGroups.Add(new DeveloperQuickSceneGroup
                {
                    Name = DeveloperQuickToolsStore.DefaultFavoriteGroupName
                });
            }

            foreach (DeveloperQuickSceneGroup group in FavoriteGroups)
            {
                if (string.IsNullOrWhiteSpace(group.Name)) group.Name = DeveloperQuickToolsStore.DefaultFavoriteGroupName;
                if (group.Scenes == null) group.Scenes = new List<DeveloperQuickSceneReference>();
                group.Scenes = DeveloperQuickToolsLogic.SanitizeSceneList(group.Scenes).ToList();
            }
        }
    }

    public static class DeveloperQuickToolsLogic
    {
        public static readonly float[] DefaultTimeScalePresets =
            { 0f, 0.1f, 0.5f, 1f, 2f, 5f, 10f, 30f, 60f, 100f };

        public static readonly float[] CompactTimeScalePresets =
            { 0f, 1f, 5f, 10f, 100f };

        public static float ClampTimeScale(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 1f;
            return Mathf.Clamp(value, 0f, 100f);
        }

        public static float CalculateFixedDeltaTime(float baseFixedDeltaTime, float timeScale)
        {
            float safeBase = baseFixedDeltaTime > 0f ? baseFixedDeltaTime : 0.02f;
            float safeScale = ClampTimeScale(timeScale);
            if (safeScale <= 0f) return safeBase;
            return Mathf.Clamp(safeBase * safeScale, 0.0001f, 10f);
        }

        public static void AddOrMoveRecent(List<DeveloperQuickSceneReference> scenes, DeveloperQuickSceneReference scene, int limit)
        {
            if (scenes == null || scene == null) return;

            scenes.RemoveAll(item => IsSameScene(item, scene));
            scenes.Insert(0, scene);

            int safeLimit = Mathf.Clamp(limit <= 0 ? 12 : limit, 3, 50);
            if (scenes.Count > safeLimit)
            {
                scenes.RemoveRange(safeLimit, scenes.Count - safeLimit);
            }
        }

        public static bool IsSameScene(DeveloperQuickSceneReference left, DeveloperQuickSceneReference right)
        {
            if (left == null || right == null) return false;

            if (!string.IsNullOrEmpty(left.Guid) &&
                !string.IsNullOrEmpty(right.Guid) &&
                string.Equals(left.Guid, right.Guid, StringComparison.Ordinal))
            {
                return true;
            }

            return !string.IsNullOrEmpty(left.Path) &&
                   !string.IsNullOrEmpty(right.Path) &&
                   string.Equals(NormalizeAssetPath(left.Path), NormalizeAssetPath(right.Path), StringComparison.OrdinalIgnoreCase);
        }

        public static IEnumerable<DeveloperQuickSceneReference> SanitizeSceneList(IEnumerable<DeveloperQuickSceneReference> scenes)
        {
            if (scenes == null) yield break;

            var seen = new List<DeveloperQuickSceneReference>();
            foreach (DeveloperQuickSceneReference scene in scenes)
            {
                if (scene == null) continue;
                if (string.IsNullOrEmpty(scene.Guid) && string.IsNullOrEmpty(scene.Path)) continue;
                if (seen.Any(item => IsSameScene(item, scene))) continue;

                scene.Path = NormalizeAssetPath(scene.Path);
                if (string.IsNullOrEmpty(scene.Name) && !string.IsNullOrEmpty(scene.Path))
                {
                    scene.Name = System.IO.Path.GetFileNameWithoutExtension(scene.Path);
                }

                seen.Add(scene);
                yield return scene;
            }
        }

        public static bool IsFavorite(DeveloperQuickToolsPreferences preferences, DeveloperQuickSceneReference scene)
        {
            if (preferences == null || scene == null || preferences.FavoriteGroups == null) return false;

            return preferences.FavoriteGroups
                .Where(group => group?.Scenes != null)
                .SelectMany(group => group.Scenes)
                .Any(item => IsSameScene(item, scene));
        }

        public static bool ToggleFavorite(DeveloperQuickToolsPreferences preferences, DeveloperQuickSceneReference scene)
        {
            if (preferences == null || scene == null) return false;
            preferences.EnsureDefaults();

            int removed = 0;
            foreach (DeveloperQuickSceneGroup group in preferences.FavoriteGroups)
            {
                if (group?.Scenes == null) continue;
                removed += group.Scenes.RemoveAll(item => IsSameScene(item, scene));
            }

            if (removed > 0) return false;

            DeveloperQuickSceneGroup defaultGroup = preferences.FavoriteGroups
                .FirstOrDefault(group => string.Equals(group.Name, DeveloperQuickToolsStore.DefaultFavoriteGroupName, StringComparison.Ordinal))
                ?? preferences.FavoriteGroups.First();
            defaultGroup.Scenes.Add(scene);
            return true;
        }

        public static int RemoveMissingScenes(DeveloperQuickToolsPreferences preferences, Func<DeveloperQuickSceneReference, bool> exists)
        {
            if (preferences == null || exists == null) return 0;
            preferences.EnsureDefaults();

            int removed = 0;
            removed += preferences.RecentScenes.RemoveAll(scene => scene == null || !exists(scene));

            foreach (DeveloperQuickSceneGroup group in preferences.FavoriteGroups)
            {
                if (group?.Scenes == null) continue;
                removed += group.Scenes.RemoveAll(scene => scene == null || !exists(scene));
            }

            return removed;
        }

        public static bool ShouldIncludeSceneInQuickList(string scenePath)
        {
            string normalizedPath = NormalizeAssetPath(scenePath);
            if (string.IsNullOrEmpty(normalizedPath)) return false;
            if (normalizedPath.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase)) return false;

            string[] parts = normalizedPath.Split('/');
            return parts.All(part => !part.EndsWith("~", StringComparison.Ordinal));
        }

        public static bool IsFrameworkScene(string scenePath)
        {
            string normalizedPath = NormalizeAssetPath(scenePath);
            return normalizedPath.StartsWith("Assets/StellarFramework/", StringComparison.OrdinalIgnoreCase);
        }

        public static string BuildSceneMenuLabel(
            string root,
            DeveloperQuickSceneReference scene,
            bool groupByFolder,
            bool isMissing)
        {
            string displayName = scene?.DisplayName ?? "未命名场景";
            if (!groupByFolder)
            {
                return isMissing ? $"{root}/{displayName}（已失效）" : $"{root}/{displayName}";
            }

            string hint = BuildSceneLocationHint(scene?.Path);
            string label = string.IsNullOrEmpty(hint)
                ? $"{root}/{displayName}"
                : $"{root}/{displayName}  ({hint})";

            return isMissing ? $"{label}（已失效）" : label;
        }

        private static string BuildSceneLocationHint(string scenePath)
        {
            string normalizedPath = NormalizeAssetPath(scenePath);
            if (string.IsNullOrEmpty(normalizedPath)) return string.Empty;

            string folder = Path.GetDirectoryName(normalizedPath)?.Replace('\\', '/') ?? string.Empty;
            if (string.IsNullOrEmpty(folder)) return string.Empty;

            if (folder.StartsWith("Assets/StellarFramework/", StringComparison.OrdinalIgnoreCase))
            {
                return "框架 > " + folder.Substring("Assets/StellarFramework/".Length).Replace("/", " > ");
            }

            if (folder.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return "项目 > " + folder.Substring("Assets/".Length).Replace("/", " > ");
            }

            return folder.Replace("/", " > ");
        }

        public static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
        }
    }

    public static class DeveloperQuickToolsStore
    {
        public const string DefaultFavoriteGroupName = "常用场景";

        private const string PreferencesKeyPrefix = "StellarFramework.DevTools.Preferences.";

        private static DeveloperQuickToolsPreferences _preferences;

        public static DeveloperQuickToolsPreferences Preferences
        {
            get
            {
                if (_preferences == null)
                {
                    _preferences = LoadPreferences();
                }

                return _preferences;
            }
        }

        public static void Save()
        {
            Preferences.EnsureDefaults();
            EditorPrefs.SetString(PreferencesKey, JsonUtility.ToJson(Preferences));
        }

        public static void Reload()
        {
            _preferences = LoadPreferences();
        }

        public static void Reset()
        {
            EditorPrefs.DeleteKey(PreferencesKey);
            _preferences = LoadPreferences();
            Save();
        }

        public static DeveloperQuickSceneGroup GetOrCreateGroup(string groupName)
        {
            string safeName = string.IsNullOrWhiteSpace(groupName) ? DefaultFavoriteGroupName : groupName.Trim();
            DeveloperQuickSceneGroup group = Preferences.FavoriteGroups
                .FirstOrDefault(item => string.Equals(item.Name, safeName, StringComparison.Ordinal));

            if (group != null) return group;

            group = new DeveloperQuickSceneGroup { Name = safeName };
            Preferences.FavoriteGroups.Add(group);
            Save();
            return group;
        }

        public static bool RemoveGroup(string groupName)
        {
            if (string.IsNullOrWhiteSpace(groupName)) return false;
            int removed = Preferences.FavoriteGroups.RemoveAll(item => string.Equals(item.Name, groupName, StringComparison.Ordinal));

            if (Preferences.FavoriteGroups.Count == 0)
            {
                Preferences.FavoriteGroups.Add(new DeveloperQuickSceneGroup { Name = DefaultFavoriteGroupName });
            }

            if (removed > 0) Save();
            return removed > 0;
        }

        private static DeveloperQuickToolsPreferences LoadPreferences()
        {
            DeveloperQuickToolsPreferences result = null;
            string json = EditorPrefs.GetString(PreferencesKey, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    result = JsonUtility.FromJson<DeveloperQuickToolsPreferences>(json);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[星河开发工具] 个人配置读取失败，已使用默认配置。原因：{exception.Message}");
                }
            }

            if (result == null) result = new DeveloperQuickToolsPreferences();
            result.EnsureDefaults();
            return result;
        }

        public static string ProjectKeySuffix
        {
            get
            {
                string projectPath = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
                return ComputeStableHash(projectPath.Replace('\\', '/').ToLowerInvariant());
            }
        }

        private static string PreferencesKey
        {
            get { return PreferencesKeyPrefix + ProjectKeySuffix; }
        }

        private static string ComputeStableHash(string value)
        {
            using (SHA1 sha1 = SHA1.Create())
            {
                byte[] bytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
                var builder = new StringBuilder(bytes.Length * 2);
                foreach (byte b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }

                return builder.ToString();
            }
        }
    }

    public static class DeveloperQuickToolsTime
    {
        private const string SessionBaseFixedDeltaKeyPrefix = "StellarFramework.DevTools.SessionBaseFixedDeltaTime.";

        public static string CurrentLabel
        {
            get { return FormatTimeScale(DeveloperQuickToolsStore.Preferences.SelectedTimeScale); }
        }

        public static void SetTimeScale(float value, bool savePreference)
        {
            DeveloperQuickToolsPreferences preferences = DeveloperQuickToolsStore.Preferences;
            preferences.SelectedTimeScale = DeveloperQuickToolsLogic.ClampTimeScale(value);

            if (savePreference)
            {
                DeveloperQuickToolsStore.Save();
            }

            ApplyConfiguredTimeScale();
        }

        public static void ApplyConfiguredTimeScale()
        {
            float scale = DeveloperQuickToolsLogic.ClampTimeScale(DeveloperQuickToolsStore.Preferences.SelectedTimeScale);
            Time.timeScale = scale;

            if (DeveloperQuickToolsStore.Preferences.SyncFixedDeltaTime)
            {
                float baseFixedDeltaTime = GetSessionBaseFixedDeltaTime();
                Time.fixedDeltaTime = DeveloperQuickToolsLogic.CalculateFixedDeltaTime(baseFixedDeltaTime, scale);
            }

            Debug.Log($"[星河开发工具] 已设置调试倍速：{FormatTimeScale(scale)}，fixedDeltaTime={Time.fixedDeltaTime:0.####}");
        }

        public static void CaptureSessionBaseFixedDeltaTime()
        {
            if (!EditorPrefs.HasKey(SessionBaseFixedDeltaKey))
            {
                EditorPrefs.SetFloat(SessionBaseFixedDeltaKey, Time.fixedDeltaTime);
            }
        }

        public static void RestoreDefaultTimeScale()
        {
            float baseFixedDeltaTime = GetSessionBaseFixedDeltaTime();
            Time.timeScale = 1f;
            Time.fixedDeltaTime = baseFixedDeltaTime;
            EditorPrefs.DeleteKey(SessionBaseFixedDeltaKey);

            DeveloperQuickToolsStore.Preferences.SelectedTimeScale = 1f;
            DeveloperQuickToolsStore.Save();

            Debug.Log($"[星河开发工具] 已恢复默认倍速：1x，fixedDeltaTime={Time.fixedDeltaTime:0.####}");
        }

        public static string FormatTimeScale(float value)
        {
            float scale = DeveloperQuickToolsLogic.ClampTimeScale(value);
            return Mathf.Approximately(scale, Mathf.Round(scale))
                ? $"{Mathf.RoundToInt(scale)}x"
                : $"{scale:0.##}x";
        }

        private static string SessionBaseFixedDeltaKey
        {
            get { return SessionBaseFixedDeltaKeyPrefix + DeveloperQuickToolsStore.ProjectKeySuffix; }
        }

        private static float GetSessionBaseFixedDeltaTime()
        {
            if (EditorPrefs.HasKey(SessionBaseFixedDeltaKey))
            {
                return EditorPrefs.GetFloat(SessionBaseFixedDeltaKey, 0.02f);
            }

            float current = Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : 0.02f;
            EditorPrefs.SetFloat(SessionBaseFixedDeltaKey, current);
            return current;
        }
    }

    public static class DeveloperQuickToolsSceneService
    {
        public static string CurrentScenePath
        {
            get { return SceneManager.GetActiveScene().path; }
        }

        public static string CurrentSceneName
        {
            get
            {
                Scene scene = SceneManager.GetActiveScene();
                return string.IsNullOrEmpty(scene.name) ? "未保存场景" : scene.name;
            }
        }

        public static DeveloperQuickSceneReference CreateReference(string scenePath)
        {
            string normalizedPath = DeveloperQuickToolsLogic.NormalizeAssetPath(scenePath);
            return new DeveloperQuickSceneReference
            {
                Guid = AssetDatabase.AssetPathToGUID(normalizedPath),
                Path = normalizedPath,
                Name = string.IsNullOrEmpty(normalizedPath) ? string.Empty : Path.GetFileNameWithoutExtension(normalizedPath)
            };
        }

        public static string ResolvePath(DeveloperQuickSceneReference scene)
        {
            if (scene == null) return string.Empty;

            if (!string.IsNullOrEmpty(scene.Guid))
            {
                string guidPath = AssetDatabase.GUIDToAssetPath(scene.Guid);
                if (IsValidSceneAsset(guidPath)) return guidPath;
            }

            return IsValidSceneAsset(scene.Path) ? DeveloperQuickToolsLogic.NormalizeAssetPath(scene.Path) : string.Empty;
        }

        public static void RecordRecentScene(string scenePath)
        {
            if (!IsValidSceneAsset(scenePath)) return;

            CleanupMissingSceneReferences(false);
            DeveloperQuickToolsPreferences preferences = DeveloperQuickToolsStore.Preferences;
            DeveloperQuickToolsLogic.AddOrMoveRecent(preferences.RecentScenes, CreateReference(scenePath), preferences.RecentLimit);
            DeveloperQuickToolsStore.Save();
        }

        public static bool OpenScene(string scenePath)
        {
            string normalizedPath = DeveloperQuickToolsLogic.NormalizeAssetPath(scenePath);
            if (!IsValidSceneAsset(normalizedPath))
            {
                Debug.LogWarning($"[星河开发工具] 场景不存在或不是有效 SceneAsset：{normalizedPath}");
                return false;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[星河开发工具] 用户取消切换场景。");
                return false;
            }

            Scene openedScene = EditorSceneManager.OpenScene(normalizedPath, OpenSceneMode.Single);
            if (!openedScene.IsValid())
            {
                Debug.LogError($"[星河开发工具] 场景打开失败：{normalizedPath}");
                return false;
            }

            RecordRecentScene(normalizedPath);
            Debug.Log($"[星河开发工具] 已打开场景：{normalizedPath}");
            return true;
        }

        public static bool OpenScene(DeveloperQuickSceneReference scene)
        {
            string resolvedPath = ResolvePath(scene);
            return OpenScene(resolvedPath);
        }

        public static bool ReloadActiveScene()
        {
            if (string.IsNullOrEmpty(CurrentScenePath))
            {
                Debug.LogWarning("[星河开发工具] 当前场景尚未保存，无法重新打开。");
                return false;
            }

            return OpenScene(CurrentScenePath);
        }

        public static bool ToggleCurrentSceneFavorite()
        {
            if (!IsValidSceneAsset(CurrentScenePath))
            {
                Debug.LogWarning("[星河开发工具] 当前场景尚未保存，不能切换收藏状态。");
                return false;
            }

            DeveloperQuickSceneReference reference = CreateReference(CurrentScenePath);
            bool added = DeveloperQuickToolsLogic.ToggleFavorite(DeveloperQuickToolsStore.Preferences, reference);
            DeveloperQuickToolsStore.Save();
            Debug.Log(added
                ? $"[星河开发工具] 已收藏场景：{reference.Path}"
                : $"[星河开发工具] 已取消收藏场景：{reference.Path}");
            return added;
        }

        public static bool IsCurrentSceneFavorite()
        {
            return IsValidSceneAsset(CurrentScenePath) &&
                   DeveloperQuickToolsLogic.IsFavorite(DeveloperQuickToolsStore.Preferences, CreateReference(CurrentScenePath));
        }

        public static bool AddFavoriteScene(string groupName, string scenePath)
        {
            if (!IsValidSceneAsset(scenePath))
            {
                Debug.LogWarning($"[星河开发工具] 不能收藏无效场景：{scenePath}");
                return false;
            }

            DeveloperQuickSceneGroup group = DeveloperQuickToolsStore.GetOrCreateGroup(groupName);
            DeveloperQuickSceneReference reference = CreateReference(scenePath);
            if (group.Scenes.Any(item => DeveloperQuickToolsLogic.IsSameScene(item, reference)))
            {
                Debug.Log($"[星河开发工具] 场景已经在收藏分组中：{group.Name} / {reference.DisplayName}");
                return false;
            }

            group.Scenes.Add(reference);
            DeveloperQuickToolsStore.Save();
            Debug.Log($"[星河开发工具] 已收藏场景：{group.Name} / {reference.Path}");
            return true;
        }

        public static bool RemoveFavoriteScene(string groupName, DeveloperQuickSceneReference scene)
        {
            DeveloperQuickSceneGroup group = DeveloperQuickToolsStore.Preferences.FavoriteGroups
                .FirstOrDefault(item => string.Equals(item.Name, groupName, StringComparison.Ordinal));
            if (group == null || scene == null) return false;

            int removed = group.Scenes.RemoveAll(item => DeveloperQuickToolsLogic.IsSameScene(item, scene));
            if (removed > 0)
            {
                DeveloperQuickToolsStore.Save();
            }

            return removed > 0;
        }

        public static bool RemoveFavoriteScene(DeveloperQuickSceneReference scene)
        {
            if (scene == null) return false;

            int removed = 0;
            foreach (DeveloperQuickSceneGroup group in DeveloperQuickToolsStore.Preferences.FavoriteGroups)
            {
                if (group?.Scenes == null) continue;
                removed += group.Scenes.RemoveAll(item => DeveloperQuickToolsLogic.IsSameScene(item, scene));
            }

            if (removed > 0)
            {
                DeveloperQuickToolsStore.Save();
            }

            return removed > 0;
        }

        public static List<DeveloperQuickSceneReference> GetAllProjectScenes()
        {
            return GetAllQuickScenes()
                .Where(scene => !DeveloperQuickToolsLogic.IsFrameworkScene(scene.Path))
                .ToList();
        }

        public static List<DeveloperQuickSceneReference> GetFrameworkScenes()
        {
            return GetAllQuickScenes()
                .Where(scene => DeveloperQuickToolsLogic.IsFrameworkScene(scene.Path))
                .ToList();
        }

        private static List<DeveloperQuickSceneReference> GetAllQuickScenes()
        {
            return AssetDatabase.FindAssets("t:Scene")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(IsValidSceneAsset)
                .Where(DeveloperQuickToolsLogic.ShouldIncludeSceneInQuickList)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(CreateReference)
                .ToList();
        }

        public static List<DeveloperQuickSceneReference> GetBuildSettingScenes()
        {
            return EditorBuildSettings.scenes
                .Where(scene => !string.IsNullOrEmpty(scene.path))
                .Select(scene => scene.path)
                .Where(IsValidSceneAsset)
                .Where(DeveloperQuickToolsLogic.ShouldIncludeSceneInQuickList)
                .Select(CreateReference)
                .ToList();
        }

        public static void PingScene(string scenePath)
        {
            Object sceneAsset = AssetDatabase.LoadAssetAtPath<Object>(scenePath);
            if (sceneAsset == null)
            {
                Debug.LogWarning($"[星河开发工具] 找不到场景资源：{scenePath}");
                return;
            }

            Selection.activeObject = sceneAsset;
            EditorGUIUtility.PingObject(sceneAsset);
        }

        public static void PingCurrentScene()
        {
            if (string.IsNullOrEmpty(CurrentScenePath))
            {
                Debug.LogWarning("[星河开发工具] 当前场景尚未保存，无法定位资源。");
                return;
            }

            PingScene(CurrentScenePath);
        }

        public static void RevealCurrentSceneFolder()
        {
            if (string.IsNullOrEmpty(CurrentScenePath))
            {
                Debug.LogWarning("[星河开发工具] 当前场景尚未保存，无法打开所在目录。");
                return;
            }

            Object sceneAsset = AssetDatabase.LoadAssetAtPath<Object>(CurrentScenePath);
            if (sceneAsset != null) EditorUtility.RevealInFinder(AssetDatabase.GetAssetPath(sceneAsset));
        }

        public static void OpenPersistentDataPath()
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            EditorUtility.RevealInFinder(Application.persistentDataPath);
        }

        public static void ClearPlayerPrefsWithConfirm()
        {
            if (!EditorUtility.DisplayDialog("清理 PlayerPrefs", "确定清理当前工程的 PlayerPrefs 吗？这个操作只影响本机调试数据。", "清理", "取消"))
            {
                return;
            }

            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("[星河开发工具] 已清理本机 PlayerPrefs。");
        }

        public static void BuildFullMenu(GenericMenu menu)
        {
            CleanupMissingSceneReferences(true);

            menu.AddDisabledItem(new GUIContent($"当前场景/{CurrentSceneName}"));
            menu.AddSeparator(string.Empty);

            AddSceneMenu(menu, "所有场景", GetAllProjectScenes(), true);
            AddSceneMenu(menu, "最近打开", DeveloperQuickToolsStore.Preferences.RecentScenes);
            AddFavoriteMenus(menu);
            AddSceneMenu(menu, "Build Settings", GetBuildSettingScenes());

            menu.AddSeparator(string.Empty);
            BuildTimeScaleMenu(menu, "倍速");

            menu.AddSeparator(string.Empty);
            bool isCurrentSceneFavorite = IsCurrentSceneFavorite();
            menu.AddItem(new GUIContent(isCurrentSceneFavorite ? "当前场景/取消收藏当前场景" : "当前场景/收藏当前场景"), isCurrentSceneFavorite,
                () => ToggleCurrentSceneFavorite());
            menu.AddItem(new GUIContent("当前场景/重新打开当前场景"), false, () => ReloadActiveScene());
            menu.AddItem(new GUIContent("当前场景/定位当前场景资源"), false, () => PingCurrentScene());
            menu.AddItem(new GUIContent("工具/打开 Tools Hub"), false, StellarFrameworkTools.ShowWindow);
            menu.AddItem(new GUIContent("工具/打开 persistentDataPath"), false, OpenPersistentDataPath);
            menu.AddItem(new GUIContent("工具/清理 PlayerPrefs"), false, ClearPlayerPrefsWithConfirm);
            menu.AddItem(new GUIContent("显示模式/紧凑模式"), DeveloperQuickToolsStore.Preferences.CompactToolbar, ToggleCompactToolbar);
        }

        public static void BuildSceneMenu(GenericMenu menu)
        {
            CleanupMissingSceneReferences(true);

            AddSceneMenu(menu, "所有场景", GetAllProjectScenes(), true);
            AddSceneMenu(menu, "最近打开", DeveloperQuickToolsStore.Preferences.RecentScenes);
            AddFavoriteMenus(menu);
            AddSceneMenu(menu, "Build Settings", GetBuildSettingScenes());

            menu.AddSeparator(string.Empty);
            bool isCurrentSceneFavorite = IsCurrentSceneFavorite();
            menu.AddItem(new GUIContent(isCurrentSceneFavorite ? "取消收藏当前场景" : "收藏当前场景"), isCurrentSceneFavorite,
                () => ToggleCurrentSceneFavorite());
            menu.AddItem(new GUIContent("重新打开当前场景"), false, () => ReloadActiveScene());
            menu.AddItem(new GUIContent("定位当前场景资源"), false, () => PingCurrentScene());
        }

        public static void BuildDirectSceneSwitchMenu(GenericMenu menu)
        {
            CleanupMissingSceneReferences(true);

            AddSceneMenu(menu, "最近打开", DeveloperQuickToolsStore.Preferences.RecentScenes);
            AddFavoriteMenus(menu);
            AddSceneMenu(menu, "Build Settings", GetBuildSettingScenes());

            menu.AddSeparator(string.Empty);
            AddSceneItems(menu, GetAllProjectScenes(), true);
            menu.AddSeparator(string.Empty);
            AddSceneMenu(menu, "StellarFramework", GetFrameworkScenes(), true);
        }

        public static void BuildTimeScaleMenu(GenericMenu menu, string root)
        {
            foreach (float preset in DeveloperQuickToolsStore.Preferences.TimeScalePresets)
            {
                float capturedPreset = preset;
                menu.AddItem(new GUIContent($"{root}/{DeveloperQuickToolsTime.FormatTimeScale(capturedPreset)}"),
                    Mathf.Approximately(DeveloperQuickToolsStore.Preferences.SelectedTimeScale, capturedPreset),
                    () => DeveloperQuickToolsTime.SetTimeScale(capturedPreset, true));
            }

            menu.AddSeparator(root + "/");
            foreach (float preset in DeveloperQuickToolsLogic.CompactTimeScalePresets)
            {
                float capturedPreset = preset;
                menu.AddItem(new GUIContent($"{root}/常用/{DeveloperQuickToolsTime.FormatTimeScale(capturedPreset)}"), false,
                    () => DeveloperQuickToolsTime.SetTimeScale(capturedPreset, true));
            }
        }

        public static void ToggleCompactToolbar()
        {
            DeveloperQuickToolsStore.Preferences.CompactToolbar = !DeveloperQuickToolsStore.Preferences.CompactToolbar;
            DeveloperQuickToolsStore.Save();
            SceneView.RepaintAll();
        }

        public static bool IsValidSceneAsset(string scenePath)
        {
            string normalizedPath = DeveloperQuickToolsLogic.NormalizeAssetPath(scenePath);
            return !string.IsNullOrEmpty(normalizedPath) &&
                   normalizedPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) &&
                   AssetDatabase.LoadAssetAtPath<SceneAsset>(normalizedPath) != null;
        }

        public static int CleanupMissingSceneReferences(bool logResult)
        {
            int removed = DeveloperQuickToolsLogic.RemoveMissingScenes(
                DeveloperQuickToolsStore.Preferences,
                scene =>
                {
                    string resolvedPath = ResolvePath(scene);
                    return !string.IsNullOrEmpty(resolvedPath) &&
                           DeveloperQuickToolsLogic.ShouldIncludeSceneInQuickList(resolvedPath);
                });

            if (removed <= 0) return 0;

            DeveloperQuickToolsStore.Save();
            if (logResult)
            {
                Debug.Log($"[星河开发工具] 已清理 {removed} 条已删除或失效的场景记录。");
            }

            return removed;
        }

        private static void AddFavoriteMenus(GenericMenu menu)
        {
            foreach (DeveloperQuickSceneGroup group in DeveloperQuickToolsStore.Preferences.FavoriteGroups)
            {
                if (group == null || group.Scenes == null || group.Scenes.Count == 0)
                {
                    continue;
                }

                AddSceneMenu(menu, "收藏", group.Scenes);
            }
        }

        private static void AddSceneMenu(
            GenericMenu menu,
            string root,
            IEnumerable<DeveloperQuickSceneReference> scenes,
            bool groupByFolder = false)
        {
            List<DeveloperQuickSceneReference> sceneList = scenes == null
                ? new List<DeveloperQuickSceneReference>()
                : scenes.ToList();

            if (sceneList.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent(root + "/暂无场景"));
                return;
            }

            foreach (DeveloperQuickSceneReference scene in sceneList)
            {
                string resolvedPath = ResolvePath(scene);
                string label = DeveloperQuickToolsLogic.BuildSceneMenuLabel(
                    root,
                    scene,
                    groupByFolder,
                    string.IsNullOrEmpty(resolvedPath));

                if (string.IsNullOrEmpty(resolvedPath))
                {
                    menu.AddDisabledItem(new GUIContent(label));
                }
                else
                {
                    string path = resolvedPath;
                    menu.AddItem(new GUIContent(label), string.Equals(path, CurrentScenePath, StringComparison.Ordinal),
                        () => OpenScene(path));
                }
            }
        }

        private static void AddSceneItems(
            GenericMenu menu,
            IEnumerable<DeveloperQuickSceneReference> scenes,
            bool showLocationHint)
        {
            List<DeveloperQuickSceneReference> sceneList = scenes == null
                ? new List<DeveloperQuickSceneReference>()
                : scenes.ToList();

            if (sceneList.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("暂无业务场景"));
                return;
            }

            foreach (DeveloperQuickSceneReference scene in sceneList)
            {
                string resolvedPath = ResolvePath(scene);
                string label = DeveloperQuickToolsLogic.BuildSceneMenuLabel(
                    string.Empty,
                    scene,
                    showLocationHint,
                    string.IsNullOrEmpty(resolvedPath)).TrimStart('/');

                if (string.IsNullOrEmpty(resolvedPath))
                {
                    menu.AddDisabledItem(new GUIContent(label));
                }
                else
                {
                    string path = resolvedPath;
                    menu.AddItem(new GUIContent(label), string.Equals(path, CurrentScenePath, StringComparison.Ordinal),
                        () => OpenScene(path));
                }
            }
        }
    }

    [InitializeOnLoad]
    internal static class DeveloperQuickToolsEditorHooks
    {
        static DeveloperQuickToolsEditorHooks()
        {
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (scene.IsValid() && !string.IsNullOrEmpty(scene.path))
            {
                DeveloperQuickToolsSceneService.RecordRecentScene(scene.path);
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    DeveloperQuickToolsTime.CaptureSessionBaseFixedDeltaTime();
                    break;
                case PlayModeStateChange.EnteredPlayMode:
                    DeveloperQuickToolsTime.ApplyConfiguredTimeScale();
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    DeveloperQuickToolsTime.RestoreDefaultTimeScale();
                    break;
            }
        }
    }
}
#endif
