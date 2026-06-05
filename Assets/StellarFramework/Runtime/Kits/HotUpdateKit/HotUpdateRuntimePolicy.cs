using UnityEngine;

namespace StellarFramework.HotUpdate
{
    /// <summary>
    /// 热更运行策略
    /// 编辑器/开发构建尽量保留新手友好的兜底与提示，
    /// 正式发布构建则切到严格模式，避免把部署错误带到线上。
    /// </summary>
    public static class HotUpdateRuntimePolicy
    {
        public static bool IsStrictProductionRuntime =>
            IsStrictProductionRuntimeFor(Application.isEditor, Debug.isDebugBuild);

        public static bool IsStrictProductionRuntimeFor(bool isEditor, bool isDebugBuild)
        {
            return !isEditor && !isDebugBuild;
        }
    }
}
