using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using YooAsset;
using YooAsset.Editor;

namespace StellarFramework.Editor.HotUpdatePublisher
{
    /// <summary>YooAsset Editor build SDK 的中立返回类型。</summary>
    public sealed class YooAssetBuildRunnerResult
    {
        public bool Success { get; set; }
        public string Error { get; set; } = string.Empty;
        public string OutputDirectory { get; set; } = string.Empty;
    }

    /// <summary>可替换 YooAsset Builder 以便测试阶段参数映射和错误传播。</summary>
    public interface IYooAssetBuildRunner
    {
        YooAssetBuildRunnerResult Build(BuildTarget target, string packageName, string packageVersion, string outputRoot, string compression);
    }

    /// <summary>将发布参数传给共用的 YooAsset 资源构建器；保留发布器的验证包限制。</summary>
    public sealed class YooAssetBuiltinBuildRunner : IYooAssetBuildRunner
    {
        public YooAssetBuildRunnerResult Build(
            BuildTarget target, string packageName, string packageVersion, string outputRoot, string compression)
        {
            if (string.Equals(packageName, "StellarHotUpdateVerification", StringComparison.OrdinalIgnoreCase))
                return new YooAssetBuildRunnerResult { Success = false, Error = "The verification-only YooAsset package cannot be used for production publishing." };
            if (!Enum.TryParse(compression, true, out ECompressOption option) || !Enum.IsDefined(typeof(ECompressOption), option))
                return new YooAssetBuildRunnerResult { Success = false, Error = $"Unsupported YooAsset compression option '{compression}'." };
            StellarFramework.Editor.ResourceBuild.YooAssetResourceBuildResult result =
                StellarFramework.Editor.ResourceBuild.YooAssetResourceBuilder.Build(
                    new StellarFramework.Editor.ResourceBuild.YooAssetResourceBuildRequest
                    {
                        Target = target, PackageName = packageName, Version = packageVersion,
                        OutputRoot = outputRoot, Compression = option
                    });
            return new YooAssetBuildRunnerResult
            {
                Success = result.Success, Error = result.Error, OutputDirectory = result.OutputDirectory
            };
        }
    }

    /// <summary>将发布上下文映射到 YooAsset BuiltinBuildParameters 并检查完整产物。</summary>
    public sealed class YooAssetHotUpdateBuildAdapter : IHotUpdateBuildAdapter
    {
        private static readonly string[] NonPublishOutputFiles = { "OutputCache", "OutputCache.manifest" };
        private readonly IYooAssetBuildRunner _runner;

        public YooAssetHotUpdateBuildAdapter(IYooAssetBuildRunner runner = null)
        {
            _runner = runner ?? new YooAssetBuiltinBuildRunner();
        }

        public Task<HotUpdatePublishStepResult> CompileHotUpdateAsync(HotUpdatePublishContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(HotUpdatePublishStepResult.Failed(
                HotUpdatePublishErrorCode.StageFailed, "HotUpdate compilation is provided by the HybridCLR adapter."));
        }

        public Task<HotUpdatePublishStepResult> ExportHybridCLRAssetsAsync(HotUpdatePublishContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(HotUpdatePublishStepResult.Failed(
                HotUpdatePublishErrorCode.StageFailed, "HybridCLR asset export is provided by the HybridCLR adapter."));
        }

        public Task<HotUpdatePublishStepResult> BuildYooAssetAsync(
            HotUpdatePublishContext context, CancellationToken cancellationToken)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            cancellationToken.ThrowIfCancellationRequested();
            if (context.HybridCLRBuildOutput == null)
                return Task.FromResult(Failed("HybridCLR DLL, metadata and Manifest must be prepared before YooAsset build."));
            if (context.Platform == BuildTarget.NoTarget || string.IsNullOrWhiteSpace(context.PackageName) ||
                string.IsNullOrWhiteSpace(context.PackageVersion) || string.IsNullOrWhiteSpace(context.YooAssetBuildOutputRoot))
                return Task.FromResult(Failed("BuildTarget, package name/version and output root are required."));

            YooAssetBuildRunnerResult result = _runner.Build(
                context.Platform, context.PackageName, context.PackageVersion,
                context.YooAssetBuildOutputRoot, context.YooAssetCompression);
            if (result == null)
                return Task.FromResult(Failed("YooAsset build runner returned no result."));
            if (!result.Success)
                return Task.FromResult(Failed(string.IsNullOrWhiteSpace(result.Error)
                    ? "YooAsset build runner reported failure without diagnostics."
                    : result.Error));
            if (string.IsNullOrWhiteSpace(result.OutputDirectory) || !Directory.Exists(result.OutputDirectory))
                return Task.FromResult(Failed("YooAsset build reported success but its output directory does not exist."));

            string[] manifestFiles =
            {
                YooAssetSettingsData.GetManifestBinaryFileName(context.PackageName, context.PackageVersion),
                YooAssetSettingsData.GetManifestJsonFileName(context.PackageName, context.PackageVersion),
                YooAssetSettingsData.GetPackageHashFileName(context.PackageName, context.PackageVersion),
                YooAssetSettingsData.GetPackageVersionFileName(context.PackageName)
            };
            for (int index = 0; index < manifestFiles.Length; index++)
            {
                if (!File.Exists(Path.Combine(result.OutputDirectory, manifestFiles[index])))
                    return Task.FromResult(Failed($"YooAsset build is missing expected manifest/catalog file '{manifestFiles[index]}'."));
            }

            string[] files = Directory.GetFiles(result.OutputDirectory, "*", SearchOption.AllDirectories);
            int bundleCount = files.Count(path => path.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase));
            if (bundleCount == 0)
                return Task.FromResult(Failed("YooAsset build produced no bundle files."));
            long totalBytes = files
                .Where(path => !NonPublishOutputFiles.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
                .Sum(path => new FileInfo(path).Length);
            context.BuildOutput = result.OutputDirectory;
            context.YooAssetBuildOutput = new YooAssetBuildOutput
            {
                PackageVersion = context.PackageVersion,
                OutputDirectory = result.OutputDirectory,
                BundleCount = bundleCount,
                TotalBytes = totalBytes,
                ManifestFiles = manifestFiles,
                NonPublishOutputFiles = (string[])NonPublishOutputFiles.Clone()
            };
            return Task.FromResult(HotUpdatePublishStepResult.Succeeded());
        }

        private static HotUpdatePublishStepResult Failed(string message)
        {
            return HotUpdatePublishStepResult.Failed(HotUpdatePublishErrorCode.StageFailed, message);
        }
    }

    /// <summary>Registers the optional YooAsset SDK implementation with the SDK-neutral Publisher Hub.</summary>
    [InitializeOnLoad]
    internal static class HotUpdatePublisherYooAssetAdapterRegistration
    {
        static HotUpdatePublisherYooAssetAdapterRegistration()
        {
            HotUpdatePublisherBuildAdapters.YooAssetFactory = () => new YooAssetHotUpdateBuildAdapter();
        }
    }
}
