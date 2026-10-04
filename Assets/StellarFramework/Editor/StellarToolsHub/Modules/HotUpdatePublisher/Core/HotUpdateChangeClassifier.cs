using System;
using System.Collections.Generic;
using System.Linq;

namespace StellarFramework.Editor.HotUpdatePublisher
{
    /// <summary>单个工作区变更允许的发布安全等级。</summary>
    public enum HotUpdateChangeSafety
    {
        /// <summary>已知可进入远端热更层；要求 Fast Gate。</summary>
        Green = 0,
        /// <summary>可继续构建，但需要完整 Gate 或人工复核。</summary>
        Yellow = 1,
        /// <summary>触及 Base App 边界，普通 Hot Patch 必须阻止。</summary>
        Red = 2
    }

    /// <summary>Git 工作区变更类型。</summary>
    public enum HotUpdateChangeKind
    {
        Unknown = 0,
        Added = 1,
        Modified = 2,
        Deleted = 3,
        Renamed = 4,
        Copied = 5,
        TypeChanged = 6
    }

    /// <summary>来自 Git porcelain status 的一个项目相对路径。</summary>
    public sealed class HotUpdateWorkspaceChange
    {
        /// <summary>创建工作区变更记录。</summary>
        public HotUpdateWorkspaceChange(string path, string gitStatus, HotUpdateChangeKind kind)
        {
            Path = HotUpdateDevelopmentConvention.NormalizePath(path);
            GitStatus = gitStatus ?? string.Empty;
            Kind = kind;
        }

        /// <summary>项目根目录相对路径。</summary>
        public string Path { get; }

        /// <summary>Git porcelain 的两字符状态。</summary>
        public string GitStatus { get; }

        /// <summary>添加、修改、删除或重命名。</summary>
        public HotUpdateChangeKind Kind { get; }
    }

    /// <summary>
    /// 分类器所需的静态资产事实。生产环境由 Git 与 Unity Editor 适配器填充，
    /// 测试可直接提供事实而无需启动构建流程。
    /// </summary>
    public sealed class HotUpdateChangeFacts
    {
        /// <summary>创建单个变更的分类事实。</summary>
        public HotUpdateChangeFacts(
            string path,
            HotUpdateChangeAssetKind assetKind,
            HotUpdateProjectLayer layer,
            string gitStatus = null,
            HotUpdateChangeKind changeKind = HotUpdateChangeKind.Modified,
            string assemblyName = null,
            bool assemblyReferencesChanged = false,
            bool isPluginImporter = false,
            bool containsHotUpdateMonoBehaviour = false,
            bool hasAotSensitiveApiDependency = false)
        {
            Path = HotUpdateDevelopmentConvention.NormalizePath(path);
            AssetKind = assetKind;
            Layer = layer;
            GitStatus = gitStatus ?? string.Empty;
            ChangeKind = changeKind;
            AssemblyName = assemblyName ?? string.Empty;
            AssemblyReferencesChanged = assemblyReferencesChanged;
            IsPluginImporter = isPluginImporter;
            ContainsHotUpdateMonoBehaviour = containsHotUpdateMonoBehaviour;
            HasAotSensitiveApiDependency = hasAotSensitiveApiDependency;
        }

        /// <summary>项目根目录相对路径。</summary>
        public string Path { get; }

        /// <summary>Unity 资产类型类别。</summary>
        public HotUpdateChangeAssetKind AssetKind { get; }

        /// <summary>Base、HotUpdate、远端内容或内置内容归属。</summary>
        public HotUpdateProjectLayer Layer { get; }

        /// <summary>Git porcelain 状态。</summary>
        public string GitStatus { get; }

        /// <summary>Git 变更类型。</summary>
        public HotUpdateChangeKind ChangeKind { get; }

        /// <summary>由最近的 asmdef 解析出的程序集名。</summary>
        public string AssemblyName { get; }

        /// <summary>此 asmdef 变更触及程序集引用或其他编译边界。</summary>
        public bool AssemblyReferencesChanged { get; }

        /// <summary>Unity 将该文件识别为原生/平台插件导入项。</summary>
        public bool IsPluginImporter { get; }

        /// <summary>Prefab/Scene 或其嵌套 Prefab 序列化了 HotUpdate 程序集中的 MonoBehaviour。</summary>
        public bool ContainsHotUpdateMonoBehaviour { get; }

        /// <summary>源代码包含需 IL2CPP/AOT 完整验证的已知 API 使用。</summary>
        public bool HasAotSensitiveApiDependency { get; }
    }

    /// <summary>一项工作区变更的安全分类和原因。</summary>
    public sealed class HotUpdateClassifiedChange
    {
        internal HotUpdateClassifiedChange(
            HotUpdateChangeFacts facts,
            HotUpdateChangeSafety safety,
            string reason,
            bool producesPlayerPayload)
        {
            Facts = facts;
            Safety = safety;
            Reason = reason ?? string.Empty;
            ProducesPlayerPayload = producesPlayerPayload;
        }

        /// <summary>解析后的变更事实。</summary>
        public HotUpdateChangeFacts Facts { get; }

        /// <summary>GREEN、YELLOW 或 RED。</summary>
        public HotUpdateChangeSafety Safety { get; }

        /// <summary>供 Preflight 展示的分类原因。</summary>
        public string Reason { get; }

        /// <summary>该修改本身是否会进入 Player/远端发布包。</summary>
        public bool ProducesPlayerPayload { get; }
    }

    /// <summary>完整工作区分类结果，包含依赖边界违规和每项变更的诊断。</summary>
    public sealed class HotUpdateChangeClassificationResult
    {
        internal HotUpdateChangeClassificationResult(
            IReadOnlyList<HotUpdateClassifiedChange> changes,
            IReadOnlyList<HotUpdateDependencyBoundaryViolation> dependencyViolations)
        {
            Changes = changes ?? Array.Empty<HotUpdateClassifiedChange>();
            DependencyViolations = dependencyViolations ?? Array.Empty<HotUpdateDependencyBoundaryViolation>();
            GreenCount = Changes.Count(change => change.Safety == HotUpdateChangeSafety.Green);
            YellowCount = Changes.Count(change => change.Safety == HotUpdateChangeSafety.Yellow);
            RedCount = Changes.Count(change => change.Safety == HotUpdateChangeSafety.Red);
        }

        /// <summary>分类明细。</summary>
        public IReadOnlyList<HotUpdateClassifiedChange> Changes { get; }

        /// <summary>Base App 对 HotUpdate 的反向程序集引用。</summary>
        public IReadOnlyList<HotUpdateDependencyBoundaryViolation> DependencyViolations { get; }

        /// <summary>Green 变更数。</summary>
        public int GreenCount { get; }

        /// <summary>Yellow 变更数。</summary>
        public int YellowCount { get; }

        /// <summary>Red 变更数。</summary>
        public int RedCount { get; }

        /// <summary>没有 Red 变更或 Base → HotUpdate 依赖违规时为 true。</summary>
        public bool CanHotPatch => RedCount == 0 && DependencyViolations.Count == 0;

        /// <summary>任一 Yellow 变更要求 Full Gate。</summary>
        public bool RequiresFullGate => YellowCount > 0;

        /// <summary>有可发布内容且无阻断项时要求 Fast Gate。</summary>
        public bool RequiresFastGate => CanHotPatch && Changes.Any(change => change.ProducesPlayerPayload);

        /// <summary>工作区是否包含可发布的 Player/远端内容修改。</summary>
        public bool HasPlayerPayload => Changes.Any(change => change.ProducesPlayerPayload);
    }

    /// <summary>读取 Git 工作区改动的适配接口。</summary>
    public interface IHotUpdateWorkspaceChangeSource
    {
        /// <summary>返回已跟踪修改、暂存修改和未跟踪文件。</summary>
        IReadOnlyList<HotUpdateWorkspaceChange> ReadChanges();
    }

    /// <summary>用 Unity 资产数据库和程序集信息扩展 Git 变更。</summary>
    public interface IHotUpdateChangeFactsProvider
    {
        /// <summary>解析单个工作区文件。</summary>
        HotUpdateChangeFacts Resolve(HotUpdateWorkspaceChange change);

        /// <summary>检查整个 asmdef 图中的 Base → HotUpdate 反向引用。</summary>
        IReadOnlyList<HotUpdateDependencyBoundaryViolation> FindDependencyBoundaryViolations();
    }

    /// <summary>
    /// 将 Git diff/status、asmdef 成员关系、插件导入信息和 Unity 资产类型合并成热更风险等级。
    /// 未知资产采用 Yellow（要求 Full Gate），不因未知而默认放行。
    /// </summary>
    public sealed class HotUpdateChangeClassifier
    {
        private readonly IHotUpdateWorkspaceChangeSource _changeSource;
        private readonly IHotUpdateChangeFactsProvider _factsProvider;

        /// <summary>创建可测试且可替换 Git/Unity 适配器的分类器。</summary>
        public HotUpdateChangeClassifier(
            IHotUpdateWorkspaceChangeSource changeSource,
            IHotUpdateChangeFactsProvider factsProvider)
        {
            _changeSource = changeSource ?? throw new ArgumentNullException(nameof(changeSource));
            _factsProvider = factsProvider ?? throw new ArgumentNullException(nameof(factsProvider));
        }

        /// <summary>读取当前工作区并执行分类和程序集方向检查。</summary>
        public HotUpdateChangeClassificationResult AnalyzeWorkspace()
        {
            IReadOnlyList<HotUpdateWorkspaceChange> changes = _changeSource.ReadChanges();
            var facts = new List<HotUpdateChangeFacts>(changes?.Count ?? 0);
            if (changes != null)
            {
                foreach (HotUpdateWorkspaceChange change in changes)
                {
                    facts.Add(_factsProvider.Resolve(change));
                }
            }

            return Classify(facts, _factsProvider.FindDependencyBoundaryViolations());
        }

        /// <summary>按已经解析的资产事实执行纯分类逻辑。</summary>
        public static HotUpdateChangeClassificationResult Classify(
            IEnumerable<HotUpdateChangeFacts> facts,
            IEnumerable<HotUpdateDependencyBoundaryViolation> dependencyViolations = null)
        {
            var changes = new List<HotUpdateClassifiedChange>();
            foreach (HotUpdateChangeFacts item in facts ?? Array.Empty<HotUpdateChangeFacts>())
            {
                if (item == null)
                {
                    continue;
                }

                changes.Add(ClassifyOne(item));
            }

            return new HotUpdateChangeClassificationResult(
                changes,
                (dependencyViolations ?? Array.Empty<HotUpdateDependencyBoundaryViolation>())
                .Where(violation => violation != null)
                .ToArray());
        }

        private static HotUpdateClassifiedChange ClassifyOne(HotUpdateChangeFacts facts)
        {
            if (facts.Layer == HotUpdateProjectLayer.EditorOnly ||
                facts.AssetKind == HotUpdateChangeAssetKind.DocumentationOrTooling)
            {
                return Result(facts, HotUpdateChangeSafety.Green,
                    "Editor, test, documentation, or maintenance-tool change; it does not enter a Player payload.",
                    false);
            }

            if (facts.IsPluginImporter ||
                facts.AssetKind == HotUpdateChangeAssetKind.NativePlugin ||
                IsNativePluginPath(facts.Path))
            {
                return Result(facts, HotUpdateChangeSafety.Red,
                    "Native or platform plugin changes require a new Base App release.", true);
            }

            if (IsProjectConfigurationPath(facts.Path) ||
                facts.AssetKind == HotUpdateChangeAssetKind.ProjectConfiguration)
            {
                return Result(facts, HotUpdateChangeSafety.Red,
                    "Project settings, package manifests, and lock files change the Base App build contract.", true);
            }

            if (facts.ContainsHotUpdateMonoBehaviour &&
                !HotUpdateDevelopmentConvention.CanPlaceHotUpdateMonoBehaviourIn(facts.Path))
            {
                return Result(facts, HotUpdateChangeSafety.Red,
                    HotUpdateDevelopmentConvention.GetHotUpdateBehaviourBoundaryError(facts.Path), true);
            }

            if (facts.Layer == HotUpdateProjectLayer.BaseApp ||
                facts.Layer == HotUpdateProjectLayer.BuiltInContent)
            {
                return Result(facts, HotUpdateChangeSafety.Red,
                    "Base App code or built-in content changed; publish a new Base App release.", true);
            }

            if (facts.AssetKind == HotUpdateChangeAssetKind.Shader ||
                facts.AssetKind == HotUpdateChangeAssetKind.ShaderVariant ||
                facts.AssetKind == HotUpdateChangeAssetKind.ComputeShader)
            {
                return Result(facts, HotUpdateChangeSafety.Yellow,
                    "Shader, shader variant, or compute-shader changes require the Full Gate.", true);
            }

            if (facts.AssetKind == HotUpdateChangeAssetKind.AssemblyDefinition)
            {
                if (facts.Layer == HotUpdateProjectLayer.HotUpdate)
                {
                    return Result(facts, HotUpdateChangeSafety.Yellow,
                        facts.AssemblyReferencesChanged
                            ? "HotUpdate asmdef references or compile settings changed; run the Full Gate."
                            : "HotUpdate assembly layout changed; run the Full Gate.",
                        true);
                }

                return Result(facts, HotUpdateChangeSafety.Red,
                    "A Base App asmdef changed; publish a new Base App release.", true);
            }

            if (facts.AssetKind == HotUpdateChangeAssetKind.CSharpSource)
            {
                if (facts.Layer != HotUpdateProjectLayer.HotUpdate)
                {
                    return Result(facts, HotUpdateChangeSafety.Red,
                        "Changed C# source is not a member of a HotUpdate assembly; publish a new Base App release.", true);
                }

                if (facts.HasAotSensitiveApiDependency)
                {
                    return Result(facts, HotUpdateChangeSafety.Yellow,
                        "The changed HotUpdate source uses an AOT-sensitive API; run the Full Gate.", true);
                }

                return Result(facts, HotUpdateChangeSafety.Green,
                    "C# source belongs to a HotUpdate assembly.", true);
            }

            if ((facts.Layer == HotUpdateProjectLayer.HotUpdate ||
                 facts.Layer == HotUpdateProjectLayer.RemoteContent) &&
                IsRecognizedRemoteAssetKind(facts.AssetKind))
            {
                return Result(facts, HotUpdateChangeSafety.Green,
                    "The asset is assigned to the remote HotUpdate/content layer.", true);
            }

            return Result(facts, HotUpdateChangeSafety.Yellow,
                "Asset deployment layer is not declared by the project convention; configure the remote-content root and run the Full Gate.",
                true);
        }

        private static HotUpdateClassifiedChange Result(
            HotUpdateChangeFacts facts,
            HotUpdateChangeSafety safety,
            string reason,
            bool producesPlayerPayload)
        {
            return new HotUpdateClassifiedChange(facts, safety, reason, producesPlayerPayload);
        }

        private static bool IsNativePluginPath(string path)
        {
            string normalized = HotUpdateDevelopmentConvention.NormalizePath(path);
            string[] segments = normalized.Split('/');
            if (segments.Any(segment => string.Equals(segment, "Plugins", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            string extension = System.IO.Path.GetExtension(normalized);
            return extension.Equals(".aar", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".so", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".dylib", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".bundle", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".dll", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsProjectConfigurationPath(string path)
        {
            string normalized = HotUpdateDevelopmentConvention.NormalizePath(path);
            return HotUpdateDevelopmentConvention.IsAtOrBelow(normalized, "ProjectSettings") ||
                   HotUpdateDevelopmentConvention.IsAtOrBelow(normalized, "Packages");
        }

        private static bool IsRecognizedRemoteAssetKind(HotUpdateChangeAssetKind assetKind)
        {
            switch (assetKind)
            {
                case HotUpdateChangeAssetKind.Scene:
                case HotUpdateChangeAssetKind.Prefab:
                case HotUpdateChangeAssetKind.Material:
                case HotUpdateChangeAssetKind.ScriptableObject:
                case HotUpdateChangeAssetKind.Texture:
                case HotUpdateChangeAssetKind.Audio:
                case HotUpdateChangeAssetKind.Video:
                case HotUpdateChangeAssetKind.HotUpdatePayload:
                case HotUpdateChangeAssetKind.Configuration:
                case HotUpdateChangeAssetKind.Model:
                    return true;
                default:
                    return false;
            }
        }
    }
}
