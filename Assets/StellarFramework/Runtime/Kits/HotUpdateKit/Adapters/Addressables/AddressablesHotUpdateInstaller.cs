using StellarFramework.HotUpdate;

namespace StellarFramework.Res
{
    public static class AddressablesHotUpdateInstaller
    {
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Install()
        {
            HotUpdateKit.SetResourceStrategy(new AddressablesPackageHotUpdateStrategy());
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
