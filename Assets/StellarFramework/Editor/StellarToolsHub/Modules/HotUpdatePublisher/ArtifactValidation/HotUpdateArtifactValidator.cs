using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using StellarFramework.HybridCLR;
using UnityEditor;
using UnityEngine;

namespace StellarFramework.Editor.HotUpdatePublisher
{
    /// <summary>一个可定位的 HotUpdate 发布产物校验结果。</summary>
    public sealed class HotUpdateArtifactValidationReport
    {
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public bool IsValid => Errors.Count == 0;

        internal void AddError(string message) { if (!string.IsNullOrWhiteSpace(message)) Errors.Add(message); }
        internal void AddWarning(string message) { if (!string.IsNullOrWhiteSpace(message)) Warnings.Add(message); }
    }

    /// <summary>
    /// 发布前静态检查完整热更构建闭环。入口验证只读取程序集元数据，不会调用 HotUpdate 入口方法。
    /// </summary>
    public sealed class HotUpdateArtifactValidator
    {
        private static readonly Regex SafeVersion = new Regex(
            @"\A[A-Za-z0-9][A-Za-z0-9.+-]{0,63}\z", RegexOptions.Compiled);

        private readonly HotUpdateBaseReleaseRepository _baseReleaseRepository;
        private readonly string _assetsRoot;

        /// <summary>创建校验器；测试可提供隔离 Assets 根目录。</summary>
        public HotUpdateArtifactValidator(
            HotUpdateBaseReleaseRepository baseReleaseRepository,
            string assetsRoot = null)
        {
            _baseReleaseRepository = baseReleaseRepository ?? throw new ArgumentNullException(nameof(baseReleaseRepository));
            _assetsRoot = Path.GetFullPath(string.IsNullOrWhiteSpace(assetsRoot) ? Application.dataPath : assetsRoot);
        }

        /// <summary>按阶段产物逐项校验；所有产品不变量问题都以报告错误返回。</summary>
        public HotUpdateArtifactValidationReport Validate(HotUpdatePublishContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var report = new HotUpdateArtifactValidationReport();
            ValidatePackageVersion(context.PackageVersion, report);
            ValidateManifestAndAssembly(context, report);
            ValidateAotMetadata(context, report);
            ValidateYooAssetBuild(context, report);
            return report;
        }

        private void ValidateManifestAndAssembly(
            HotUpdatePublishContext context,
            HotUpdateArtifactValidationReport report)
        {
            string manifestPath = context.HybridCLRBuildOutput?.ManifestAssetPath;
            if (!TryResolveAssetPath(manifestPath, out string manifestAbsolutePath))
            {
                report.AddError("HotUpdate Manifest asset path is missing or is not a safe Assets path.");
                return;
            }

            if (!File.Exists(manifestAbsolutePath))
            {
                report.AddError($"HotUpdate Manifest does not exist: '{manifestPath}'.");
                return;
            }

            string json = File.ReadAllText(manifestAbsolutePath, Encoding.UTF8);
            HotUpdateManifest manifest;
            try
            {
                manifest = JsonUtility.FromJson<HotUpdateManifest>(json.TrimStart('\uFEFF'));
            }
            catch (ArgumentException exception)
            {
                report.AddError($"HotUpdate Manifest JSON is invalid: {exception.Message}");
                return;
            }

            if (manifest == null)
            {
                report.AddError("HotUpdate Manifest JSON did not produce a manifest object.");
                return;
            }

            if (manifest.version != 1)
                report.AddError($"Unsupported HotUpdate Manifest version '{manifest.version}'.");
            if (!string.Equals(manifest.buildTarget, context.Platform.ToString(), StringComparison.Ordinal))
                report.AddError($"Manifest buildTarget '{manifest.buildTarget}' does not match '{context.Platform}'.");

            HotUpdateManifestValidationReport manifestValidation = manifest.Validate(strictAssemblyIntegrity: true);
            foreach (string error in manifestValidation.Errors) report.AddError("Manifest: " + error);
            foreach (string warning in manifestValidation.Warnings) report.AddWarning("Manifest: " + warning);

            if (!TryResolveAssetPath(manifest.hotUpdateAssemblyKey, out string assemblyPath))
            {
                report.AddError("Manifest HotUpdate DLL key is not a safe Assets path.");
                return;
            }

            if (!File.Exists(assemblyPath))
            {
                report.AddError($"HotUpdate DLL asset does not exist: '{manifest.hotUpdateAssemblyKey}'.");
                return;
            }

            byte[] assemblyBytes = File.ReadAllBytes(assemblyPath);
            string actualHash = ComputeSha256(assemblyBytes);
            string expectedHash = HotUpdateManifest.NormalizeSha256(manifest.hotUpdateAssemblySha256);
            if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
            {
                report.AddError($"HotUpdate DLL SHA256 mismatch. Expected={expectedHash}, Actual={actualHash}.");
                return;
            }

            ValidateEntryPoint(assemblyBytes, manifest.hotUpdateEntryClass, manifest.hotUpdateEntryMethod, report);
        }

        private static void ValidateEntryPoint(
            byte[] assemblyBytes,
            string entryClass,
            string entryMethod,
            HotUpdateArtifactValidationReport report)
        {
            Assembly assembly;
            try
            {
                // Assembly.Load(bytes) inspects the same PE bytes the runtime will load. No static initializer or entry method is invoked.
                assembly = Assembly.Load(assemblyBytes);
            }
            catch (BadImageFormatException exception)
            {
                report.AddError($"HotUpdate DLL is not a readable managed assembly: {exception.Message}");
                return;
            }
            catch (FileLoadException exception)
            {
                report.AddError($"HotUpdate DLL could not be loaded for metadata inspection: {exception.Message}");
                return;
            }

            Type type;
            try
            {
                type = assembly.GetType(entryClass, throwOnError: false, ignoreCase: false);
            }
            catch (TypeLoadException exception)
            {
                report.AddError($"HotUpdate entry class '{entryClass}' could not be inspected: {exception.Message}");
                return;
            }

            if (type == null)
            {
                report.AddError($"HotUpdate entry class '{entryClass}' was not found in '{assembly.GetName().Name}'.");
                return;
            }

            MethodInfo method;
            try
            {
                method = type.GetMethod(entryMethod, BindingFlags.Public | BindingFlags.Static);
            }
            catch (AmbiguousMatchException exception)
            {
                report.AddError($"HotUpdate entry method '{entryClass}.{entryMethod}' is ambiguous: {exception.Message}");
                return;
            }
            if (method == null)
                report.AddError($"Public static HotUpdate entry method '{entryClass}.{entryMethod}' was not found.");
        }

        private void ValidateAotMetadata(
            HotUpdatePublishContext context,
            HotUpdateArtifactValidationReport report)
        {
            HotUpdateBaseRelease release = context.SelectedBaseRelease;
            HotUpdateBaseReleaseRequirements requirements = context.SelectedBaseReleaseRequirements;
            if (release == null || requirements == null)
            {
                report.AddError("Hot Patch artifact validation requires an explicitly selected compatible BaseRelease.");
                return;
            }

            IReadOnlyList<string> authoritativePaths;
            try
            {
                authoritativePaths = _baseReleaseRepository.GetAotMetadataPaths(release, requirements);
            }
            catch (Exception exception) when (exception is IOException || exception is ArgumentException)
            {
                report.AddError($"Selected BaseRelease AOT metadata is invalid: {exception.Message}");
                return;
            }

            var expectedHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < authoritativePaths.Count; index++)
                expectedHashes.Add(Path.GetFileName(authoritativePaths[index]), ComputeSha256(File.ReadAllBytes(authoritativePaths[index])));

            HotUpdateManifest manifest = ReadManifestForAot(context, report);
            if (manifest == null) return;
            if (manifest.aotMetadataKeys == null || manifest.aotMetadataKeys.Count != expectedHashes.Count)
            {
                report.AddError($"Manifest AOT metadata count does not match selected BaseRelease '{release.BaseAppVersion}'.");
                return;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string assetKey in manifest.aotMetadataKeys)
            {
                if (!TryResolveAssetPath(assetKey, out string assetPath))
                {
                    report.AddError($"AOT metadata key '{assetKey}' is not a safe Assets path.");
                    continue;
                }

                string fileName = Path.GetFileName(assetPath);
                string metadataName = fileName.EndsWith(".bytes", StringComparison.OrdinalIgnoreCase)
                    ? fileName.Substring(0, fileName.Length - ".bytes".Length)
                    : fileName;
                if (!seen.Add(metadataName))
                {
                    report.AddError($"Manifest contains duplicate AOT metadata key '{assetKey}'.");
                    continue;
                }
                if (!File.Exists(assetPath))
                {
                    report.AddError($"AOT metadata asset does not exist: '{assetKey}'.");
                    continue;
                }
                if (!expectedHashes.TryGetValue(metadataName, out string expectedHash))
                {
                    report.AddError($"AOT metadata '{metadataName}' is not part of selected BaseRelease '{release.BaseAppVersion}'.");
                    continue;
                }

                string actualHash = ComputeSha256(File.ReadAllBytes(assetPath));
                if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
                    report.AddError($"AOT metadata SHA256 mismatch for '{assetKey}' against selected BaseRelease.");
            }
        }

        private HotUpdateManifest ReadManifestForAot(
            HotUpdatePublishContext context,
            HotUpdateArtifactValidationReport report)
        {
            if (!TryResolveAssetPath(context.HybridCLRBuildOutput?.ManifestAssetPath, out string manifestPath) ||
                !File.Exists(manifestPath))
                return null;
            try
            {
                return JsonUtility.FromJson<HotUpdateManifest>(File.ReadAllText(manifestPath, Encoding.UTF8).TrimStart('\uFEFF'));
            }
            catch (ArgumentException exception)
            {
                report.AddError($"Manifest AOT metadata could not be parsed: {exception.Message}");
                return null;
            }
        }

        private static void ValidatePackageVersion(string version, HotUpdateArtifactValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(version) || !SafeVersion.IsMatch(version))
                report.AddError("PackageVersion must be a path-safe identifier of 1–64 letters, digits, '.', '+' or '-'.");
        }

        private static void ValidateYooAssetBuild(
            HotUpdatePublishContext context,
            HotUpdateArtifactValidationReport report)
        {
            YooAssetBuildOutput output = context.YooAssetBuildOutput;
            if (output == null)
            {
                report.AddError("YooAsset build did not produce a completed output record.");
                return;
            }
            if (!string.Equals(output.PackageVersion, context.PackageVersion, StringComparison.Ordinal))
                report.AddError("YooAsset output PackageVersion does not match publish context.");
            if (string.IsNullOrWhiteSpace(output.OutputDirectory) || !Directory.Exists(output.OutputDirectory))
            {
                report.AddError("YooAsset package output directory is missing.");
                return;
            }
            if (output.BundleCount <= 0 || output.TotalBytes <= 0)
                report.AddError("YooAsset build output must contain at least one bundle and a positive byte count.");
            if (output.ManifestFiles == null || output.ManifestFiles.Length < 4)
            {
                report.AddError("YooAsset output record must include binary/JSON manifest, package hash and package version files.");
                return;
            }
            foreach (string fileName in output.ManifestFiles)
            {
                if (string.IsNullOrWhiteSpace(fileName) ||
                    !string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal))
                {
                    report.AddError($"YooAsset manifest entry '{fileName}' must be a file name without a path.");
                    continue;
                }
                if (!File.Exists(Path.Combine(output.OutputDirectory, fileName)))
                    report.AddError($"YooAsset build output is missing required file '{fileName}'.");
            }
        }

        private bool TryResolveAssetPath(string assetPath, out string absolutePath)
        {
            absolutePath = string.Empty;
            if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.Ordinal))
                return false;
            string relativePath = assetPath.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar);
            string candidate = Path.GetFullPath(Path.Combine(_assetsRoot, relativePath));
            string rootPrefix = _assetsRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                return false;
            absolutePath = candidate;
            return true;
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(bytes);
                var builder = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++) builder.Append(hash[index].ToString("x2"));
                return builder.ToString();
            }
        }
    }

    /// <summary>将 P4 产物检查接入固定 Publisher 流水线。</summary>
    public sealed class HotUpdateArtifactValidationStageHandler : IHotUpdatePublishStageHandler
    {
        private readonly HotUpdateArtifactValidator _validator;
        public HotUpdatePublishStage Stage => HotUpdatePublishStage.ValidateArtifacts;

        public HotUpdateArtifactValidationStageHandler(HotUpdateArtifactValidator validator)
        {
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        }

        public System.Threading.Tasks.Task<HotUpdatePublishStepResult> ExecuteAsync(
            HotUpdatePublishContext context,
            System.Threading.CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            HotUpdateArtifactValidationReport report = _validator.Validate(context);
            if (!report.IsValid)
                return System.Threading.Tasks.Task.FromResult(HotUpdatePublishStepResult.Failed(
                    HotUpdatePublishErrorCode.ArtifactValidationFailed,
                    string.Join(Environment.NewLine, report.Errors), report.Warnings.ToArray()));
            return System.Threading.Tasks.Task.FromResult(HotUpdatePublishStepResult.Succeeded(report.Warnings.ToArray()));
        }
    }
}
