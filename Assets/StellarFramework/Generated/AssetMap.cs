using System.Collections.Generic;
namespace StellarFramework.Res.AB {
public static class AssetMap {
    public static Dictionary<string, string> GetMap() {
        return new Dictionary<string, string> {
            { "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Drop Shadow.mat", "textmesh_pro_resources" },
            { "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset", "textmesh_pro_resources" },
            { "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Outline.mat", "textmesh_pro_resources" },
            { "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset", "textmesh_pro_resources" },
            { "Assets/TextMesh Pro/Resources/LineBreaking Following Characters.txt", "textmesh_pro_resources" },
            { "Assets/TextMesh Pro/Resources/LineBreaking Leading Characters.txt", "textmesh_pro_resources" },
            { "Assets/TextMesh Pro/Resources/Sprite Assets/EmojiOne.asset", "textmesh_pro_resources" },
            { "Assets/TextMesh Pro/Resources/Style Sheets/Default Style Sheet.asset", "textmesh_pro_resources" },
            { "Assets/TextMesh Pro/Resources/TMP Settings.asset", "textmesh_pro_resources" },
            { "Assets/Resources/UIPanel/Panel_Snake.prefab", "resources_uipanel" },
            { "Assets/Resources/UIPanel/Panel_StartGame.prefab", "resources_uipanel" },
        };
    }
    public static class Bundles {
        public const string RESOURCES_UIPANEL = "resources_uipanel";
        public const string SHADERS = "shaders";
        public const string TEXTMESH_PRO_RESOURCES = "textmesh_pro_resources";
    }
}}