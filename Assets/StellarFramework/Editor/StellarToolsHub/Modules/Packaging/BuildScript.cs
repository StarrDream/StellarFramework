using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace StellarFramework.Build
{
    /// <summary>
    /// CLI / CI 构建入口（batchmode）。
    ///
    /// 用法（命令行）:
    ///   Unity -batchmode -quit -projectPath . \
    ///     -executeMethod StellarFramework.Build.BuildScript.PerformBuild \
    ///     -buildTarget Android \
    ///     -output BuildArtifacts/Android \
    ///     -version 1.2.3 \
    ///     -clean
    ///
    /// 参数:
    ///   -buildTarget  目标平台（StandaloneWindows64/Android/iOS/WebGL，默认取 Editor 当前平台）
    ///   -output       输出目录（默认 BuildArtifacts/&lt;Target&gt;）
    ///   -version      PlayerSettings.bundleVersion
    ///   -clean        构建前清理输出目录
    ///
    /// 也支持环境变量 UNITY_BUILD_TARGET / UNITY_OUTPUT_DIR（供 CI 使用），
    /// 优先级：命令行参数 > 环境变量 > 默认值。
    ///
    /// 退出码：成功 0，失败 1（CI 可据此判断构建结果）。
    /// </summary>
    public static class BuildScript
    {
        public static void PerformBuild()
        {
            string targetArg = GetArg("-buildTarget");
            string outputArg = GetArg("-output");
            string versionArg = GetArg("-version");
            bool clean = HasArg("-clean");

            BuildTarget target = ParseBuildTarget(targetArg);
            string outputDir = string.IsNullOrEmpty(outputArg)
                ? GetEnv("UNITY_OUTPUT_DIR", $"BuildArtifacts/{target}")
                : outputArg;

            if (!string.IsNullOrEmpty(versionArg))
            {
                PlayerSettings.bundleVersion = versionArg;
                Debug.Log($"[BuildScript] 版本号已设置: {versionArg}");
            }

            if (clean && Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, true);
                Debug.Log($"[BuildScript] 已清理输出目录: {outputDir}");
            }

            Directory.CreateDirectory(outputDir);

            string[] enabledScenes = GetEnabledScenes();
            if (enabledScenes.Length == 0)
            {
                Debug.LogError("[BuildScript] Build Settings 中没有启用的场景。请在 File > Build Settings 中添加场景。");
                EditorApplication.Exit(1);
                return;
            }

            string location = Path.Combine(outputDir, GetOutputFileName(target));

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = enabledScenes,
                locationPathName = location,
                target = target,
                options = BuildOptions.None
            };

            Debug.Log($"[BuildScript] 开始构建: Target={target}, Scenes={enabledScenes.Length}, Output={location}");

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            Debug.Log($"[BuildScript] 构建结果: {summary.result}, 耗时={summary.totalTime.TotalSeconds:F1}s, 大小={summary.totalSize / 1048576f:F1}MB, 输出={summary.outputPath}");

            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[BuildScript] 构建失败: {summary.result}");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log("[BuildScript] 构建成功。");
            EditorApplication.Exit(0);
        }

        #region 辅助

        private static string[] GetEnabledScenes()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            if (scenes == null || scenes.Length == 0)
            {
                return Array.Empty<string>();
            }

            var result = new System.Collections.Generic.List<string>(scenes.Length);
            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i] != null && scenes[i].enabled && !string.IsNullOrEmpty(scenes[i].path))
                {
                    result.Add(scenes[i].path);
                }
            }

            return result.ToArray();
        }

        private static BuildTarget ParseBuildTarget(string raw)
        {
            string env = GetEnv("UNITY_BUILD_TARGET", string.Empty);
            string value = !string.IsNullOrEmpty(raw) ? raw : env;
            if (string.IsNullOrEmpty(value))
            {
                return EditorUserBuildSettings.activeBuildTarget;
            }

            if (Enum.TryParse(value, true, out BuildTarget target) &&
                Enum.IsDefined(typeof(BuildTarget), target))
            {
                return target;
            }

            Debug.LogWarning($"[BuildScript] 无法识别 -buildTarget={value}，使用当前平台 {EditorUserBuildSettings.activeBuildTarget}");
            return EditorUserBuildSettings.activeBuildTarget;
        }

        private static string GetOutputFileName(BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return "StellarFramework.exe";
                case BuildTarget.Android:
                    return "StellarFramework.apk";
                case BuildTarget.iOS:
                case BuildTarget.WebGL:
                case BuildTarget.StandaloneOSX:
                case BuildTarget.StandaloneLinux64:
                    return "StellarFramework";
                default:
                    return "StellarFramework";
            }
        }

        private static string GetArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return string.Empty;
        }

        private static bool HasArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetEnv(string name, string fallback)
        {
            string value = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        #endregion
    }
}
