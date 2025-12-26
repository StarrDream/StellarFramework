#if UNITY_EDITOR
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace StellarFramework.Editor
{
    public static class AppConfigEditor
    {
        // 引用 Runtime 的常量，确保文件名一致
        private const string FILENAME = StellarFramework.AppConfig.CONFIG_NAME;

        /// <summary>
        /// 生成默认配置文件（写入 StreamingAssets）
        /// </summary>
        public static void GenerateDefaultConfig()
        {
            string streamingDir = Application.streamingAssetsPath;
            string path = Path.Combine(streamingDir, FILENAME);

            if (!Directory.Exists(streamingDir))
            {
                Directory.CreateDirectory(streamingDir);
                Debug.Log($"[AppConfigEditor] 创建目录: {streamingDir}");
            }

            // 这里按 Hub 的按钮语义：直接覆盖
            if (File.Exists(path))
            {
                Debug.Log($"[AppConfigEditor] 覆盖已存在配置: {path}");
            }

            try
            {
                JObject root = new JObject();
                root["AppVersion"] = Application.version;
                root["LogKitMode"] = true;

                JObject settings = new JObject();
                settings["Language"] = "zh-CN";
                settings["MasterVolume"] = 1.0f;
                root["GameSettings"] = settings;

                JObject features = new JObject();
                features["ShowFPS"] = true;
                root["Features"] = features;

                // 使用无 BOM UTF8
                File.WriteAllText(path, root.ToString(Newtonsoft.Json.Formatting.Indented), new UTF8Encoding(false));

                AssetDatabase.Refresh();

                // Ping 到 Project 里的 StreamingAssets 文件（如果路径在 Assets 下）
                var assetPath = "Assets/StreamingAssets/" + FILENAME;
                var asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                if (asset) EditorGUIUtility.PingObject(asset);

                Debug.Log($"[AppConfigEditor] 默认配置生成成功: {path}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[AppConfigEditor] 生成失败: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// 在系统文件管理器中打开默认配置文件（StreamingAssets）
        /// </summary>
        public static void OpenDefaultConfig()
        {
            string path = Path.Combine(Application.streamingAssetsPath, FILENAME);
            if (File.Exists(path))
            {
                EditorUtility.RevealInFinder(path);
                Debug.Log($"[AppConfigEditor] 打开默认配置: {path}");
            }
            else
            {
                Debug.LogError($"[AppConfigEditor] 文件不存在: {path}");
            }
        }

        /// <summary>
        /// 在系统文件管理器中打开本地存档配置文件（PersistentDataPath）
        /// </summary>
        public static void OpenSaveConfig()
        {
            string path = StellarFramework.ConfigCore.GetPersistentPath(FILENAME);
            if (File.Exists(path))
            {
                EditorUtility.RevealInFinder(path);
                Debug.Log($"[AppConfigEditor] 打开本地存档: {path}");
            }
            else
            {
                Debug.LogWarning($"[AppConfigEditor] 本地存档不存在: {path}");
                EditorUtility.RevealInFinder(Application.persistentDataPath);
            }
        }

        /// <summary>
        /// 删除本地存档配置文件（PersistentDataPath）
        /// </summary>
        public static void ClearSaveConfig()
        {
            string path = StellarFramework.ConfigCore.GetPersistentPath(FILENAME);
            if (File.Exists(path))
            {
                File.Delete(path);
                Debug.Log($"[AppConfigEditor] 本地存档已清除: {path}");
            }
            else
            {
                Debug.Log($"[AppConfigEditor] 本地无存档，无需清理: {path}");
            }

            AssetDatabase.Refresh();
        }
    }
}
#endif