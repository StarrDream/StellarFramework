#if UNITY_EDITOR
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace StellarFramework.Editor
{
    public static class UrlConfigEditor
    {
        private const string FILENAME = StellarFramework.UrlConfig.CONFIG_NAME;
        private const string PREF_KEY = "UrlConfig_Env_Key";

        public static StellarFramework.UrlEnvironment CurrentEnv
        {
            get => (StellarFramework.UrlEnvironment)EditorPrefs.GetInt(PREF_KEY, (int)StellarFramework.UrlEnvironment.Dev);
            set => EditorPrefs.SetInt(PREF_KEY, (int)value);
        }

        public static string GetCurrentEnvLabel()
        {
            // 给 Hub 显示用，包含 PrefKey，方便你调试
            return $"{CurrentEnv} (EditorPrefs: {PREF_KEY}={(int)CurrentEnv})";
        }

        public static void SwitchToDev()
        {
            CurrentEnv = StellarFramework.UrlEnvironment.Dev;
            Debug.Log("[UrlConfigEditor] 已切换到 Dev（仅 EditorPrefs）");
        }

        public static void SwitchToRelease()
        {
            CurrentEnv = StellarFramework.UrlEnvironment.Release;
            Debug.Log("[UrlConfigEditor] 已切换到 Release（仅 EditorPrefs）");
        }

        public static void OpenConfigFile()
        {
            string path = Path.Combine(Application.streamingAssetsPath, FILENAME);
            if (File.Exists(path))
            {
                EditorUtility.RevealInFinder(path);
                Debug.Log($"[UrlConfigEditor] 打开配置文件: {path}");
            }
            else
            {
                Debug.LogError($"[UrlConfigEditor] 文件未找到: {path}");
            }
        }

        public static void GenerateDefaultConfig()
        {
            string streamingDir = Application.streamingAssetsPath;
            string path = Path.Combine(streamingDir, FILENAME);

            if (!Directory.Exists(streamingDir))
            {
                Directory.CreateDirectory(streamingDir);
                Debug.Log($"[UrlConfigEditor] 创建目录: {streamingDir}");
            }

            // 按 Hub 语义：直接覆盖
            if (File.Exists(path))
            {
                Debug.Log($"[UrlConfigEditor] 覆盖已存在配置: {path}");
            }

            try
            {
                var defaultJson = new JObject();
                defaultJson["ActiveProfile"] = "Dev";

                var envs = new JObject();
                envs["Dev"] = new JObject { ["ApiService"] = "http://127.0.0.1:80" };
                envs["Release"] = new JObject { ["ApiService"] = "http://server.com:80" };
                defaultJson["Environments"] = envs;

                var endpoints = new JObject();
                endpoints["Login"] = new JObject { ["Service"] = "ApiService", ["Path"] = "/api/login" };
                endpoints["GetUserInfo"] = new JObject { ["Service"] = "ApiService", ["Path"] = "/api/user/{uid}" };
                defaultJson["Endpoints"] = endpoints;

                // 使用无 BOM UTF8
                File.WriteAllText(path, defaultJson.ToString(Newtonsoft.Json.Formatting.Indented), new UTF8Encoding(false));

                AssetDatabase.Refresh();

                var assetPath = "Assets/StreamingAssets/" + FILENAME;
                var asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                if (asset) EditorGUIUtility.PingObject(asset);

                Debug.Log($"[UrlConfigEditor] 默认配置生成成功: {path}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[UrlConfigEditor] 生成失败: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }
    }
}
#endif