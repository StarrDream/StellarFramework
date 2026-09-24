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

    /// <summary>直接调用 YooAsset 的正式 BuiltinBuildPipeline；不调用 HotUpdate Verification fixture builder。</summary>
    public sealed class YooAssetBuiltinBuildRunner : IYooAssetBuildRunner
    {
        public YooAssetBuildRunnerResult Build(
            BuildTarget target, string packageName, string packageVersion, string outputRoot, string compression)
        {
            AssetBundleCollectorSetting setting = LoadExistingCollectorSettings(packageName);
            if (setting == null)
                return new YooAssetBuildRunnerResult { Success = false, Error = "A production YooAsset collector setting/package was not found." };
            if (string.Equals(packageName, "StellarHotUpdateVerification", StringComparison.OrdinalIgnoreCase))
                return new YooAssetBuildRunnerResult { Success = false, Error = "The verification-only YooAsset package cannot be used for production publishing." };

            string absoluteOutputRoot = Path.GetFullPath(outputRoot);
            string outputDirectory = Path.Combine(absoluteOutputRoot, target.ToString(), packageName, packageVersion);
            if (Directory.Exists(outputDirectory) || File.Exists(outputDirectory))
                return new YooAssetBuildRunnerResult { Success = false, Error = $"Refusing to overwrite existing YooAsset package output '{outputDirectory}'." };

            if (!Enum.TryParse(compression, true, out ECompressOption compressOption) || !Enum.IsDefined(typeof(ECompressOption), compressOption))
                return new YooAssetBuildRunnerResult { Success = false, Error = $"Unsupported YooAsset compression option '{compression}'." };

            var parameters = new BuiltinBuildParameters
            {
                BuildTarget = target,
                BuildPipeline = EBuildPipeline.BuiltinBuildPipeline.ToString(),
                BuildBundleType = (int)EBuildBundleType.AssetBundle,
                PackageName = packageName,
                PackageVersion = packageVersion,
                BuildOutputRoot = absoluteOutputRoot,
                BuildinFileRoot = AssetBundleBuilderHelper.GetStreamingAssetsRoot(),
                CompressOption = compressOption,
                ClearBuildCacheFiles = false,
                VerifyBuildingResult = true,
                FileNameStyle = EFileNameStyle.HashName
            };
            BuildResult result = new BuiltinBuildPipeline().Run(parameters, true);
            return new YooAssetBuildRunnerResult
            {
                Success = result.Success,
                Error = result.Success ? string.Empty :
                    $"YooAsset build failed at '{result.FailedTask}': {result.ErrorInfo}{Environment.NewLine}{result.ErrorStack}",
                OutputDirectory = result.OutputPackageDirectory
            };
        }

        private static AssetBundleCollectorSetting LoadExistingCollectorSettings(string packageName)
        {
            string[] guids = AssetDatabase.FindAssets("t:AssetBundleCollectorSetting");
            if (guids.Length != 1)
                throw new InvalidOperationException(
                    $"Expected exactly one existing YooAsset collector setting, found {guids.Length}. Refusing to create or guess project configuration.");
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var setting = AssetDatabase.LoadAssetAtPath<AssetBundleCollectorSetting>(path);
            if (setting == null)
                throw new InvalidOperationException($"YooAsset collector setting at '{path}' could not be loaded.");
            if (setting.Packages == null || !setting.Packages.Any(item =>
                    item != null && string.Equals(item.PackageName, packageName, StringComparison.Ordinal)))
                throw new InvalidOperationException($"YooAsset package '{packageName}' is not present in collector setting '{path}'.");
            return setting;
        }
    }

    /// <summary>将发布上下文映射到 YooAsset BuiltinBuildParameters 并检查完整产物。</summary>
    public sealed class YooAssetHotUpdateBuildAdapter : IHotUpdateBuildAdapter
    {
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
            long totalBytes = files.Sum(path => new FileInfo(path).Length);
            context.BuildOutput = result.OutputDirectory;
            context.YooAssetBuildOutput = new YooAssetBuildOutput
            {
                PackageVersion = context.PackageVersion,
                OutputDirectory = result.OutputDirectory,
                BundleCount = bundleCount,
                TotalBytes = totalBytes,
                ManifestFiles = manifestFiles
            };
            return Task.FromResult(HotUpdatePublishStepResult.Succeeded());
        }

        private static HotUpdatePublishStepResult Failed(string message)
        {
            return HotUpdatePublishStepResult.Failed(HotUpdatePublishErrorCode.StageFailed, message);
        }
    }
}
