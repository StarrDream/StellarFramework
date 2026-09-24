using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StellarFramework.Editor.Modules;
using UnityEditor;
using UnityEngine;
using HybridCLR.Editor.Commands;

namespace StellarFramework.Editor.HotUpdatePublisher
{
    /// <summary>可替换的 HybridCLR DLL 编译入口，供自动化验证隔离 Unity Build API。</summary>
    public interface IHybridCLRAssemblyCompiler
    {
        void Compile(BuildTarget target, bool developmentBuild);
    }

    /// <summary>HybridCLR Editor SDK 的生产编译实现。</summary>
    public sealed class HybridCLRAssemblyCompiler : IHybridCLRAssemblyCompiler
    {
        public void Compile(BuildTarget target, bool developmentBuild)
        {
            CompileDllCommand.CompileDll(target, developmentBuild);
        }
    }

    /// <summary>
    /// Hot Patch 的 HybridCLR Adapter。HotUpdate DLL 来自本次 CompileDll，AOT metadata 只来自所选 BaseRelease。
    /// </summary>
    public sealed class HybridCLRHotUpdateBuildAdapter : IHotUpdateBuildAdapter
    {
        private readonly HotUpdateBaseReleaseRepository _baseReleaseRepository;
        private readonly IHybridCLRAssemblyCompiler _compiler;

        public HybridCLRHotUpdateBuildAdapter(
            HotUpdateBaseReleaseRepository baseReleaseRepository,
            IHybridCLRAssemblyCompiler compiler = null)
        {
            _baseReleaseRepository = baseReleaseRepository ?? throw new ArgumentNullException(nameof(baseReleaseRepository));
            _compiler = compiler ?? new HybridCLRAssemblyCompiler();
        }

        /// <summary>
        /// 生成并记录 Base App 的正式 AOT metadata。调用者必须先在 Unity Player Settings 中选择目标和 IL2CPP；
        /// 此处不会暗中切换全局 Editor 构建设置。
        /// </summary>
        public HotUpdateBaseRelease CreateBaseRelease(HotUpdateBaseReleaseCreateRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.Platform != EditorUserBuildSettings.activeBuildTarget)
                throw new InvalidOperationException("Active BuildTarget must match the BaseRelease target before HybridCLR generation.");
            BuildTargetGroup targetGroup = UnityEditor.BuildPipeline.GetBuildTargetGroup(request.Platform);
            if (request.ScriptingBackend != PlayerSettings.GetScriptingBackend(targetGroup))
                throw new InvalidOperationException("Player scripting backend must match the BaseRelease record before HybridCLR generation.");
            if (request.ScriptingBackend != ScriptingImplementation.IL2CPP)
                throw new InvalidOperationException("HybridCLR BaseRelease requires the target Player scripting backend to be IL2CPP.");

            PrebuildCommand.GenerateAll();
            string metadataDirectory = HybridCLRHotUpdateAssetExporter.GetGeneratedAotSourceDirectory(request.Platform);
            if (!Directory.Exists(metadataDirectory))
                throw new DirectoryNotFoundException($"HybridCLR generated AOT metadata directory is missing: '{metadataDirectory}'.");
            request.AotMetadataSourcePaths = Directory.GetFiles(metadataDirectory, "*.dll", SearchOption.TopDirectoryOnly);
            if (request.AotMetadataSourcePaths.Length == 0)
                throw new InvalidDataException($"HybridCLR generated no AOT metadata for '{request.Platform}' in '{metadataDirectory}'.");
            return _baseReleaseRepository.Create(request);
        }

        public Task<HotUpdatePublishStepResult> CompileHotUpdateAsync(
            HotUpdatePublishContext context, CancellationToken cancellationToken)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            cancellationToken.ThrowIfCancellationRequested();
            if (context.Platform != EditorUserBuildSettings.activeBuildTarget)
                return Task.FromResult(Failed("Active BuildTarget does not match the selected HotUpdate target."));
            if (context.SelectedBaseRelease == null || context.SelectedBaseReleaseRequirements == null)
                return Task.FromResult(Failed("A validated BaseRelease must be selected before compiling a Hot Patch."));
            if (context.SelectedBaseRelease.ScriptingBackend != ScriptingImplementation.IL2CPP)
                return Task.FromResult(Failed("HybridCLR Hot Patch requires an IL2CPP BaseRelease."));

            _compiler.Compile(context.Platform, context.DevelopmentBuild);
            context.CompiledHotUpdateDirectory = HybridCLRHotUpdateAssetExporter
                .GetGeneratedHotUpdateSourceDirectory(context.Platform);
            if (!Directory.Exists(context.CompiledHotUpdateDirectory))
                return Task.FromResult(Failed($"HybridCLR CompileDll completed but its output directory is missing: '{context.CompiledHotUpdateDirectory}'."));

            string[] assemblyNames = context.HotUpdateAssemblyNames ?? Array.Empty<string>();
            string[] sourceDlls = assemblyNames.Length == 0
                ? Directory.GetFiles(context.CompiledHotUpdateDirectory, "*.dll", SearchOption.TopDirectoryOnly)
                : assemblyNames.Select(name => Path.Combine(context.CompiledHotUpdateDirectory,
                    Path.GetFileNameWithoutExtension(name) + ".dll")).ToArray();
            if (sourceDlls.Length == 0 || sourceDlls.Any(path => !File.Exists(path)))
                return Task.FromResult(Failed("One or more configured HotUpdate DLLs are missing from the fresh CompileDll output."));
            return Task.FromResult(HotUpdatePublishStepResult.Succeeded());
        }

        public Task<HotUpdatePublishStepResult> ExportHybridCLRAssetsAsync(
            HotUpdatePublishContext context, CancellationToken cancellationToken)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            cancellationToken.ThrowIfCancellationRequested();
            if (context.SelectedBaseRelease == null || context.SelectedBaseReleaseRequirements == null)
                return Task.FromResult(Failed("A validated BaseRelease is required; temporary HybridCLRData metadata is not a fallback."));
            if (string.IsNullOrWhiteSpace(context.CompiledHotUpdateDirectory) || !Directory.Exists(context.CompiledHotUpdateDirectory))
                return Task.FromResult(Failed("Fresh CompileDll output is missing; export stopped."));
            if (string.IsNullOrWhiteSpace(context.HotUpdateAssetOutputRoot) ||
                string.IsNullOrWhiteSpace(context.HotUpdateManifestAssetPath))
                return Task.FromResult(Failed("HotUpdate asset output root and Manifest asset path are required."));

            IReadOnlyList<string> metadataPaths = _baseReleaseRepository.GetAotMetadataPaths(
                context.SelectedBaseRelease, context.SelectedBaseReleaseRequirements);
            string root = context.HotUpdateAssetOutputRoot.Replace('\\', '/').TrimEnd('/');
            string codeFolder = root + "/Code";
            string metadataFolder = root + "/Metadata";
            string manifestAssetPath = context.HotUpdateManifestAssetPath.Replace('\\', '/');
            if (!IsSafeAssetPath(codeFolder) || !IsSafeAssetPath(metadataFolder) ||
                !IsSafeAssetPath(manifestAssetPath) ||
                !manifestAssetPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(Failed("HybridCLR output paths must be project Assets paths and Manifest must be JSON."));

            string[] hotUpdateNames = context.HotUpdateAssemblyNames ?? Array.Empty<string>();
            HybridCLRHotUpdateExportReport hotReport = HybridCLRHotUpdateAssetExporter.ExportDllDirectory(
                context.CompiledHotUpdateDirectory, codeFolder, hotUpdateNames, overwrite: true);
            if (!hotReport.Success || hotReport.HotUpdateDlls.Count == 0)
                return Task.FromResult(Failed("HotUpdate DLL export failed: " + string.Join("; ", hotReport.Errors)));

            string metadataSourceDirectory = Path.GetDirectoryName(metadataPaths[0]);
            string[] metadataNames = metadataPaths.Select(Path.GetFileNameWithoutExtension).ToArray();
            HybridCLRHotUpdateExportReport metadataReport = HybridCLRHotUpdateAssetExporter.ExportDllDirectory(
                metadataSourceDirectory, metadataFolder, metadataNames, overwrite: true);
            if (!metadataReport.Success || metadataReport.HotUpdateDlls.Count != metadataPaths.Count)
                return Task.FromResult(Failed("BaseRelease AOT metadata export failed: " + string.Join("; ", metadataReport.Errors)));

            var report = new HybridCLRHotUpdateExportReport();
            report.HotUpdateDlls.AddRange(hotReport.HotUpdateDlls);
            report.AotMetadataDlls.AddRange(metadataReport.HotUpdateDlls);
            string manifestJson = HybridCLRHotUpdateAssetExporter.BuildManifestJson(
                report, context.Platform, context.HotUpdateEntryClass, context.HotUpdateEntryMethod);
            if (string.IsNullOrWhiteSpace(manifestJson))
                return Task.FromResult(Failed("HybridCLR exporter did not produce a non-empty Manifest."));

            string absoluteManifestPath = ToAbsoluteAssetPath(manifestAssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteManifestPath));
            File.WriteAllText(absoluteManifestPath, manifestJson);
            AssetDatabase.ImportAsset(manifestAssetPath, ImportAssetOptions.ForceUpdate);
            context.HybridCLRBuildOutput = new HybridCLRBuildOutput
            {
                ManifestAssetPath = manifestAssetPath,
                ManifestJson = manifestJson,
                HotUpdateDllAssetPath = hotReport.HotUpdateDlls[0].DestinationAssetPath,
                HotUpdateDllSha256 = hotReport.HotUpdateDlls[0].Sha256,
                AotMetadataAssetPaths = metadataReport.HotUpdateDlls.Select(item => item.DestinationAssetPath).ToArray()
            };
            return Task.FromResult(HotUpdatePublishStepResult.Succeeded(hotReport.Warnings.Concat(metadataReport.Warnings).ToArray()));
        }

        public Task<HotUpdatePublishStepResult> BuildYooAssetAsync(
            HotUpdatePublishContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(Failed("YooAsset build is provided by the separate YooAsset Editor adapter."));
        }

        private static HotUpdatePublishStepResult Failed(string message)
        {
            return HotUpdatePublishStepResult.Failed(HotUpdatePublishErrorCode.StageFailed, message);
        }

        private static bool IsSafeAssetPath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath) ||
                (!assetPath.StartsWith("Assets/", StringComparison.Ordinal) &&
                 !string.Equals(assetPath, "Assets", StringComparison.Ordinal)))
                return false;
            string absolute = ToAbsoluteAssetPath(assetPath);
            string assetsRoot = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return absolute.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static string ToAbsoluteAssetPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }
    }
}
