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
        [Header("热更配置规范")] public static string HotUpdateAssemblyName = "HotUpdate.dll";
        public static string HotUpdateEntryClass = "HotUpdate.HotUpdateMain";
        public static string HotUpdateEntryMethod = "Main";

        /// <summary>
        /// 补充元数据 DLL 列表 (用于解决 AOT 泛型实例化报错问题)
        /// 需根据 HybridCLR 生成的 AOT 列表进行配置
        /// </summary>
        public static List<string> AOTMetaAssemblyFiles = new List<string>()
        {
            "mscorlib.dll",
            "System.dll",
            "System.Core.dll"
        };

        /// <summary>
        /// 步骤 1：加载 AOT 补充元数据
        /// </summary>
        /// <param name="dllBytesProvider">提供 DLL 字节流的委托 (由业务层注入 ResKit 或 Addressables 的加载逻辑)</param>
        public static async UniTask LoadMetadataForAOTAssembliesAsync(Func<string, UniTask<byte[]>> dllBytesProvider)
        {
            if (dllBytesProvider == null)
            {
                LogKit.LogError("[HybridCLRHook] 加载 AOT 元数据失败：未提供 DLL 读取委托，请注入资源加载逻辑。");
                return;
            }

            foreach (var aotDllName in AOTMetaAssemblyFiles)
            {
                byte[] dllBytes = await dllBytesProvider.Invoke(aotDllName);
                if (dllBytes != null && dllBytes.Length > 0)
                {
                    // 宏隔离：确保在未安装 HybridCLR 插件时，框架依然可以正常编译
#if HYBRIDCLR_ENABLE
                    HybridCLR.RuntimeApi.LoadMetadataForAOTAssembly(dllBytes, HybridCLR.HomologousImageMode.SuperSet);
                    LogKit.Log($"[HybridCLRHook] 成功加载 AOT 补充元数据: {aotDllName}");
#else
                    LogKit.LogWarning($"[HybridCLRHook] 未开启 HYBRIDCLR_ENABLE 宏，跳过 AOT 元数据加载: {aotDllName}");
#endif
                }
                else
                {
                    LogKit.LogError($"[HybridCLRHook] 无法获取 AOT 元数据 DLL 字节流: {aotDllName}");
                }
            }
        }

        /// <summary>
        /// 步骤 2：加载热更程序集并执行反射跳转
        /// </summary>
        /// <param name="hotUpdateDllBytes">热更 DLL 的字节流</param>
        public static void LoadAndStartHotUpdateAssembly(byte[] hotUpdateDllBytes)
        {
            if (hotUpdateDllBytes == null || hotUpdateDllBytes.Length == 0)
            {
                LogKit.LogError("[HybridCLRHook] 启动热更失败：热更 DLL 字节流为空。");
                return;
            }

            try
            {
                // 1. 加载 Assembly 至内存
                Assembly hotUpdateAss = Assembly.Load(hotUpdateDllBytes);
                LogKit.Log($"[HybridCLRHook] 成功加载热更程序集: {hotUpdateAss.FullName}");

                // 2. 查找入口类
                Type entryType = hotUpdateAss.GetType(HotUpdateEntryClass);
                if (entryType == null)
                {
                    LogKit.LogError($"[HybridCLRHook] 找不到热更入口类: {HotUpdateEntryClass}，请检查命名空间与类名配置。");
                    return;
                }

                // 3. 查找入口方法
                MethodInfo method =
                    entryType.GetMethod(HotUpdateEntryMethod, BindingFlags.Static | BindingFlags.Public);
                if (method == null)
                {
                    LogKit.LogError($"[HybridCLRHook] 找不到热更入口方法: {HotUpdateEntryMethod}，请确保该方法为 public static。");
                    return;
                }

                // 4. 执行跳转，将控制权移交至热更域
                LogKit.Log("[HybridCLRHook] 正在跨域跳转至热更逻辑...");
                method.Invoke(null, null);
            }
            catch (Exception e)
            {
                LogKit.LogError($"[HybridCLRHook] 启动热更逻辑时发生严重异常: {e.Message}\n{e.StackTrace}");
            }
        }
    }
}