#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Project-local build entry point for the Android Release verification pipeline.
/// It intentionally lives outside the distributable StellarFramework package.
/// </summary>
public static class StellarFrameworkAndroidReleaseVerificationBuild
{
    private const string ScenePath =
        "Assets/StellarFramework/Samples/ArchitectureDemo/Scene/FrameworkArchitecture_Playable.unity";

    private const string DefaultRelativeOutputPath =
        "Builds/AndroidVerification/StellarFramework-ArchitectureDemo-x86_64-release.apk";

    private const string DefaultRelativeStatePath =
        "Library/StellarFramework/AndroidVerification/android-build-state.json";

    private const string HotUpdateRelativeOutputPath =
        "Builds/AndroidVerification/StellarFramework-HotUpdate-x86_64-release.apk";

    private const string HotUpdateRelativeStatePath =
        "Library/StellarFramework/AndroidVerification/android-hotupdate-build-state.json";

    private const string OutputEnvironmentVariable = "STELLAR_ANDROID_VERIFICATION_APK";
    private const string MenuPath =
        "Tools/StellarFramework/Verification/Build Android Release Verification APK";

    private const string HotUpdateMenuPath =
        "Tools/StellarFramework/Verification/Build Android Release HotUpdate Verification APK";

    [Serializable]
    private sealed class BuildState
    {
        public string status;
        public string startedAt;
        public string completedAt;
        public string outputPath;
        public string profile;
        public string buildTarget;
        public string scriptingBackend;
        public string architectures;
        public bool developmentBuild;
        public bool internetPermission;
        public string insecureHttpOption;
        public string buildResult;
        public int totalErrors;
        public int totalWarnings;
        public string totalSizeBytes;
        public string error;
    }

    [MenuItem(MenuPath)]
    private static void BuildReleaseFromMenu()
    {
        if (BuildPipeline.isBuildingPlayer)
        {
            throw new InvalidOperationException("An Android verification build is already running.");
        }

        BuildRelease();
    }

    /// <summary>
    /// Builds a non-development IL2CPP x86_64 APK for the API 35 emulator gate.
    /// All temporarily changed Android build settings are restored before returning.
    /// </summary>
    public static void BuildRelease()
    {
        BuildReleaseCore(false);
    }

    /// <summary>
    /// Builds the Android HotUpdate gate APK. HTTP is enabled only for this verification build;
    /// the prior Player setting is restored before the method returns.
    /// </summary>
    public static void BuildHotUpdateRelease()
    {
        BuildReleaseCore(true);
    }

    [MenuItem(HotUpdateMenuPath)]
    private static void BuildHotUpdateReleaseFromMenu()
    {
        if (BuildPipeline.isBuildingPlayer)
        {
            throw new InvalidOperationException("An Android verification build is already running.");
        }

        BuildHotUpdateRelease();
    }

    private static void BuildReleaseCore(bool hotUpdateProfile)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
        {
            throw new FileNotFoundException("ArchitectureDemo validation scene not found.", ScenePath);
        }

        string outputPath = ResolveOutputPath(hotUpdateProfile);
        string outputDirectory = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new InvalidOperationException(
                $"Unable to resolve APK output directory from '{outputPath}'.");
        }

        Directory.CreateDirectory(outputDirectory);
        string statePath = ResolveStatePath(hotUpdateProfile);
        var buildState = new BuildState
        {
            status = "RUNNING",
            startedAt = DateTimeOffset.Now.ToString("O"),
            outputPath = outputPath,
            profile = hotUpdateProfile ? "HotUpdate" : "ArchitectureDemoSmoke",
            buildTarget = BuildTarget.Android.ToString(),
            buildResult = "Unknown",
            totalSizeBytes = "0",
            error = string.Empty
        };
        WriteBuildState(statePath, buildState);

        NamedBuildTarget androidTarget = NamedBuildTarget.Android;
        ScriptingImplementation previousBackend = PlayerSettings.GetScriptingBackend(androidTarget);
        AndroidArchitecture previousArchitectures = PlayerSettings.Android.targetArchitectures;
        bool previousDevelopment = EditorUserBuildSettings.development;
        bool previousBuildAppBundle = EditorUserBuildSettings.buildAppBundle;
        bool previousInternetPermission = PlayerSettings.Android.forceInternetPermission;
        FieldInfo httpOptionField = GetInsecureHttpOptionField();
        object previousHttpOption = httpOptionField?.GetValue(null);

        try
        {
            PlayerSettings.SetScriptingBackend(androidTarget, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.X86_64;
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.buildAppBundle = false;
            if (hotUpdateProfile)
            {
                PlayerSettings.Android.forceInternetPermission = true;
                SetInsecureHttpOption(httpOptionField, "AlwaysAllowed");
            }

            buildState.scriptingBackend = PlayerSettings.GetScriptingBackend(androidTarget).ToString();
            buildState.architectures = PlayerSettings.Android.targetArchitectures.ToString();
            buildState.developmentBuild = EditorUserBuildSettings.development;
            buildState.internetPermission = PlayerSettings.Android.forceInternetPermission;
            buildState.insecureHttpOption = httpOptionField?.GetValue(null)?.ToString() ?? "Unavailable";

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            buildState.buildResult = summary.result.ToString();
            buildState.totalErrors = summary.totalErrors;
            buildState.totalWarnings = summary.totalWarnings;
            buildState.totalSizeBytes = summary.totalSize.ToString();
            Debug.Log(
                $"[StellarAndroidReleaseVerificationBuild] Result={summary.result} " +
                $"Errors={summary.totalErrors} Warnings={summary.totalWarnings} " +
                $"Size={summary.totalSize} Output={outputPath}");

            if (summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Android Release verification build failed: {summary.result}, " +
                    $"errors={summary.totalErrors}.");
            }

            buildState.status = "PASS";
        }
        catch (Exception exception)
        {
            buildState.status = "FAIL";
            buildState.error = exception.ToString();
            throw;
        }
        finally
        {
            PlayerSettings.Android.targetArchitectures = previousArchitectures;
            PlayerSettings.SetScriptingBackend(androidTarget, previousBackend);
            EditorUserBuildSettings.development = previousDevelopment;
            EditorUserBuildSettings.buildAppBundle = previousBuildAppBundle;
            PlayerSettings.Android.forceInternetPermission = previousInternetPermission;
            if (hotUpdateProfile && httpOptionField != null)
            {
                httpOptionField.SetValue(null, previousHttpOption);
            }

            Debug.Log(
                $"[StellarAndroidReleaseVerificationBuild] Restored backend={previousBackend}, " +
                $"architectures={previousArchitectures}, development={previousDevelopment}, " +
                $"buildAppBundle={previousBuildAppBundle}, internetPermission={previousInternetPermission}, " +
                $"insecureHttpOption={previousHttpOption ?? "Unavailable"}.");

            buildState.completedAt = DateTimeOffset.Now.ToString("O");
            WriteBuildState(statePath, buildState);
        }
    }

    private static void WriteBuildState(string statePath, BuildState state)
    {
        string directory = Path.GetDirectoryName(statePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string tempPath = statePath + ".tmp";
        File.WriteAllText(tempPath, JsonUtility.ToJson(state, true));

        if (File.Exists(statePath))
        {
            File.Delete(statePath);
        }

        File.Move(tempPath, statePath);
    }

    private static FieldInfo GetInsecureHttpOptionField()
    {
        return typeof(PlayerSettings).GetField(
            "insecureHttpOption",
            BindingFlags.Public | BindingFlags.Static);
    }

    private static void SetInsecureHttpOption(FieldInfo field, string optionName)
    {
        if (field == null)
        {
            Debug.LogWarning(
                "[StellarAndroidReleaseVerificationBuild] PlayerSettings.insecureHttpOption is unavailable; " +
                "the Android verification APK will use the current Unity HTTP policy.");
            return;
        }

        if (!field.FieldType.IsEnum)
        {
            throw new InvalidOperationException(
                "PlayerSettings.insecureHttpOption is present but is not an enum field.");
        }

        object enabledOption;
        try
        {
            enabledOption = Enum.Parse(field.FieldType, optionName, ignoreCase: false);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                $"PlayerSettings.insecureHttpOption does not define '{optionName}'.", exception);
        }

        field.SetValue(null, enabledOption);
    }

    private static string ResolveOutputPath(bool hotUpdateProfile)
    {
        string requestedPath = Environment.GetEnvironmentVariable(
            hotUpdateProfile ? "STELLAR_ANDROID_HOTUPDATE_APK" : OutputEnvironmentVariable);
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Unable to resolve Unity project root.");

        if (string.IsNullOrWhiteSpace(requestedPath))
        {
            requestedPath = hotUpdateProfile ? HotUpdateRelativeOutputPath : DefaultRelativeOutputPath;
        }

        string combined = Path.IsPathRooted(requestedPath)
            ? requestedPath
            : Path.Combine(projectRoot, requestedPath);

        return Path.GetFullPath(combined);
    }

    private static string ResolveStatePath(bool hotUpdateProfile)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Unable to resolve Unity project root.");

        return Path.GetFullPath(Path.Combine(
            projectRoot,
            hotUpdateProfile ? HotUpdateRelativeStatePath : DefaultRelativeStatePath));
    }
}
#endif
