using UnityEngine;

namespace StellarFramework.Res
{
    /// <summary>Registers the Unity Resources backend as an optional ResKit loader.</summary>
    public static class ResKitResourcesInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallBeforeFirstScene()
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

        /// <summary>Registers the Resources loader under ResKit's built-in Resources key.</summary>
        public static void Install()
        {
            ResKit.RegisterLoader(ResKit.KeyResources, _ => ResKit.Allocate<ResourceLoader>());
        }

        /// <summary>Removes the Resources loader registration.</summary>
        public static void Uninstall()
        {
            ResKit.RegisterLoader(ResKit.KeyResources, null);
        }
    }
}
