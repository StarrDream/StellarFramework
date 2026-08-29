using UnityEngine;

namespace StellarFramework.UI
{
    /// <summary>
    /// 可选 ResKit 适配器导入后，恢复 UIKit 的 ResKit 默认加载策略。
    /// </summary>
    public static class UIKitResKitInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Install()
        {
            UIKit.RegisterDefaultLoadStrategyFactory(settings => new ResKitUILoadStrategy(settings));
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
