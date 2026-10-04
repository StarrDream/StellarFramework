using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace StellarFramework.Editor.HotUpdatePublisher
{
    /// <summary>
    /// 从 asmdef、Unity AssetDatabase、PluginImporter 和 Prefab 组件信息中解析变更风险。
    /// 此适配器只存在于 Editor Publisher 程序集，不进入 Player 或 Hot Update Full Runtime。
    /// </summary>
    public sealed class UnityHotUpdateChangeFactsProvider : IHotUpdateChangeFactsProvider
    {
        private static readonly Regex GuidRegex = new Regex(
            @"(?m)^guid:\s*([0-9a-fA-F]+)\s*$",
            RegexOptions.Compiled);

        private static readonly Regex SerializedScriptGuidRegex = new Regex(
            @"(?m)^\s*m_Script:\s*\{[^}\r\n]*\bguid:\s*([0-9a-fA-F]{32})",
            RegexOptions.Compiled);

        private static readonly string[] AotSensitiveMarkers =
        {
            "System.Reflection.Emit",
            "System.Linq.Expressions",
            "DllImport(",
            "Marshal.GetFunctionPointerForDelegate",
            "NativeLibrary.Load",
            "delegate*"
        };

        private readonly string _projectRoot;
        private readonly Dictionary<string, HotUpdateAssemblyDefinition> _definitionsByPath;
        private readonly IReadOnlyList<HotUpdateAssemblyDefinition> _definitions;
        private readonly HashSet<string> _buildSettingsScenes;

        /// <summary>创建当前 Unity 项目的事实解析器。</summary>
        public UnityHotUpdateChangeFactsProvider(string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new ArgumentException("Project root is required.", nameof(projectRoot));
            }

            _projectRoot = Path.GetFullPath(projectRoot);
            _definitions = ReadAssemblyDefinitions(_projectRoot);
            _definitionsByPath = _definitions
                .Where(definition => !string.IsNullOrWhiteSpace(definition.Path))
                .ToDictionary(definition => definition.Path, StringComparer.OrdinalIgnoreCase);
            _buildSettingsScenes = new HashSet<string>(
                EditorBuildSettings.scenes
                    .Where(scene => scene != null && scene.enabled)
                    .Select(scene => HotUpdateDevelopmentConvention.NormalizePath(scene.path)),
                StringComparer.OrdinalIgnoreCase);
        }

        /// <inheritdoc />
        public HotUpdateChangeFacts Resolve(HotUpdateWorkspaceChange change)
        {
            if (change == null)
            {
                throw new ArgumentNullException(nameof(change));
            }

            string path = HotUpdateDevelopmentConvention.NormalizePath(change.Path);
            string assetPath = path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)
                ? path.Substring(0, path.Length - ".meta".Length)
                : path;
            string absoluteAssetPath = ToAbsolutePath(assetPath);
            HotUpdateAssemblyDefinition definition = ResolveAssemblyDefinition(assetPath);
            string assemblyName = definition?.Name ?? string.Empty;
            bool isBuildSettingsScene = _buildSettingsScenes.Contains(assetPath);
            HotUpdateProjectLayer layer = HotUpdateDevelopmentConvention.ClassifyLayer(
                assetPath,
                assemblyName,
                isBuildSettingsScene);
            HotUpdateChangeAssetKind assetKind = ResolveAssetKind(assetPath);
            bool isPluginImporter = IsPluginImporter(assetPath);
            bool containsHotUpdateBehaviour =
                (assetKind == HotUpdateChangeAssetKind.Prefab ||
                 assetKind == HotUpdateChangeAssetKind.Scene) &&
                File.Exists(absoluteAssetPath) &&
                ContainsHotUpdateMonoBehaviour(assetPath);
            bool aotRisk = assetKind == HotUpdateChangeAssetKind.CSharpSource &&
                           File.Exists(absoluteAssetPath) &&
                           ContainsAotSensitiveMarker(File.ReadAllText(absoluteAssetPath));

            if (assetKind == HotUpdateChangeAssetKind.AssemblyDefinition)
            {
                definition = GetDefinitionAtPath(assetPath);
                assemblyName = definition?.Name ?? assemblyName;
                layer = HotUpdateDevelopmentConvention.ClassifyLayer(assetPath, assemblyName);
            }

            return new HotUpdateChangeFacts(
                path,
                assetKind,
                layer,
                change.GitStatus,
                change.Kind,
                assemblyName,
                assemblyReferencesChanged: assetKind == HotUpdateChangeAssetKind.AssemblyDefinition,
                isPluginImporter: isPluginImporter,
                containsHotUpdateMonoBehaviour: containsHotUpdateBehaviour,
                hasAotSensitiveApiDependency: aotRisk);
        }

        /// <inheritdoc />
        public IReadOnlyList<HotUpdateDependencyBoundaryViolation> FindDependencyBoundaryViolations()
        {
            return HotUpdateDependencyBoundaryPolicy.FindViolations(_definitions);
        }

        private static IReadOnlyList<HotUpdateAssemblyDefinition> ReadAssemblyDefinitions(string projectRoot)
        {
            var definitions = new List<HotUpdateAssemblyDefinition>();
            foreach (string searchRoot in GetAssemblySearchRoots(projectRoot))
            {
                foreach (string asmdefPath in Directory.GetFiles(
                             searchRoot,
                             "*.asmdef",
                             SearchOption.AllDirectories).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                {
                    string json = File.ReadAllText(asmdefPath);
                    AssemblyDefinitionDocument document;
                    try
                    {
                        document = JsonUtility.FromJson<AssemblyDefinitionDocument>(json);
                    }
                    catch (Exception exception)
                    {
                        throw new InvalidDataException(
                            $"Could not parse assembly definition '{asmdefPath}'.",
                            exception);
                    }

                    if (document == null || string.IsNullOrWhiteSpace(document.name))
                    {
                        throw new InvalidDataException(
                            $"Assembly definition '{asmdefPath}' has no valid assembly name.");
                    }

                    string projectRelativePath = ToProjectRelativePath(projectRoot, asmdefPath);
                    string guid = ReadMetaGuid(asmdefPath + ".meta");
                    bool editorOnly = IsEditorOnlyAssembly(document, projectRelativePath);
                    definitions.Add(new HotUpdateAssemblyDefinition(
                        document.name,
                        projectRelativePath,
                        guid,
                        document.references,
                        editorOnly));
                }
            }

            return definitions;
        }

        private HotUpdateAssemblyDefinition ResolveAssemblyDefinition(string assetPath)
        {
            string normalized = HotUpdateDevelopmentConvention.NormalizePath(assetPath);
            if (normalized.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase))
            {
                return GetDefinitionAtPath(normalized);
            }

            string directory = Path.GetDirectoryName(normalized)?.Replace('\\', '/');
            while (!string.IsNullOrEmpty(directory))
            {
                string absoluteDirectory = ToAbsolutePath(directory);
                string[] asmdefs = Directory.Exists(absoluteDirectory)
                    ? Directory.GetFiles(absoluteDirectory, "*.asmdef", SearchOption.TopDirectoryOnly)
                    : Array.Empty<string>();
                if (asmdefs.Length > 1)
                {
                    throw new InvalidDataException(
                        $"Multiple asmdefs were found in '{directory}', so membership for '{assetPath}' is ambiguous.");
                }

                if (asmdefs.Length == 1)
                {
                    string relativeAsmdef = ToProjectRelativePath(_projectRoot, asmdefs[0]);
                    if (_definitionsByPath.TryGetValue(
                            relativeAsmdef,
                            out HotUpdateAssemblyDefinition definition))
                    {
                        return definition;
                    }
                }

                directory = Path.GetDirectoryName(directory)?.Replace('\\', '/');
            }

            return null;
        }

        private HotUpdateAssemblyDefinition GetDefinitionAtPath(string path)
        {
            string normalized = HotUpdateDevelopmentConvention.NormalizePath(path);
            return _definitionsByPath.TryGetValue(normalized, out HotUpdateAssemblyDefinition definition)
                ? definition
                : null;
        }

        private bool IsPluginImporter(string assetPath)
        {
            if (!File.Exists(ToAbsolutePath(assetPath)))
            {
                return false;
            }

            return AssetImporter.GetAtPath(assetPath) is PluginImporter;
        }

        private bool ContainsHotUpdateMonoBehaviour(string assetPath)
        {
            var candidatePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                HotUpdateDevelopmentConvention.NormalizePath(assetPath)
            };

            foreach (string dependency in AssetDatabase.GetDependencies(assetPath, true))
            {
                string normalized = HotUpdateDevelopmentConvention.NormalizePath(dependency);
                string extension = Path.GetExtension(normalized);
                if (extension.Equals(".prefab", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".unity", StringComparison.OrdinalIgnoreCase))
                {
                    candidatePaths.Add(normalized);
                }
            }

            foreach (string candidatePath in candidatePaths)
            {
                string absolutePath = ToAbsolutePath(candidatePath);
                if (!File.Exists(absolutePath))
                {
                    continue;
                }

                string serializedAsset = File.ReadAllText(absolutePath);
                MatchCollection references = SerializedScriptGuidRegex.Matches(serializedAsset);
                for (int index = 0; index < references.Count; index++)
                {
                    string scriptPath = AssetDatabase.GUIDToAssetPath(references[index].Groups[1].Value);
                    if (string.IsNullOrEmpty(scriptPath))
                    {
                        continue;
                    }

                    HotUpdateAssemblyDefinition definition = ResolveAssemblyDefinition(scriptPath);
                    if (HotUpdateDevelopmentConvention.ClassifyLayer(scriptPath, definition?.Name) ==
                        HotUpdateProjectLayer.HotUpdate)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool ContainsAotSensitiveMarker(string source)
        {
            for (int index = 0; index < AotSensitiveMarkers.Length; index++)
            {
                if (source.IndexOf(AotSensitiveMarkers[index], StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static HotUpdateChangeAssetKind ResolveAssetKind(string assetPath)
        {
            string normalized = HotUpdateDevelopmentConvention.NormalizePath(assetPath);
            if (IsProjectConfigurationPath(normalized))
            {
                return HotUpdateChangeAssetKind.ProjectConfiguration;
            }

            string extension = Path.GetExtension(normalized);
            if (extension.Equals(".cs", StringComparison.OrdinalIgnoreCase))
            {
                return HotUpdateChangeAssetKind.CSharpSource;
            }

            if (extension.Equals(".asmdef", StringComparison.OrdinalIgnoreCase))
            {
                return HotUpdateChangeAssetKind.AssemblyDefinition;
            }

            if (extension.Equals(".unity", StringComparison.OrdinalIgnoreCase))
            {
                return HotUpdateChangeAssetKind.Scene;
            }

            if (extension.Equals(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                return HotUpdateChangeAssetKind.Prefab;
            }

            if (extension.Equals(".shader", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".hlsl", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".cginc", StringComparison.OrdinalIgnoreCase))
            {
                return HotUpdateChangeAssetKind.Shader;
            }

            if (extension.Equals(".shadervariants", StringComparison.OrdinalIgnoreCase))
            {
                return HotUpdateChangeAssetKind.ShaderVariant;
            }

            if (extension.Equals(".compute", StringComparison.OrdinalIgnoreCase))
            {
                return HotUpdateChangeAssetKind.ComputeShader;
            }

            if (extension.Equals(".mat", StringComparison.OrdinalIgnoreCase))
            {
                return HotUpdateChangeAssetKind.Material;
            }

            if (normalized.StartsWith("Assets/GameHotUpdate/", StringComparison.OrdinalIgnoreCase) &&
                (extension.Equals(".bytes", StringComparison.OrdinalIgnoreCase) ||
                 extension.Equals(".json", StringComparison.OrdinalIgnoreCase)))
            {
                return HotUpdateChangeAssetKind.HotUpdatePayload;
            }

            if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".csv", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".bytes", StringComparison.OrdinalIgnoreCase))
            {
                return HotUpdateChangeAssetKind.Configuration;
            }

            if (extension.Equals(".fbx", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".obj", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".blend", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".gltf", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".glb", StringComparison.OrdinalIgnoreCase))
            {
                return HotUpdateChangeAssetKind.Model;
            }

            if (IsNativePluginExtension(extension))
            {
                return HotUpdateChangeAssetKind.NativePlugin;
            }

            Type assetType = AssetDatabase.GetMainAssetTypeAtPath(normalized);
            if (assetType != null)
            {
                if (assetType.Name.IndexOf("ShaderVariantCollection", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return HotUpdateChangeAssetKind.ShaderVariant;
                }

                if (assetType.Name.IndexOf("ScriptableRendererFeature", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return HotUpdateChangeAssetKind.Shader;
                }

                if (typeof(ScriptableObject).IsAssignableFrom(assetType))
                {
                    return HotUpdateChangeAssetKind.ScriptableObject;
                }

                if (assetType == typeof(GameObject) || assetType.Name == "Mesh")
                {
                    return HotUpdateChangeAssetKind.Model;
                }

            }

            if (IsTextureExtension(extension)) return HotUpdateChangeAssetKind.Texture;
            if (IsAudioExtension(extension)) return HotUpdateChangeAssetKind.Audio;
            if (IsVideoExtension(extension)) return HotUpdateChangeAssetKind.Video;
            return HotUpdateChangeAssetKind.Unknown;
        }

        private static bool IsProjectConfigurationPath(string path)
        {
            return HotUpdateDevelopmentConvention.IsAtOrBelow(path, "ProjectSettings") ||
                   HotUpdateDevelopmentConvention.IsAtOrBelow(path, "Packages");
        }

        private static bool IsNativePluginExtension(string extension)
        {
            return extension.Equals(".aar", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".so", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".dylib", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".bundle", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".dll", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTextureExtension(string extension)
        {
            return extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".tga", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".psd", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".tif", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".tiff", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".exr", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAudioExtension(string extension)
        {
            return extension.Equals(".wav", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".ogg", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".aiff", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".aif", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsVideoExtension(string extension)
        {
            return extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".mov", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".webm", StringComparison.OrdinalIgnoreCase);
        }

        private static string ReadMetaGuid(string metaPath)
        {
            if (!File.Exists(metaPath))
            {
                return string.Empty;
            }

            Match match = GuidRegex.Match(File.ReadAllText(metaPath));
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private static IEnumerable<string> GetAssemblySearchRoots(string projectRoot)
        {
            string assetsRoot = Path.Combine(projectRoot, "Assets");
            if (Directory.Exists(assetsRoot))
            {
                yield return assetsRoot;
            }

            string packagesRoot = Path.Combine(projectRoot, "Packages");
            if (Directory.Exists(packagesRoot))
            {
                yield return packagesRoot;
            }
        }

        private static bool IsEditorOnlyAssembly(
            AssemblyDefinitionDocument document,
            string projectRelativePath)
        {
            if (HotUpdateDevelopmentConvention.IsEditorOnlyPath(projectRelativePath))
            {
                return true;
            }

            string[] includedPlatforms = document.includePlatforms ?? Array.Empty<string>();
            return includedPlatforms.Length > 0 &&
                   includedPlatforms.All(platform =>
                       string.Equals(platform, "Editor", StringComparison.OrdinalIgnoreCase));
        }

        private static string ToProjectRelativePath(string projectRoot, string absolutePath)
        {
            string relative = absolutePath.Substring(projectRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return HotUpdateDevelopmentConvention.NormalizePath(relative);
        }

        private string ToAbsolutePath(string projectRelativePath)
        {
            return Path.Combine(
                _projectRoot,
                HotUpdateDevelopmentConvention.NormalizePath(projectRelativePath)
                    .Replace('/', Path.DirectorySeparatorChar));
        }

        [Serializable]
        private sealed class AssemblyDefinitionDocument
        {
            public string name;
            public string[] references;
            public string[] includePlatforms;
        }
    }
}
