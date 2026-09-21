using UnityEngine;

namespace StellarFramework.Res
{
    /// <summary>
    /// Registers Addressables as a pure ResKit loading backend.
    /// </summary>
    /// <remarks>
    /// This installer intentionally does not register catalog/update/download services.
    /// Addressables participates in StellarFramework only as a Load/Release backend.
    /// </remarks>
    public static class AddressablesResKitInstaller
    {
        public const string LoaderKey = "Addressables";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Install()
        {
            ResKit.RegisterLoader(
                LoaderKey,
                request => ResKit.Allocate<AddressableLoader>());
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
