using System;
using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace StellarFramework.HotUpdate
{
    /// <summary>
    /// HybridCLR 热更生命周期钩子
    /// 职责：提供标准化的代码热更装载与跳转流程，彻底解耦 AOT 环境与 HotUpdate 环境。
    /// </summary>
    public static class HybridCLRHook
    {
        public enum HotUpdateState
        {
            None,
            LoadingMetadata,
            MetadataLoaded,
            LoadingHotUpdateAssembly,
            LoadedHotUpdateAssembly,
            EnteringHotUpdate,
            EnteredHotUpdate,
            Failed
        }

        [Header("热更配置规范")] public static string HotUpdateAssemblyName = "HotUpdate.dll";

        public static string HotUpdateEntryClass = "HotUpdate.HotUpdateMain";
        public static string HotUpdateEntryMethod = "Main";

        /// <summary>
        /// 补充元数据 DLL 列表
        /// </summary>
        public static List<string> AOTMetaAssemblyFiles = new List<string>
        {
            "mscorlib.dll",
            "System.dll",
            "System.Core.dll"
        };

        public static HotUpdateState State { get; private set; } = HotUpdateState.None;
        public static string LastError { get; private set; }
        public static string LoadedAssemblyFullName { get; private set; }

        /// <summary>
        /// 步骤 1：加载 AOT 补充元数据
        /// </summary>
        public static async UniTask<bool> LoadMetadataForAOTAssembliesAsync(
            Func<string, UniTask<byte[]>> dllBytesProvider)
        {
            if (dllBytesProvider == null)
            {
                SetFailed("[HybridCLRHook] 加载 AOT 元数据失败: dllBytesProvider 为空");
                return false;
            }

            State = HotUpdateState.LoadingMetadata;
            LastError = null;

            for (int i = 0; i < AOTMetaAssemblyFiles.Count; i++)
            {
                string aotDllName = AOTMetaAssemblyFiles[i];
                if (string.IsNullOrEmpty(aotDllName))
                {
                    SetFailed("[HybridCLRHook] 加载 AOT 元数据失败: 检测到空 DLL 名称");
                    return false;
                }

                byte[] dllBytes = await dllBytesProvider.Invoke(aotDllName);
                if (dllBytes == null || dllBytes.Length == 0)
                {
                    SetFailed($"[HybridCLRHook] 无法获取 AOT 元数据 DLL 字节流: {aotDllName}");
                    return false;
                }

#if HYBRIDCLR_ENABLE
                HybridCLR.RuntimeApi.LoadMetadataForAOTAssembly(dllBytes, HybridCLR.HomologousImageMode.SuperSet);
                LogKit.Log($"[HybridCLRHook] 成功加载 AOT 补充元数据: {aotDllName}");
#else
                LogKit.LogWarning($"[HybridCLRHook] 未开启 HYBRIDCLR_ENABLE 宏，跳过 AOT 元数据加载: {aotDllName}");
#endif
            }

            State = HotUpdateState.MetadataLoaded;
            return true;
        }

        /// <summary>
        /// 步骤 2：加载热更程序集并执行跳转
        /// </summary>
        public static bool LoadAndStartHotUpdateAssembly(byte[] hotUpdateDllBytes)
        {
            if (hotUpdateDllBytes == null || hotUpdateDllBytes.Length == 0)
            {
                SetFailed("[HybridCLRHook] 启动热更失败: 热更 DLL 字节流为空");
                return false;
            }

            State = HotUpdateState.LoadingHotUpdateAssembly;
            LastError = null;
            LoadedAssemblyFullName = null;

            Assembly hotUpdateAssembly = null;
            try
            {
                hotUpdateAssembly = Assembly.Load(hotUpdateDllBytes);
            }
            catch (Exception e)
            {
                SetFailed($"[HybridCLRHook] 加载热更程序集失败: Exception={e.Message}");
                return false;
            }

            if (hotUpdateAssembly == null)
            {
                SetFailed("[HybridCLRHook] 加载热更程序集失败: Assembly.Load 返回为空");
                return false;
            }

            LoadedAssemblyFullName = hotUpdateAssembly.FullName;
            State = HotUpdateState.LoadedHotUpdateAssembly;
            LogKit.Log($"[HybridCLRHook] 成功加载热更程序集: {LoadedAssemblyFullName}");

            Type entryType = hotUpdateAssembly.GetType(HotUpdateEntryClass);
            if (entryType == null)
            {
                SetFailed($"[HybridCLRHook] 找不到热更入口类: {HotUpdateEntryClass}");
                return false;
            }

            MethodInfo method = entryType.GetMethod(HotUpdateEntryMethod, BindingFlags.Static | BindingFlags.Public);
            if (method == null)
            {
                SetFailed($"[HybridCLRHook] 找不到热更入口方法: {HotUpdateEntryMethod}, EntryClass={HotUpdateEntryClass}");
                return false;
            }

            State = HotUpdateState.EnteringHotUpdate;
            LogKit.Log("[HybridCLRHook] 正在跨域跳转至热更逻辑...");

            try
            {
                method.Invoke(null, null);
            }
            catch (Exception e)
            {
                SetFailed($"[HybridCLRHook] 执行热更入口失败: Exception={e.Message}\nStackTrace={e.StackTrace}");
                return false;
            }

            State = HotUpdateState.EnteredHotUpdate;
            LogKit.Log("[HybridCLRHook] 热更入口执行完成");
            return true;
        }

        private static void SetFailed(string error)
        {
            State = HotUpdateState.Failed;
            LastError = error;
            LogKit.LogError(error);
        }
    }
}