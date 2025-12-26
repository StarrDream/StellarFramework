// ========== SceneLoader.cs ==========
// Path: Assets/StellarFramework/Runtime/Tools/Utils/SceneLoader.cs

using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StellarFramework
{
    public static class SceneLoader
    {
        /// <summary>
        /// 异步加载场景 (UniTask版)
        /// </summary>
        /// <param name="sceneName">场景名称</param>
        /// <param name="mode">加载模式</param>
        /// <param name="progress">进度回调接口 (0.0 ~ 1.0)</param>
        public static async UniTask LoadSceneAsync(string sceneName, LoadSceneMode mode = LoadSceneMode.Single, IProgress<float> progress = null)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                LogKit.LogError("[SceneLoader] 场景名为空，无法加载");
                return;
            }

            // 检查场景是否在 Build Settings 中
            if (SceneUtility.GetBuildIndexByScenePath(sceneName) == -1)
            {
                // 尝试只用名字查找（有时传入的是路径）
                // 注意：这里无法完美检测所有情况，但能捕获大部分配置错误
                LogKit.LogWarning($"[SceneLoader] 警告: 场景 '{sceneName}' 可能未添加到 Build Settings 中！");
            }

            var operation = SceneManager.LoadSceneAsync(sceneName, mode);
            if (operation == null)
            {
                LogKit.LogError($"[SceneLoader] 加载操作创建失败: {sceneName}");
                return;
            }

            // 防止场景瞬间切换，通常需要手动控制（这里为了通用性暂不设为 false，
            // 如果需要做 Loading 界面，建议使用 PreloadScene）
            // operation.allowSceneActivation = false;

            // 使用 UniTask 扩展等待
            await operation.ToUniTask(progress: progress);

            LogKit.Log($"[SceneLoader] 场景加载完成: {sceneName}");
        }

        /// <summary>
        /// 预加载场景 (不自动激活)
        /// </summary>
        /// <returns>返回 AsyncOperation 以便后续手动激活</returns>
        public static async UniTask<AsyncOperation> PreloadSceneAsync(string sceneName, IProgress<float> progress = null)
        {
            var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation == null)
            {
                LogKit.LogError($"[SceneLoader] 预加载失败: {sceneName}");
                return null;
            }

            operation.allowSceneActivation = false;

            // 轮询直到 0.9
            while (operation.progress < 0.9f)
            {
                // 归一化进度 (0~0.9 -> 0~1.0)
                progress?.Report(operation.progress / 0.9f);
                await UniTask.Yield();
            }

            progress?.Report(1.0f);
            LogKit.Log($"[SceneLoader] 场景预加载就绪: {sceneName}");

            return operation;
        }

        /// <summary>
        /// 激活预加载的场景
        /// </summary>
        public static void ActivatePreloadedScene(AsyncOperation operation)
        {
            if (operation != null)
            {
                operation.allowSceneActivation = true;
                LogKit.Log("[SceneLoader] 激活预加载场景");
            }
            else
            {
                LogKit.LogError("[SceneLoader] 激活失败: Operation 为空");
            }
        }
    }
}