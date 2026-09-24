using System;
using System.Collections.Generic;
using System.Linq;

namespace StellarFramework.Editor.HotUpdatePublisher
{
    /// <summary>识别业务资产在 Base App 与远端热更层中的约定归属。</summary>
    public enum HotUpdateProjectLayer
    {
        Unknown = 0,
        BaseApp = 1,
        HotUpdate = 2,
        RemoteContent = 3,
        BuiltInContent = 4,
        EditorOnly = 5
    }

    /// <summary>供变更分类器使用的 Unity 资产类别。</summary>
    public enum HotUpdateChangeAssetKind
    {
        Unknown = 0,
        CSharpSource = 1,
        AssemblyDefinition = 2,
        Scene = 3,
        Prefab = 4,
        Shader = 5,
        ShaderVariant = 6,
        ComputeShader = 7,
        Material = 8,
        ScriptableObject = 9,
        Texture = 10,
        Audio = 11,
        Video = 12,
        HotUpdatePayload = 13,
        DocumentationOrTooling = 14,
        NativePlugin = 15,
        ProjectConfiguration = 16,
        Configuration = 17,
        Model = 18
    }

    /// <summary>
    /// 新 Unity 项目的 Base / HotUpdate / 内容目录约定。旧项目可继续使用原目录；
    /// 未声明归属的资产会要求更高等级验证，而不会被静默认定为安全热更内容。
    /// </summary>
    public static class HotUpdateDevelopmentConvention
    {
        public const string ProjectRoot = "Assets/_Project";
        public const string BaseRoot = "Assets/_Project/Base";
        public const string HotUpdateRoot = "Assets/_Project/HotUpdate";
        public const string RemoteContentRoot = "Assets/_Project/Content";
        public const string BuiltInContentRoot = "Assets/_Project/BaseContent";
        public const string DefaultHotUpdateAssemblyName = "HotUpdate";

        /// <summary>将相对路径归一化为 Unity 使用的正斜杠形式。</summary>
        public static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            string normalized = path.Replace('\\', '/').Trim();
            while (normalized.StartsWith("./", StringComparison.Ordinal))
            {
                normalized = normalized.Substring(2);
            }

            return normalized.TrimEnd('/');
        }

        /// <summary>判断程序集名称是否遵循 HotUpdate 命名约定。</summary>
        public static bool IsHotUpdateAssemblyName(string assemblyName)
        {
            if (string.IsNullOrWhiteSpace(assemblyName))
            {
                return false;
            }

            string name = assemblyName.Trim();
            return string.Equals(name, DefaultHotUpdateAssemblyName, StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith(DefaultHotUpdateAssemblyName + ".", StringComparison.OrdinalIgnoreCase) ||
                   name.EndsWith("." + DefaultHotUpdateAssemblyName, StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("." + DefaultHotUpdateAssemblyName + ".", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>判断路径是否位于一个已知的远端内容根目录。</summary>
        public static bool IsRemoteContentPath(string path)
        {
            string normalized = NormalizePath(path);
            return IsAtOrBelow(normalized, RemoteContentRoot) ||
                   IsAtOrBelow(normalized, "Assets/GameHotUpdate/Code") ||
                   IsAtOrBelow(normalized, "Assets/GameHotUpdate/Metadata") ||
                   IsAtOrBelow(normalized, "Assets/GameHotUpdate/Manifest");
        }

        /// <summary>判断路径是否明确属于随 Base App 内置的内容。</summary>
        public static bool IsBuiltInContentPath(string path)
        {
            string normalized = NormalizePath(path);
            return IsAtOrBelow(normalized, BuiltInContentRoot) ||
                   IsAtOrBelow(normalized, "Assets/Resources") ||
                   IsAtOrBelow(normalized, "Assets/StreamingAssets");
        }

        /// <summary>判断路径是否属于 Unity Editor、测试、文档或维护脚本，不会进入 Player 内容。</summary>
        public static bool IsEditorOnlyPath(string path)
        {
            string normalized = NormalizePath(path);
            if (IsAtOrBelow(normalized, "Tools") ||
                IsAtOrBelow(normalized, "Assets/StellarFramework/FrameworkDoc") ||
                IsAtOrBelow(normalized, "Assets/StellarFrameworkVerification") ||
                IsAtOrBelow(normalized, "Assets/Tests"))
            {
                return true;
            }

            string[] segments = normalized.Split('/');
            return segments.Any(segment =>
                string.Equals(segment, "Editor", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(segment, "Tests", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 按路径、程序集成员关系和 Build Settings 场景标记归类资产；
        /// Built-in 与 Base 归属优先于目录命名习惯。
        /// </summary>
        public static HotUpdateProjectLayer ClassifyLayer(
            string path,
            string assemblyName = null,
            bool isBuildSettingsScene = false)
        {
            string normalized = NormalizePath(path);
            if (IsEditorOnlyPath(normalized))
            {
                return HotUpdateProjectLayer.EditorOnly;
            }

            if (IsAtOrBelow(normalized, BaseRoot))
            {
                return HotUpdateProjectLayer.BaseApp;
            }

            if (IsBuiltInContentPath(normalized) || isBuildSettingsScene)
            {
                return HotUpdateProjectLayer.BuiltInContent;
            }

            if (IsAtOrBelow(normalized, HotUpdateRoot) ||
                IsAtOrBelow(normalized, "Assets/GameHotUpdate") ||
                IsHotUpdateAssemblyName(assemblyName))
            {
                return HotUpdateProjectLayer.HotUpdate;
            }

            if (IsRemoteContentPath(normalized))
            {
                return HotUpdateProjectLayer.RemoteContent;
            }

            return HotUpdateProjectLayer.Unknown;
        }

        /// <summary>HotUpdate MonoBehaviour 只能存在于 YooAsset 远端管理的 Prefab/Scene。</summary>
        public static bool CanPlaceHotUpdateMonoBehaviourIn(string assetPath)
        {
            return IsRemoteContentPath(assetPath) && !IsBuiltInContentPath(assetPath);
        }

        /// <summary>HotUpdate MonoBehaviour 被序列化进内置内容时返回可诊断的边界错误。</summary>
        public static string GetHotUpdateBehaviourBoundaryError(string assetPath)
        {
            if (CanPlaceHotUpdateMonoBehaviourIn(assetPath))
            {
                return string.Empty;
            }

            return $"'{NormalizePath(assetPath)}' contains a HotUpdate MonoBehaviour but is not in a YooAsset remote-content root. Load the assembly before loading this content, or move the asset to '{RemoteContentRoot}'.";
        }

        internal static bool IsAtOrBelow(string path, string root)
        {
            string normalizedPath = NormalizePath(path);
            string normalizedRoot = NormalizePath(root);
            return string.Equals(normalizedPath, normalizedRoot, StringComparison.OrdinalIgnoreCase) ||
                   normalizedPath.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>解析 asmdef 依赖边界时使用的最小程序集定义数据。</summary>
    public sealed class HotUpdateAssemblyDefinition
    {
        /// <summary>创建程序集定义信息。</summary>
        public HotUpdateAssemblyDefinition(
            string name,
            string path,
            string guid,
            IEnumerable<string> references,
            bool isEditorOnly = false)
        {
            Name = name?.Trim() ?? string.Empty;
            Path = HotUpdateDevelopmentConvention.NormalizePath(path);
            Guid = NormalizeGuid(guid);
            IsEditorOnly = isEditorOnly || HotUpdateDevelopmentConvention.IsEditorOnlyPath(Path);
            References = (references ?? Array.Empty<string>())
                .Where(reference => !string.IsNullOrWhiteSpace(reference))
                .Select(reference => reference.Trim())
                .ToArray();
        }

        /// <summary>程序集名称。</summary>
        public string Name { get; }

        /// <summary>asmdef 的 Unity 项目相对路径。</summary>
        public string Path { get; }

        /// <summary>asmdef .meta 中的 GUID。</summary>
        public string Guid { get; }

        /// <summary>该程序集只包含 Editor 代码，不参与 Player 依赖方向校验。</summary>
        public bool IsEditorOnly { get; }

        /// <summary>asmdef 显式引用的名称或 GUID。</summary>
        public IReadOnlyList<string> References { get; }

        /// <summary>程序集名是否属于 HotUpdate 层。</summary>
        public bool IsHotUpdate => HotUpdateDevelopmentConvention.IsHotUpdateAssemblyName(Name) ||
                                   HotUpdateDevelopmentConvention.IsAtOrBelow(Path, HotUpdateDevelopmentConvention.HotUpdateRoot);

        private static string NormalizeGuid(string guid)
        {
            return string.IsNullOrWhiteSpace(guid) ? string.Empty : guid.Trim().ToLowerInvariant();
        }
    }

    /// <summary>检测 Base App 程序集反向引用 HotUpdate 程序集的结果。</summary>
    public sealed class HotUpdateDependencyBoundaryViolation
    {
        /// <summary>创建依赖边界诊断。</summary>
        public HotUpdateDependencyBoundaryViolation(string sourceAssembly, string targetAssembly, string sourcePath)
        {
            SourceAssembly = sourceAssembly ?? string.Empty;
            TargetAssembly = targetAssembly ?? string.Empty;
            SourcePath = HotUpdateDevelopmentConvention.NormalizePath(sourcePath);
        }

        /// <summary>反向引用的 Base App 程序集。</summary>
        public string SourceAssembly { get; }

        /// <summary>被 Base App 引用的 HotUpdate 程序集。</summary>
        public string TargetAssembly { get; }

        /// <summary>源 asmdef 路径。</summary>
        public string SourcePath { get; }

        /// <summary>适合展示给开发者的错误信息。</summary>
        public string Message => $"Base assembly '{SourceAssembly}' references HotUpdate assembly '{TargetAssembly}' at '{SourcePath}'. Base App must not depend on HotUpdate types.";
    }

    /// <summary>纯数据级 Base → HotUpdate 依赖方向检查器。</summary>
    public static class HotUpdateDependencyBoundaryPolicy
    {
        /// <summary>找出所有 Base App asmdef 对 HotUpdate asmdef 的直接引用。</summary>
        public static IReadOnlyList<HotUpdateDependencyBoundaryViolation> FindViolations(
            IEnumerable<HotUpdateAssemblyDefinition> definitions)
        {
            HotUpdateAssemblyDefinition[] all = (definitions ?? Array.Empty<HotUpdateAssemblyDefinition>())
                .Where(definition => definition != null)
                .ToArray();
            Dictionary<string, HotUpdateAssemblyDefinition> byName = all
                .Where(definition => !string.IsNullOrWhiteSpace(definition.Name))
                .GroupBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            Dictionary<string, HotUpdateAssemblyDefinition> byGuid = all
                .Where(definition => !string.IsNullOrWhiteSpace(definition.Guid))
                .GroupBy(definition => definition.Guid, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            var violations = new List<HotUpdateDependencyBoundaryViolation>();
            foreach (HotUpdateAssemblyDefinition source in all)
            {
                if (source.IsHotUpdate || source.IsEditorOnly)
                {
                    continue;
                }

                foreach (string reference in source.References)
                {
                    HotUpdateAssemblyDefinition target = ResolveReference(reference, byName, byGuid);
                    if (target != null && target.IsHotUpdate)
                    {
                        violations.Add(new HotUpdateDependencyBoundaryViolation(
                            source.Name,
                            target.Name,
                            source.Path));
                    }
                }
            }

            return violations;
        }

        private static HotUpdateAssemblyDefinition ResolveReference(
            string reference,
            IReadOnlyDictionary<string, HotUpdateAssemblyDefinition> byName,
            IReadOnlyDictionary<string, HotUpdateAssemblyDefinition> byGuid)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                return null;
            }

            const string guidPrefix = "GUID:";
            if (reference.StartsWith(guidPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string guid = reference.Substring(guidPrefix.Length).Trim().ToLowerInvariant();
                return byGuid.TryGetValue(guid, out HotUpdateAssemblyDefinition byGuidDefinition)
                    ? byGuidDefinition
                    : null;
            }

            return byName.TryGetValue(reference.Trim(), out HotUpdateAssemblyDefinition byNameDefinition)
                ? byNameDefinition
                : null;
        }
    }
}
