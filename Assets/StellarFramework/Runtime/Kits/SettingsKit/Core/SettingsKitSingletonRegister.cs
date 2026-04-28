using UnityEngine;

namespace StellarFramework.Settings
{
    internal static class SettingsKitSingletonRegister
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            SingletonFactory.RegisterMetadata(
                typeof(SettingsManager),
                new SingletonMetadata
                {
                    ResourcePath = string.Empty,
                    LifeCycle = SingletonLifeCycle.Global,
                    UseContainer = true
                });

            SingletonFactory.RegisterPureSingletonCreator(
                typeof(SettingsManager),
                static () => new SettingsManager());
        }
    }
}
