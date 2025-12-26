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
        /// 通用加载流程
        /// </summary>
        /// <param name="fileName">配置文件名 (如 appConfig.json)</param>
        /// <param name="onComplete">回调: (JObject数据, 是否来自用户存档/热更)</param>
        public static IEnumerator LoadConfigProcess(string fileName, Action<JObject, bool> onComplete)
        {
            string loadUrl;
            bool isUserSave = false;

            // 1. 优先检查 PersistentDataPath (用户存档 或 热更文件)
            // 这里的逻辑是通用的：只要外部有文件，就读外部的，覆盖包内的
            string userSavePath = Path.Combine(Application.persistentDataPath, fileName);
            if (File.Exists(userSavePath))
            {
                loadUrl = "file://" + userSavePath.Replace("\\", "/");
                isUserSave = true;
            }
            else
            {
                // 2. 读取 StreamingAssets (包内默认)
                loadUrl = GetStreamingAssetsUrl(fileName);
            }

            LogKit.Log($"[ConfigCore] 开始加载: {fileName} | URL: {loadUrl}");

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

                // 清洗 BOM 头 (防止 Newtonsoft 解析首字符报错)
                // 很多文本编辑器保存UTF8时会带BOM，导致JSON解析第一行报错
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
                    LogKit.LogError($"[ConfigCore] JSON 解析异常: {fileName}\n{ex.Message}\n原始内容: {json}");
                    onComplete?.Invoke(null, false);
                }
            }
        }

        /// <summary>
        /// 获取适配各平台的 StreamingAssets WebRequest 路径
        /// </summary>
        public static string GetStreamingAssetsUrl(string fileName)
        {
            // 移除开头的斜杠，防止路径拼接出现双斜杠
            fileName = fileName.TrimStart('/');

            // 1. 编辑器环境 (优先处理，方便调试)
#if UNITY_EDITOR
            // 编辑器下 Application.streamingAssetsPath 返回的是工程目录的物理路径
            // 需要加 file:// 协议，并且处理 Windows 的反斜杠
            return "file://" + Path.Combine(Application.streamingAssetsPath, fileName).Replace("\\", "/");

            // 2. 自带协议头的平台 (Android / WebGL / HarmonyOS)
#elif UNITY_ANDROID || UNITY_WEBGL || UNITY_OPENHARMONY
            // Android:     jar:file:///data/app/...
            // WebGL:       http://localhost/...
            // HarmonyOS:   rawfile://...
            // 这些平台返回的路径已经包含了协议头，直接拼接即可，不要加 file://
            return Application.streamingAssetsPath + "/" + fileName;

            // 3. 需要手动加 file:// 的平台 (iOS / Windows / Mac / Linux)
#else
            // iOS:         /var/containers/Bundle/...
            // Windows:     C:/Program Files/...
            // Mac/Linux:   /Users/...
            // 这些平台返回的是绝对物理路径，UnityWebRequest 需要 file:// 协议才能读取
            return "file://" + Path.Combine(Application.streamingAssetsPath, fileName).Replace("\\", "/");
#endif
        }


        /// <summary>
        /// 获取用户存档/热更目录的物理路径 (用于 System.IO 读写)
        /// </summary>
        public static string GetPersistentPath(string fileName)
        {
            return Path.Combine(Application.persistentDataPath, fileName);
        }
    }
}