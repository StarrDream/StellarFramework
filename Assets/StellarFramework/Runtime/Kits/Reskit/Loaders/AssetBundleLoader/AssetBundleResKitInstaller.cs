using UnityEngine;

namespace StellarFramework.Res
{
    /// <summary>
    /// AssetBundle 适配器的可选安装入口。导入该程序集时才注册 AB 加载器与单例。
    /// </summary>
    public static class AssetBundleResKitInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Install()
        {
            SingletonFactory.RegisterMetadata(typeof(AB.AssetBundleManager), new SingletonMetadata
            {
                ResourcePath = string.Empty,
                LifeCycle = SingletonLifeCycle.Global,
                UseContainer = true
            });
            SingletonFactory.RegisterPureSingletonCreator(typeof(AB.AssetBundleManager),
                static () => new AB.AssetBundleManager());
            ResKit.RegisterLoader(ResKit.KeyAssetBundle, request => ResKit.Allocate<AssetBundleLoader>());
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void InstallInEditor()
        {
            Install();
        }
#endif
    }
}
