using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace StellarFramework
{
    public enum FrameworkRenderPipelineFamily
    {
        BuiltIn = 0,
        URP = 1,
        HDRP = 2
    }

    public static class RenderPipelineCompatibility
    {
        public static FrameworkRenderPipelineFamily CurrentFamily => DetectCurrentFamily();

        public static bool IsBuiltInPipeline()
        {
            return DetectCurrentFamily() == FrameworkRenderPipelineFamily.BuiltIn;
        }

        public static FrameworkRenderPipelineFamily DetectCurrentFamily()
        {
            RenderPipelineAsset pipelineAsset = GraphicsSettings.currentRenderPipeline;
            if (pipelineAsset == null)
            {
                pipelineAsset = QualitySettings.renderPipeline;
            }

            if (pipelineAsset == null)
            {
                return FrameworkRenderPipelineFamily.BuiltIn;
            }

            string typeName = pipelineAsset.GetType().FullName ?? pipelineAsset.GetType().Name;
            string assetName = pipelineAsset.name ?? string.Empty;
            if (ContainsIgnoreCase(typeName, "HDRender") || ContainsIgnoreCase(assetName, "HDRP"))
            {
                return FrameworkRenderPipelineFamily.HDRP;
            }

            if (ContainsIgnoreCase(typeName, "Universal") || ContainsIgnoreCase(typeName, "URP"))
            {
                return FrameworkRenderPipelineFamily.URP;
            }

            // Non-URP/HDRP SRP variants are treated like Built-in for framework compatibility.
            return FrameworkRenderPipelineFamily.BuiltIn;
        }

        public static Shader FindPreferredLitShader()
        {
            FrameworkRenderPipelineFamily family = DetectCurrentFamily();
            switch (family)
            {
                case FrameworkRenderPipelineFamily.URP:
                    return FindFirstShader(
                        "Universal Render Pipeline/Lit",
                        "Standard");

                case FrameworkRenderPipelineFamily.HDRP:
                    return FindFirstShader(
                        "HDRP/Lit",
                        "HDRP/LayeredLit",
                        "Standard");

                default:
                    return FindFirstShader("Standard");
            }
        }

        public static string GetPreferredLitShaderName()
        {
            Shader shader = FindPreferredLitShader();
            return shader != null ? shader.name : string.Empty;
        }

        private static Shader FindFirstShader(params string[] shaderNames)
        {
            if (shaderNames == null)
            {
                return null;
            }

            for (int i = 0; i < shaderNames.Length; i++)
            {
                string shaderName = shaderNames[i];
                if (string.IsNullOrWhiteSpace(shaderName))
                {
                    continue;
                }

                Shader shader = Shader.Find(shaderName);
                if (shader != null)
                {
                    return shader;
                }
            }

            return null;
        }

        private static bool ContainsIgnoreCase(string source, string value)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(value))
            {
                return false;
            }

            return source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
