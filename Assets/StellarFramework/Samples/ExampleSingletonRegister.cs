using UnityEngine;

namespace StellarFramework.Examples
{
    internal static class ExampleSingletonRegister
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterAll()
        {
            SingletonFactory.RegisterMetadata(typeof(GlobalNetworkManager),
                new SingletonMetadata
                {
                    ResourcePath = string.Empty,
                    LifeCycle = SingletonLifeCycle.Global,
                    UseContainer = true
                });

            SingletonFactory.RegisterMetadata(typeof(LevelDirector),
                new SingletonMetadata
                {
                    ResourcePath = string.Empty,
                    LifeCycle = SingletonLifeCycle.Scene,
                    UseContainer = true
                });

            SingletonFactory.RegisterMetadata(typeof(GameDataCalculator),
                new SingletonMetadata
                {
                    ResourcePath = string.Empty,
                    LifeCycle = SingletonLifeCycle.Global,
                    UseContainer = true
                });

            SingletonFactory.RegisterPureSingletonCreator(typeof(GameDataCalculator),
                static () => new GameDataCalculator());
        }
    }
}
