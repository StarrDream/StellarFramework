using System;
using System.Collections;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace StellarFramework
{
    /// <summary>
    /// [底层] 配置加载核心
    /// 职责: 处理平台路径差异、Web请求、BOM清洗、JSON基础解析
    /// </summary>
    public static class ConfigCore
    {
        /// <summary>
        /// 通用加载流程，支持相对路径
        /// </summary>
        /// <param name="relativePath">配置文件相对路径 (如 Normal/ShopConfig.json)</param>
        /// <param name="onComplete">回调: (JObject数据, 是否来自用户存档/热更)</param>
        public static IEnumerator LoadConfigProcess(string relativePath, Action<JObject, bool> onComplete)
        {
            if (string.IsNullOrEmpty(relativePath))
            {
                LogKit.LogError($"[ConfigCore] 加载失败: 传入的相对路径为空");
                onComplete?.Invoke(null, false);
                yield break;
            }

            string loadUrl;
            bool isUserSave = false;

            // 1. 优先检查 PersistentDataPath (用户存档 或 热更文件)
            string userSavePath = Path.Combine(Application.persistentDataPath, relativePath);
            if (File.Exists(userSavePath))
            {
                loadUrl = "file://" + userSavePath.Replace("\\", "/");
                isUserSave = true;
            }
            else
            {
                // 2. 读取 StreamingAssets (包内默认)
                loadUrl = GetStreamingAssetsUrl(relativePath);
            }

            using (UnityWebRequest request = UnityWebRequest.Get(loadUrl))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    LogKit.LogError($"[ConfigCore] 请求失败: {loadUrl}\nError: {request.error}");
                    onComplete?.Invoke(null, false);
                    yield break;
                }

                string json = request.downloadHandler.text;

                // 清洗 BOM 头，防止 Newtonsoft 解析首字符报错
                if (!string.IsNullOrEmpty(json) && json[0] == '\uFEFF')
                {
                    json = json.Substring(1);
                }

                try
                {
                    JObject data = JObject.Parse(json);
                    onComplete?.Invoke(data, isUserSave);
                }
                catch (Exception ex)
                {
                    LogKit.LogError($"[ConfigCore] JSON 解析异常: {relativePath}\n{ex.Message}\n原始内容: {json}");
                    onComplete?.Invoke(null, false);
                }
            }
        }

        /// <summary>
        /// 获取适配各平台的 StreamingAssets WebRequest 路径
        /// </summary>
        public static string GetStreamingAssetsUrl(string relativePath)
        {
            relativePath = relativePath.TrimStart('/');
#if UNITY_EDITOR
            return "file://" + Path.Combine(Application.streamingAssetsPath, relativePath).Replace("\\", "/");
#elif UNITY_ANDROID || UNITY_WEBGL || UNITY_OPENHARMONY
            return Application.streamingAssetsPath + "/" + relativePath;
#else
            return "file://" + Path.Combine(Application.streamingAssetsPath, relativePath).Replace("\\", "/");
#endif
        }

        /// <summary>
        /// 获取用户存档/热更目录的物理路径 (用于 System.IO 读写)
        /// </summary>
        public static string GetPersistentPath(string relativePath)
        {
            return Path.Combine(Application.persistentDataPath, relativePath);
        }
    }
}