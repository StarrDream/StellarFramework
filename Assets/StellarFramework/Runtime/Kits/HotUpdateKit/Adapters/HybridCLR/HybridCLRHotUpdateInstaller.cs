using UnityEngine;

namespace StellarFramework.HotUpdate
{
    internal static class HybridCLRHotUpdateInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallAtRuntime()
        {
            HotUpdateKit.SetCodeHotUpdateStrategy(new HybridCLRCodeHotUpdateStrategy());
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void InstallInEditor()
        {
            HotUpdateKit.SetCodeHotUpdateStrategy(new HybridCLRCodeHotUpdateStrategy());
        }
#endif
    }
}
