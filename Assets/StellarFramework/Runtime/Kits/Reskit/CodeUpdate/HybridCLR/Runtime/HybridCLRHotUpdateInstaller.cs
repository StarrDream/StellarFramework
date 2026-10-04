using UnityEngine;
using StellarFramework.Res;

namespace StellarFramework.Res.CodeUpdate.HybridCLR
{
    internal static class HybridCLRHotUpdateInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallAtRuntime()
        {
            Install();
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void InstallInEditor()
        {
            Install();
        }
#endif

        internal static void Install()
        {
            ResKit.RegisterCodeUpdateProvider<HotUpdateSettings, HybridCLRUpdateResult>(
                HybridCLRResCodeUpdateProvider.ProviderId,
                new HybridCLRResCodeUpdateProvider());
        }
    }
}
