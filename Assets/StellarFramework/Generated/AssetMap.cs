using System.Collections.Generic;
namespace StellarFramework.Res.AB {
public static class AssetMap {
    public static Dictionary<string, string> GetMap() {
        return new Dictionary<string, string> {
            { "Assets/Art/ResKitTest/TestCapsule_AB.prefab", "art" },
        };
    }
    public static class Bundles {
        public const string ART = "art";
    }
}}