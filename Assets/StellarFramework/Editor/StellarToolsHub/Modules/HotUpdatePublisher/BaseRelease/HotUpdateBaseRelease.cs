using System;
using UnityEditor;

namespace StellarFramework.Editor.HotUpdatePublisher
{
    /// <summary>与一个已发布 Base App 精确绑定的 HybridCLR AOT metadata 快照。</summary>
    [Serializable]
    public sealed class HotUpdateBaseRelease
    {
        /// <summary>BaseRelease JSON schema 版本。</summary>
        public int SchemaVersion = 1;

        /// <summary>Base App 版本。</summary>
        public string BaseAppVersion;

        /// <summary>构建目标平台。</summary>
        public BuildTarget Platform;

        /// <summary>目标 Player 架构，例如 ARM64 或 x86_64。</summary>
        public string Architecture;

        /// <summary>Unity Editor 版本。</summary>
        public string UnityVersion;

        /// <summary>HybridCLR package/runtime 版本或固定 source revision。</summary>
        public string HybridCLRVersion;

        /// <summary>YooAsset package 版本。</summary>
        public string YooAssetVersion;

        /// <summary>Player 使用的脚本后端。</summary>
        public ScriptingImplementation ScriptingBackend;

        /// <summary>生成该 Base App 的 Git commit。</summary>
        public string GitCommit;

        /// <summary>BaseRelease 创建时间的 ISO-8601 UTC 文本。</summary>
        public string CreatedAt;

        /// <summary>复制到 BaseRelease/AotMetadata 的 metadata 文件名。</summary>
        public string[] AotMetadata;

        /// <summary>与 AotMetadata 同序的 lowercase SHA256。</summary>
        public string[] AotHashes;
    }

    /// <summary>创建 BaseRelease 时的目标构建及 metadata 来源。</summary>
    public sealed class HotUpdateBaseReleaseCreateRequest
    {
        /// <summary>Base App 版本。</summary>
        public string BaseAppVersion { get; set; } = string.Empty;

        /// <summary>构建目标平台。</summary>
        public BuildTarget Platform { get; set; }

        /// <summary>目标 Player 架构。</summary>
        public string Architecture { get; set; } = string.Empty;

        /// <summary>Unity Editor 版本。</summary>
        public string UnityVersion { get; set; } = string.Empty;

        /// <summary>HybridCLR package/runtime 版本或固定 source revision。</summary>
        public string HybridCLRVersion { get; set; } = string.Empty;

        /// <summary>YooAsset package 版本。</summary>
        public string YooAssetVersion { get; set; } = string.Empty;

        /// <summary>Player 使用的脚本后端。</summary>
        public ScriptingImplementation ScriptingBackend { get; set; }

        /// <summary>生成该 Base App 的 Git commit。</summary>
        public string GitCommit { get; set; } = string.Empty;

        /// <summary>由本次 Base App 构建实际生成的 AOT metadata DLL 源路径。</summary>
        public string[] AotMetadataSourcePaths { get; set; } = Array.Empty<string>();
    }

    /// <summary>Hot Patch 目标运行环境要求匹配的 BaseRelease 信息。</summary>
    public sealed class HotUpdateBaseReleaseRequirements
    {
        /// <summary>期望绑定的 Base App 版本。</summary>
        public string BaseAppVersion { get; set; } = string.Empty;

        /// <summary>Hot Patch 的构建目标平台。</summary>
        public BuildTarget Platform { get; set; }

        /// <summary>Hot Patch 的目标 Player 架构。</summary>
        public string Architecture { get; set; } = string.Empty;

        /// <summary>当前 Unity Editor 版本。</summary>
        public string UnityVersion { get; set; } = string.Empty;

        /// <summary>当前 HybridCLR package/runtime 版本或固定 source revision。</summary>
        public string HybridCLRVersion { get; set; } = string.Empty;

        /// <summary>当前 YooAsset package 版本。</summary>
        public string YooAssetVersion { get; set; } = string.Empty;

        /// <summary>目标 Base App 使用的脚本后端。</summary>
        public ScriptingImplementation ScriptingBackend { get; set; }
    }

    /// <summary>BaseRelease 校验发现的一项兼容性或文件完整性问题。</summary>
    public sealed class HotUpdateBaseReleaseValidationIssue
    {
        internal HotUpdateBaseReleaseValidationIssue(
            HotUpdateBaseReleaseValidationIssueCode code,
            string message)
        {
            Code = code;
            Message = message ?? string.Empty;
        }

        /// <summary>稳定的问题代码。</summary>
        public HotUpdateBaseReleaseValidationIssueCode Code { get; }

        /// <summary>供 Editor UI 显示的问题详情。</summary>
        public string Message { get; }
    }

    /// <summary>BaseRelease 校验的完整结果。</summary>
    public sealed class HotUpdateBaseReleaseValidationResult
    {
        internal HotUpdateBaseReleaseValidationResult(
            HotUpdateBaseRelease release,
            HotUpdateBaseReleaseValidationIssue[] issues)
        {
            Release = release;
            Issues = issues ?? Array.Empty<HotUpdateBaseReleaseValidationIssue>();
        }

        /// <summary>从仓库重新读取的权威发布记录。</summary>
        public HotUpdateBaseRelease Release { get; }

        /// <summary>结构化校验问题；为空表示可用于该 Hot Patch 目标。</summary>
        public HotUpdateBaseReleaseValidationIssue[] Issues { get; }

        /// <summary>所有平台、版本、后端和 metadata 完整性校验均通过。</summary>
        public bool IsValid => Issues.Length == 0;
    }

    /// <summary>BaseRelease 文件记录的稳定问题代码。</summary>
    public enum HotUpdateBaseReleaseValidationIssueCode
    {
        MissingField = 0,
        UnsupportedSchemaVersion = 1,
        PlatformMismatch = 2,
        BaseAppVersionMismatch = 3,
        ArchitectureMismatch = 4,
        UnityVersionMismatch = 5,
        HybridCLRVersionMismatch = 6,
        YooAssetVersionMismatch = 7,
        ScriptingBackendMismatch = 8,
        MetadataListInvalid = 9,
        MetadataFileMissing = 10,
        MetadataHashMismatch = 11
    }
}
