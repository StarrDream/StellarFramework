using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class VerificationSurfacePolicyTests
    {
        [Test]
        public void VerificationAreaExistsOutsideFrameworkPayload()
        {
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFrameworkVerification/README.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFrameworkVerification/ValidationArchitecture.md")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFrameworkVerification/Editor/ReleaseVerificationHubModule.cs")), Is.True);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFrameworkVerification/Scenes/FrameworkValidation_Playable.unity")), Is.False);
            Assert.That(File.Exists(ToAbsoluteAssetPath("Assets/StellarFrameworkVerification/Example_FrameworkValidation/FrameworkValidationRunner.cs")), Is.False);
        }

        [Test]
        public void ValidationArchitectureAndEvidenceLedgerAreLinked()
        {
            string architecture = ReadAssetText("Assets/StellarFrameworkVerification/ValidationArchitecture.md");
            string rootReadme = ReadAssetText("README.md");
            string testsGuide = ReadAssetText("Assets/StellarFramework/FrameworkDoc/08-Validation/Tests-说明文档-Guide.md");
            string matrix = ReadAssetText("Assets/StellarFramework/FrameworkDoc/08-Validation/KitExportValidationMatrix.md");

            Assert.That(architecture, Does.Contain("StellarFramework 验证架构与发布验收规范"));
            Assert.That(architecture, Does.Contain("Kit Behavior"));
            Assert.That(architecture, Does.Contain("Performance"));
            Assert.That(architecture, Does.Contain("Framework Policy"));
            Assert.That(architecture, Does.Contain("Integration"));
            Assert.That(architecture, Does.Contain("Release"));
            Assert.That(rootReadme, Does.Contain("ValidationArchitecture.md"));
            Assert.That(testsGuide, Does.Contain("ValidationArchitecture.md"));
            Assert.That(matrix, Does.Contain("Evidence Ledger"));
        }

        [Test]
        public void VerificationAreaIsMaintainerOnlyAndExcludedFromDistribution()
        {
            string verificationReadme = ReadAssetText("Assets/StellarFrameworkVerification/README.md");
            string catalog = ReadAssetText("Assets/StellarFramework/KitCatalog/KitDistributionCatalog.json");
            string publisher = ReadAssetText(
                "Assets/StellarFramework/Editor/StellarToolsHub/Modules/Packaging/StellarFrameworkPackagePublisher.cs");

            Assert.That(verificationReadme, Does.Contain("Maintainer-only"));
            Assert.That(verificationReadme, Does.Contain("Not distributed"));
            Assert.That(catalog, Does.Not.Contain("StellarFrameworkVerification"));
            Assert.That(publisher, Does.Contain("Assets/StellarFrameworkVerification"));
        }

        [Test]
        public void HotUpdateReleaseGateIsRuntimeDiscoverableAndEditorPreparationIsSeparate()
        {
            const string testAssetPath =
                "Assets/StellarFramework/Tests/PlayMode/YooAssetHotUpdate/YooAssetHotUpdateEndToEndTests.cs";
            const string assemblyAssetPath =
                "Assets/StellarFramework/Tests/PlayMode/YooAssetHotUpdate/StellarFramework.YooAssetHotUpdate.PlayMode.Tests.asmdef";
            const string builderAssetPath =
                "Assets/StellarFrameworkVerification/Editor/YooAssetHotUpdateVerificationBuilder.cs";

            AssemblyDefinitionData assembly = JsonUtility.FromJson<AssemblyDefinitionData>(
                ReadAssetText(assemblyAssetPath));
            Assert.That(assembly, Is.Not.Null);
            CollectionAssert.Contains(assembly.references, "StellarFramework.Verification.Runtime");
            CollectionAssert.Contains(assembly.references, "UniTask");
            CollectionAssert.DoesNotContain(assembly.references, "StellarFramework.Verification.Editor");
            CollectionAssert.DoesNotContain(assembly.references, "YooAsset.Editor");
            Assert.That(assembly.includePlatforms, Is.Empty,
                "PlayMode TestAssemblies must compile for the Runtime target, not Editor only.");

            string testSource = ReadAssetText(testAssetPath);
            Assert.That(testSource, Does.Contain("namespace StellarFramework.Tests.ReleaseGate"));
            Assert.That(testSource, Does.Contain("HotUpdateRuntimeVerification.RunAsync"));
            Assert.That(testSource, Does.Contain("HotUpdateVerificationPaths.RuntimeConfigFilePath"));
            Assert.That(testSource, Does.Contain("precondition missing"));
            Assert.That(testSource, Does.Not.Contain("UnityEditor"));
            Assert.That(testSource, Does.Not.Contain("YooAsset.Editor"));
            Assert.That(testSource, Does.Not.Contain("StellarFrameworkVerification.Editor"));

            string builderSource = ReadAssetText(builderAssetPath);
            Assert.That(
                builderSource,
                Does.Contain("[MenuItem(\"Tools/StellarFramework/Verification/Prepare HotUpdate PlayMode Release Gate\")]") );
            Assert.That(builderSource, Does.Contain("HotUpdateVerificationPaths.RuntimeConfigFilePath"));

            string sharedPaths = ReadAssetText("Assets/StellarFrameworkVerification/Runtime/HotUpdateVerificationPaths.cs");
            Assert.That(sharedPaths, Does.Contain("PackageName"));
            Assert.That(sharedPaths, Does.Contain("PackageVersion"));
            Assert.That(sharedPaths, Does.Contain("RuntimeConfigFilePath"));
            Assert.That(sharedPaths, Does.Contain("RuntimeResultFilePath"));
        }

        [Test]
        public void AndroidHotUpdateReleaseGateUsesAndroidArtifactsIntentAndStructuredRuntimeEvidence()
        {
            const string builderPath =
                "Assets/StellarFrameworkVerification/Editor/YooAssetHotUpdateVerificationBuilder.cs";
            const string builderAssemblyPath =
                "Assets/StellarFrameworkVerification/Editor/StellarFramework.Verification.Editor.asmdef";
            const string runtimePath =
                "Assets/StellarFrameworkVerification/Runtime/HotUpdateRuntimeVerification.cs";
            const string hybridClrAdapterPath =
                "Assets/StellarFramework/Runtime/Kits/HybridCLRKit/Runtime/HybridCLRHotUpdateAdapter.cs";
            const string runtimeAssemblyPath =
                "Assets/StellarFrameworkVerification/Runtime/StellarFramework.Verification.Runtime.asmdef";
            const string androidBuildPath =
                "Assets/Editor/StellarFrameworkAndroidReleaseVerificationBuild.cs";
            const string androidPipelinePath =
                "Tools/AndroidVerification/Invoke-StellarAndroidReleaseVerification.ps1";
            const string smokePath =
                "Tools/AndroidVerification/Invoke-StellarApkSmoke.ps1";
            const string androidStartPath =
                "Tools/AndroidVerification/Start-StellarAndroid.ps1";
            const string cdnPath =
                "Tools/AndroidVerification/StellarVerificationCdn.py";

            string builder = ReadAssetText(builderPath);
            string runtime = ReadAssetText(runtimePath);
            string hybridClrAdapter = ReadAssetText(hybridClrAdapterPath);
            string androidBuild = ReadAssetText(androidBuildPath);
            string pipeline = ReadAssetText(androidPipelinePath);
            string smoke = ReadAssetText(smokePath);
            string androidReleasePipeline = ReadAssetText(
                "Tools/AndroidVerification/Invoke-StellarAndroidReleaseVerification.ps1");
            string androidStart = ReadAssetText(androidStartPath);
            string cdn = ReadAssetText(cdnPath);
            AssemblyDefinitionData editorAssembly = JsonUtility.FromJson<AssemblyDefinitionData>(
                ReadAssetText(builderAssemblyPath));
            AssemblyDefinitionData runtimeAssembly = JsonUtility.FromJson<AssemblyDefinitionData>(
                ReadAssetText(runtimeAssemblyPath));

            Assert.That(editorAssembly, Is.Not.Null);
            CollectionAssert.Contains(editorAssembly.references, "HybridCLR.Editor");
            CollectionAssert.Contains(editorAssembly.references, "StellarFramework.ToolsHub.HybridCLRKit.Editor");
            Assert.That(runtimeAssembly, Is.Not.Null);
            CollectionAssert.DoesNotContain(runtimeAssembly.references, "HybridCLR.Editor");
            CollectionAssert.DoesNotContain(runtimeAssembly.references, "YooAsset.Editor");

            Assert.That(builder, Does.Contain("PrebuildCommand.GenerateAll()"));
            Assert.That(builder, Does.Contain("GetGeneratedAotSourceDirectory(BuildTarget.Android)"));
            Assert.That(builder, Does.Contain("ExportGeneratedAssets(BuildTarget.Android)"));
            Assert.That(builder, Does.Contain("BuildTarget.Android"));
            Assert.That(builder, Does.Contain("finally"));
            Assert.That(builder, Does.Contain("Prepare Android HotUpdate Release Gate"));

            Assert.That(runtime, Does.Contain("RunAndroidRemoteAsync"));
            Assert.That(runtime, Does.Contain("AndroidJavaClass(\"com.unity3d.player.UnityPlayer\")"));
            Assert.That(runtime, Does.Contain("HotUpdateVerificationPaths.AndroidVerifyIntentExtra"));
            Assert.That(runtime, Does.Contain("aotMetadataLoadSucceeded"));
            Assert.That(runtime, Does.Contain("HybridCLRHook.AOTMetaAssemblyFiles"));
            Assert.That(runtime, Does.Contain("[StellarHotUpdateVerificationChunk] "));
            Assert.That(runtime, Does.Contain("Convert.ToBase64String(Encoding.UTF8.GetBytes(json))"));
            Assert.That(runtime, Does.Contain("[StellarHotUpdateVerificationStage] "));
            Assert.That(runtime, Does.Contain("PackageCleanupStarted"));
            Assert.That(runtime, Does.Contain("ContentUpdateStarted"));
            Assert.That(runtime, Does.Contain("HybridCLRRunStarted"));
            Assert.That(runtime, Does.Contain("entryMarkerObserved"));
            Assert.That(hybridClrAdapter, Does.Contain("RuntimeApi.LoadMetadataForAOTAssembly"));
            Assert.That(hybridClrAdapter, Does.Contain("Assembly.Load(hotUpdateDllBytes)"));
            Assert.That(hybridClrAdapter, Does.Contain("method.Invoke(null, null)"));

            Assert.That(androidBuild, Does.Contain("BuildHotUpdateRelease"));
            Assert.That(androidBuild, Does.Contain("ScriptingImplementation.IL2CPP"));
            Assert.That(androidBuild, Does.Contain("AndroidArchitecture.X86_64"));
            Assert.That(androidBuild, Does.Contain("EditorUserBuildSettings.development = false"));
            Assert.That(androidBuild, Does.Contain("insecureHttpOption"));
            Assert.That(androidBuild, Does.Contain("finally"));

            Assert.That(pipeline, Does.Contain("Prepare Android HotUpdate Release Gate"));
            Assert.That(pipeline, Does.Contain("Build Android Release HotUpdate Verification APK"));
            Assert.That(pipeline, Does.Contain("adb reverse"));
            Assert.That(pipeline, Does.Contain("if ($HotUpdate) { 4096 } else { 2048 }"));
            Assert.That(pipeline, Does.Contain("Get-StellarAndroidMemoryKilobytes"));
            Assert.That(pipeline, Does.Contain("StellarVerificationCdn.py"));
            Assert.That(pipeline, Does.Contain("productVerificationStatus = 'PASS'"));
            Assert.That(androidReleasePipeline, Does.Contain("$statusCode -ne 504"));
            Assert.That(androidReleasePipeline, Does.Contain("GatewayTimeoutRecoveredFromBuildState"));
            Assert.That(androidReleasePipeline, Does.Contain("$state.buildResult -ne 'Succeeded'"));
            Assert.That(androidReleasePipeline, Does.Contain("buildRequestTransportWarning"));
            Assert.That(smoke, Does.Contain("Read-StellarHotUpdatePassRecord"));
            Assert.That(smoke, Does.Contain("StellarHotUpdateVerificationChunk"));
            Assert.That(smoke, Does.Contain("FromBase64String($encodedJson)"));
            Assert.That(smoke, Does.Contain("incomplete; expected"));
            Assert.That(smoke, Does.Contain("Get-StellarAndroidStartupPromptButton"));
            Assert.That(smoke, Does.Contain("android:id/aerr_wait"));
            Assert.That(smoke, Does.Contain("Wait-StellarHotUpdateBootstrap"));
            Assert.That(smoke, Does.Contain("BootstrapEntered"));
            Assert.That(smoke, Does.Contain("coldStartSystemPromptsHandled"));
            Assert.That(smoke, Does.Contain("downloadedFileCount"));
            Assert.That(smoke, Does.Contain("entryMarkerObserved"));
            Assert.That(smoke, Does.Contain("EmulatorMemoryMegabytes"));
            Assert.That(androidStart, Does.Contain("[int] $MemoryMegabytes = 2048"));
            Assert.That(androidStart, Does.Contain("'-memory', [string]$MemoryMegabytes"));
            Assert.That(cdn, Does.Contain("Accept-Ranges"));
            Assert.That(cdn, Does.Contain("Content-Range"));
            Assert.That(cdn, Does.Contain("127.0.0.1"));
        }

        private static string ReadAssetText(string assetPath)
        {
            return File.ReadAllText(ToAbsoluteAssetPath(assetPath));
        }

        private static string ToAbsoluteAssetPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        [Serializable]
        private sealed class AssemblyDefinitionData
        {
            public string[] references;
            public string[] includePlatforms;
        }
    }
}
