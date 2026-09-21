using UnityEngine;

namespace StellarFramework.HybridCLR
{
    internal static class HybridCLRHotUpdateInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallAtRuntime()
        {
            HybridCLRKit.SetStrategy(new HybridCLRCodeHotUpdateStrategy());
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void InstallInEditor()
        {
            HybridCLRKit.SetStrategy(new HybridCLRCodeHotUpdateStrategy());
        }
#endif
    }
}
