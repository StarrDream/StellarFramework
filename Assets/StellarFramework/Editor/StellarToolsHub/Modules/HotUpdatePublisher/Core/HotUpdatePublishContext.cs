using System;
using System.Collections.Generic;
using UnityEditor;

namespace StellarFramework.Editor.HotUpdatePublisher
{
    /// <summary>
    /// 一次发布事务的共享输入和阶段产物。字段由配置、Preflight 及构建阶段逐步填充，
    /// 该对象只在 Editor Publisher 流程中存在。
    /// </summary>
    public sealed class HotUpdatePublishContext
    {
        /// <summary>目标 Player 平台。</summary>
        public BuildTarget Platform { get; set; }

        /// <summary>开发、测试、预发布或生产环境标识。</summary>
        public string Environment { get; set; } = string.Empty;

        /// <summary>当前补丁要求的 Base App 版本。</summary>
        public string BaseAppVersion { get; set; } = string.Empty;

        /// <summary>经仓库加载并与目标构建兼容性校验的 Base Release；Hot Patch 不从临时 metadata 回退。</summary>
        public HotUpdateBaseRelease SelectedBaseRelease { get; private set; }

        /// <summary>校验已选 BaseRelease 时使用的完整目标兼容条件。</summary>
        public HotUpdateBaseReleaseRequirements SelectedBaseReleaseRequirements { get; private set; }

        /// <summary>由 HybridCLR 编译阶段产生的 HotUpdate DLL 源目录。</summary>
        public string CompiledHotUpdateDirectory { get; set; } = string.Empty;

        /// <summary>HotUpdate DLL/AOT metadata 的 YooAsset 收集资源根目录。</summary>
        public string HotUpdateAssetOutputRoot { get; set; } = "Assets/GameHotUpdate";

        /// <summary>HotUpdate Manifest JSON 的收集资源路径。</summary>
        public string HotUpdateManifestAssetPath { get; set; } = "Assets/GameHotUpdate/Manifest/HotUpdateManifest.json";

        /// <summary>Manifest 中的 HotUpdate 入口类。</summary>
        public string HotUpdateEntryClass { get; set; } = "HotUpdate.HotUpdateMain";

        /// <summary>Manifest 中的 HotUpdate 入口方法。</summary>
        public string HotUpdateEntryMethod { get; set; } = "Main";

        /// <summary>本次编译的 HotUpdate 程序集名；为空时使用 HybridCLR 当前生成目录内所有 DLL。</summary>
        public string[] HotUpdateAssemblyNames { get; set; } = Array.Empty<string>();

        /// <summary>是否使用 HybridCLR 开发构建选项编译。</summary>
        public bool DevelopmentBuild { get; set; }

        /// <summary>此操作是否是明确的 Base App 发布；要求 Full Gate，不能将其作为普通 Hot Patch 上传。</summary>
        public bool IsBaseAppRelease { get; set; }

        /// <summary>是否由发布负责人明确标记为重大 Hot Patch；要求 Full Gate。</summary>
        public bool IsMajorHotPatch { get; set; }

        /// <summary>YooAsset 内置构建压缩模式：Uncompressed、LZMA 或 LZ4。</summary>
        public string YooAssetCompression { get; set; } = "LZ4";

        /// <summary>YooAsset 构建输出根目录；需由发布配置显式设置。</summary>
        public string YooAssetBuildOutputRoot { get; set; } = string.Empty;

        /// <summary>HybridCLR 导出完成的报告。</summary>
        public HybridCLRBuildOutput HybridCLRBuildOutput { get; set; }

        /// <summary>YooAsset 构建完成的结构化产物报告。</summary>
        public YooAssetBuildOutput YooAssetBuildOutput { get; set; }

        /// <summary>YooAsset package 名称。</summary>
        public string PackageName { get; set; } = string.Empty;

        /// <summary>本次发布的不可变标识。</summary>
        public string ReleaseId { get; set; } = string.Empty;

        /// <summary>YooAsset package 版本。</summary>
        public string PackageVersion { get; set; } = string.Empty;

        /// <summary>发布说明。</summary>
        public string ReleaseNotes { get; set; } = string.Empty;

        /// <summary>开始发布时读取的 Git 分支。</summary>
        public string GitBranch { get; set; } = string.Empty;

        /// <summary>开始发布时读取的 Git commit。</summary>
        public string GitCommit { get; set; } = string.Empty;

        /// <summary>工作区是否含 staged、unstaged 或 untracked 改动。</summary>
        public bool GitDirty { get; set; }

        /// <summary>Preflight 产生的变更安全分类。</summary>
        public HotUpdateChangeClassificationResult ChangeClassification { get; set; }

        /// <summary>YooAsset 与 HybridCLR 阶段生成的构建输出目录。</summary>
        public string BuildOutput { get; set; } = string.Empty;

        /// <summary>目标发布适配器或 Server Profile 的标识。</summary>
        public string PublishTarget { get; set; } = string.Empty;

        /// <summary>远端发布根目录，不含具体 PackageVersion 指针。</summary>
        public string ServerRoot { get; set; } = string.Empty;

        /// <summary>实际发布目标实例；目标凭证和 SDK 实现不会暴露给 Publisher Core。</summary>
        public IHotUpdatePublishTarget PublishTargetAdapter { get; set; }

        /// <summary>更新 PackageVersion 指针前预期的远端当前版本；首次发布为空字符串。</summary>
        public string ExpectedCurrentPackageVersion { get; set; } = string.Empty;

        /// <summary>PrepareUpload 根据 YooAsset 输出创建的最后发布版本指针请求。</summary>
        public HotUpdateVersionPublishRequest VersionPublishRequest { get; internal set; }

        /// <summary>从完整 YooAsset 输出快照出的不可变上传文件；PackageVersion 指针文件单独最后发布。</summary>
        public IReadOnlyList<HotUpdatePublishFile> PublishFiles { get; private set; } = Array.Empty<HotUpdatePublishFile>();

        /// <summary>是否已完成全部不可变文件上传。</summary>
        public bool ImmutableUploadCompleted { get; private set; }

        /// <summary>是否已完成目标端哈希验证及发布前远端验证。</summary>
        public bool RemotePublishVerificationCompleted { get; private set; }

        /// <summary>Finalize 阶段产生的机器可读发布记录。</summary>
        public HotUpdateReleaseRecord ReleaseRecord { get; set; }

        /// <summary>已通过的 Fast/Full Gate 机器证据摘要。</summary>
        public HotUpdateReleaseGateReport ReleaseGateReport { get; internal set; }

        internal void SetPublishFiles(IReadOnlyList<HotUpdatePublishFile> files)
        {
            PublishFiles = files ?? throw new ArgumentNullException(nameof(files));
            ImmutableUploadCompleted = false;
            RemotePublishVerificationCompleted = false;
        }

        internal void MarkImmutableUploadCompleted()
        {
            ImmutableUploadCompleted = true;
            RemotePublishVerificationCompleted = false;
        }

        internal void MarkRemotePublishVerificationCompleted()
        {
            if (!ImmutableUploadCompleted)
                throw new InvalidOperationException("Remote verification cannot complete before immutable upload.");
            RemotePublishVerificationCompleted = true;
        }

        /// <summary>只从 BaseRelease 仓库加载并校验选中的版本；不会回退到 HybridCLRData。</summary>
        public HotUpdateBaseRelease SelectBaseRelease(
            HotUpdateBaseReleaseRepository repository,
            HotUpdateBaseReleaseRequirements requirements)
        {
            if (repository == null)
            {
                throw new ArgumentNullException(nameof(repository));
            }

            if (requirements == null)
            {
                throw new ArgumentNullException(nameof(requirements));
            }

            HotUpdateBaseRelease selected = repository.LoadAndValidate(
                requirements.Platform,
                requirements.BaseAppVersion,
                requirements);
            SelectedBaseRelease = selected;
            SelectedBaseReleaseRequirements = requirements;
            BaseAppVersion = selected.BaseAppVersion;
            Platform = selected.Platform;
            return selected;
        }
    }

    /// <summary>一次已验证的 HotUpdate 发布记录；存储和历史索引由后续阶段负责。</summary>
    [Serializable]
    public sealed class HotUpdateReleaseRecord
    {
        /// <summary>不可变发布标识。</summary>
        public string ReleaseId;

        /// <summary>YooAsset package 名称。</summary>
        public string PackageName;

        /// <summary>YooAsset package 版本。</summary>
        public string PackageVersion;

        /// <summary>兼容的 Base App 版本。</summary>
        public string BaseAppVersion;

        /// <summary>构建目标平台。</summary>
        public BuildTarget Platform;

        /// <summary>服务器环境标识。</summary>
        public string Environment;

        /// <summary>本次发布说明。</summary>
        public string ReleaseNotes;

        /// <summary>生成该发布的 Git commit。</summary>
        public string GitCommit;

        /// <summary>生成该发布的 Git 分支。</summary>
        public string GitBranch;

        /// <summary>生成该发布时工作区是否有改动。</summary>
        public bool GitDirty;

        /// <summary>HotUpdate DLL 的 SHA256。</summary>
        public string HotUpdateDllSha256;

        /// <summary>YooAsset bundle 数。</summary>
        public int BundleCount;

        /// <summary>分类结果快照，避免历史记录依赖当前 Git 工作区。</summary>
        public HotUpdateReleaseChangeClassification ChangeClassification;

        /// <summary>本次发布的不可变远端文件路径、字节数和 SHA256。</summary>
        public HotUpdateReleaseFileRecord[] Files = Array.Empty<HotUpdateReleaseFileRecord>();

        /// <summary>YooAsset version/manifest filenames required to verify or restore this historical package.</summary>
        public string[] ManifestFiles = Array.Empty<string>();

        /// <summary>本次产物总字节数。</summary>
        public long TotalBytes;

        /// <summary>Fast/Full Release Gate 的机器结果摘要。</summary>
        public string GateResult;

        /// <summary>远端服务器发布根目录。</summary>
        public string ServerRoot;

        /// <summary>记录创建时间（UTC）。</summary>
        public DateTime CreatedAtUtc;

        /// <summary>当前远端发布状态。</summary>
        public HotUpdateReleaseRecordStatus Status;
    }

    /// <summary>在 release history 中持久化的 Change Classifier 汇总。</summary>
    [Serializable]
    public sealed class HotUpdateReleaseChangeClassification
    {
        public int GreenCount;
        public int YellowCount;
        public int RedCount;
        public int DependencyViolationCount;
        public string Safety;
    }

    /// <summary>供回滚前远端完整性校验使用的历史发布文件证据。</summary>
    [Serializable]
    public sealed class HotUpdateReleaseFileRecord : IHotUpdateRemoteFile
    {
        public string RelativePath;
        public long Length;
        public string Sha256;

        string IHotUpdateRemoteFile.RelativePath => RelativePath;
        long IHotUpdateRemoteFile.Length => Length;
    }

    /// <summary>追加到 History 的发布或回滚事件；发布记录本体保留可追溯状态。</summary>
    [Serializable]
    public sealed class HotUpdateReleaseHistoryEvent
    {
        public string EventId;
        public string ReleaseId;
        public string EventType;
        public string FromVersion;
        public string ToVersion;
        public string Diagnostic;
        public DateTime CreatedAtUtc;
    }

    /// <summary>发布记录的生命周期状态。</summary>
    public enum HotUpdateReleaseRecordStatus
    {
        Prepared = 0,
        Active = 1,
        Superseded = 2,
        RolledBack = 3,
        Failed = 4,
        RollbackUnverified = 5
    }
}
