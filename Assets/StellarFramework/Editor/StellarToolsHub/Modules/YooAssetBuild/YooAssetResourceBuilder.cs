using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using YooAsset;
using YooAsset.Editor;

namespace StellarFramework.Editor.ResourceBuild
{
    [Serializable]
    public sealed class YooAssetResourceBuildRequest
    {
        public BuildTarget Target = BuildTarget.StandaloneWindows64;
        public string PackageName = string.Empty;
        public string Version = "1.0.0";
        public string OutputRoot = "BuildArtifacts/Resources/YooAsset";
        public ECompressOption Compression = ECompressOption.LZ4;
    }

    [Serializable]
    public sealed class YooAssetResourceBuildResult
    {
        public bool Success;
        public string Error = string.Empty;
        public string OutputDirectory = string.Empty;
        public int BundleCount;
        public long TotalBytes;
        public double ElapsedSeconds;
    }

    /// <summary>ToolsHub 与热更发布器共用的资源构建入口，只依赖 YooAsset 编辑器 SDK。</summary>
    public static class YooAssetResourceBuilder
    {
        public static bool IsBuilding { get; private set; }

        public static AssetBundleCollectorSetting FindCollectorSetting()
        {
            string[] guids = AssetDatabase.FindAssets("t:AssetBundleCollectorSetting");
            if (guids.Length == 0) return null;
            if (guids.Length != 1)
                throw new InvalidOperationException($"找到 {guids.Length} 份 YooAsset 采集配置。请保留唯一的有效配置后再构建。");
            return AssetDatabase.LoadAssetAtPath<AssetBundleCollectorSetting>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        public static string GetOutputDirectory(YooAssetResourceBuildRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!IsSafeSegment(request.PackageName) || !IsSafeSegment(request.Version))
                throw new ArgumentException("资源包名和版本必须是有效的文件夹名称，不能包含路径分隔符、保留名称或相对路径。");
            if (string.Equals(request.Version, "OutputCache", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("资源版本不能使用 YooAsset 的缓存目录名称。请使用 1.0.0 等版本名称。");
            if (string.IsNullOrWhiteSpace(request.OutputRoot)) throw new ArgumentException("请选择输出目录。");
            return Path.Combine(Path.GetFullPath(request.OutputRoot), request.Target.ToString(), request.PackageName, request.Version);
        }

        public static string Validate(YooAssetResourceBuildRequest request)
        {
            try
            {
                string output = GetOutputDirectory(request);
                if (request.Target == BuildTarget.NoTarget ||
                    !UnityEditor.BuildPipeline.IsBuildTargetSupported(UnityEditor.BuildPipeline.GetBuildTargetGroup(request.Target), request.Target))
                    return "当前 Unity 未安装此平台的构建支持。请在 Unity Hub 中安装对应模块。";
                if (!Enum.IsDefined(typeof(ECompressOption), request.Compression)) return "请选择有效的压缩方式。";
                string root = Path.GetFullPath(request.OutputRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string project = Directory.GetParent(Application.dataPath).FullName;
                foreach (string name in new[] { "Assets", "Packages", "ProjectSettings" })
                {
                    string reserved = Path.Combine(project, name);
                    if (string.Equals(root, reserved, StringComparison.OrdinalIgnoreCase) ||
                        root.StartsWith(reserved + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        return "输出目录不能放在 Assets、Packages 或 ProjectSettings 内。建议使用项目的 BuildArtifacts 目录。";
                }
                if (File.Exists(root)) return "输出目录指向了一个文件，请选择文件夹。";
                if (Directory.Exists(output) || File.Exists(output)) return "此版本的产物已存在，请填写新版本或选择其他输出目录。";
                AssetBundleCollectorSetting setting = FindCollectorSetting();
                if (setting == null) return "还没有 YooAsset 采集配置。请点击“配置采集规则”创建并保存资源包。";
                AssetBundleCollectorPackage package = setting.Packages?.FirstOrDefault(item => item != null && item.PackageName == request.PackageName);
                if (package == null) return "所选资源包已不存在，请刷新资源包列表。";
                if (package.Groups == null || !package.Groups.Any(group => group?.Collectors != null && group.Collectors.Count > 0))
                    return "资源包没有采集项。请在采集规则中添加需要构建的资源目录。";
                return string.Empty;
            }
            catch (Exception exception) { return exception.Message; }
        }

        public static YooAssetResourceBuildResult Build(YooAssetResourceBuildRequest request)
        {
            if (IsBuilding) return new YooAssetResourceBuildResult { Error = "已有资源构建正在执行，请等待完成。" };
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                UnityEditor.BuildPipeline.isBuildingPlayer)
                return new YooAssetResourceBuildResult { Error = "请等待 Unity 导入、编译或 Player 构建完成，并退出运行模式。" };
            string error = Validate(request);
            if (!string.IsNullOrEmpty(error)) return new YooAssetResourceBuildResult { Error = error };
            double started = EditorApplication.timeSinceStartup;
            IsBuilding = true;
            YooAssetResourceBuildResult result;
            try
            {
                var parameters = new BuiltinBuildParameters
                {
                    BuildTarget = request.Target,
                    BuildPipeline = EBuildPipeline.BuiltinBuildPipeline.ToString(),
                    BuildBundleType = (int)EBuildBundleType.AssetBundle,
                    PackageName = request.PackageName,
                    PackageVersion = request.Version,
                    BuildOutputRoot = Path.GetFullPath(request.OutputRoot),
                    BuildinFileRoot = AssetBundleBuilderHelper.GetStreamingAssetsRoot(),
                    CompressOption = request.Compression,
                    ClearBuildCacheFiles = false,
                    VerifyBuildingResult = true,
                    FileNameStyle = EFileNameStyle.HashName,
                    BuildinFileCopyOption = EBuildinFileCopyOption.None
                };
                BuildResult output = new BuiltinBuildPipeline().Run(parameters, true);
                result = output.Success ? InspectOutput(request, output.OutputPackageDirectory) :
                    new YooAssetResourceBuildResult { Error = $"YooAsset 构建失败（{output.FailedTask}）：{output.ErrorInfo}\n{output.ErrorStack}" };
            }
            catch (Exception exception) { result = new YooAssetResourceBuildResult { Error = exception.ToString() }; }
            finally
            {
                IsBuilding = false;
            }
            result.ElapsedSeconds = EditorApplication.timeSinceStartup - started;
            return result;
        }

        public static YooAssetResourceBuildResult InspectOutput(YooAssetResourceBuildRequest request, string directory)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return new YooAssetResourceBuildResult { Error = "YooAsset 返回成功，但输出目录不存在。" };
            string[] manifests =
            {
                YooAssetSettingsData.GetManifestBinaryFileName(request.PackageName, request.Version),
                YooAssetSettingsData.GetManifestJsonFileName(request.PackageName, request.Version),
                YooAssetSettingsData.GetPackageHashFileName(request.PackageName, request.Version),
                YooAssetSettingsData.GetPackageVersionFileName(request.PackageName)
            };
            foreach (string manifest in manifests)
                if (!File.Exists(Path.Combine(directory, manifest)))
                    return new YooAssetResourceBuildResult { Error = "构建产物缺少清单文件：" + manifest };
            string[] files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
            int bundles = files.Count(file => file.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase));
            if (bundles == 0) return new YooAssetResourceBuildResult { Error = "未生成资源 Bundle。请检查采集规则和资源内容。" };
            return new YooAssetResourceBuildResult
            {
                Success = true, OutputDirectory = directory, BundleCount = bundles,
                TotalBytes = files.Where(file => Path.GetFileName(file) != "OutputCache" && Path.GetFileName(file) != "OutputCache.manifest")
                    .Sum(file => new FileInfo(file).Length)
            };
        }

        private static bool IsSafeSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value != value.Trim() || value == "." || value == ".." || value.Length > 128 ||
                value.EndsWith(".", StringComparison.Ordinal) || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                value.Contains("/") || value.Contains("\\") || value.Contains(":")) return false;
            string stem = value.Split('.')[0].ToUpperInvariant();
            return stem != "CON" && stem != "PRN" && stem != "AUX" && stem != "NUL" &&
                !(stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) &&
                    stem[3] >= '1' && stem[3] <= '9');
        }
    }
}
