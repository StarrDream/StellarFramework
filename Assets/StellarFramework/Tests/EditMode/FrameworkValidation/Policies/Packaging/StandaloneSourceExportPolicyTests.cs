using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class StandaloneSourceExportPolicyTests
    {
        [Test]
        public void RecommendedProfilesResolveToStableDependencyClosures()
        {
            Type publisherType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(
                    "StellarFramework.Editor.Modules.StellarFrameworkPackagePublisher", false))
                .FirstOrDefault(type => type != null);
            Assert.That(publisherType, Is.Not.Null);

            MethodInfo resolveMethod = publisherType.GetMethod(
                "ResolveRecommendedProfileClosureIds",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(resolveMethod, Is.Not.Null);

            string[] localizationClosure =
                (string[])resolveMethod.Invoke(null, new object[] { "localization.complete" });
            Assert.That(localizationClosure, Is.EqualTo(new[]
            {
                "localizationkit.core",
                "localizationkit.editor",
                "localizationkit.tmp",
                "localizationkit.tmp.editor",
                "localizationkit.tmp.tools",
                "localizationkit.tools",
                "localizationkit.ugui",
                "toolshub.core"
            }));

            string[] resKitClosure =
                (string[])resolveMethod.Invoke(null, new object[] { "reskit.complete" });
            Assert.That(resKitClosure, Is.EqualTo(new[]
            {
                "generated.assetmap",
                "logkit",
                "poolkit",
                "reskit.core",
                "reskit.tools",
                "toolshub.core"
            }));

            string[] uiClosure = (string[])resolveMethod.Invoke(null, new object[] { "uikit.complete" });
            Assert.That(uiClosure, Is.EqualTo(new[]
            {
                "generated.assetmap",
                "logkit",
                "poolkit",
                "reskit.core",
                "reskit.tools",
                "runtime.core",
                "singletonkit",
                "toolshub.core",
                "uikit.adaptation",
                "uikit.adaptation.tools",
                "uikit.core",
                "uikit.reskit",
                "uikit.tools"
            }));

            string[] hotUpdateClosure = (string[])resolveMethod.Invoke(null, new object[] { "hotupdate.full" });
            Assert.That(hotUpdateClosure, Is.EqualTo(new[]
            {
                "generated.assetmap",
                "hybridclrkit",
                "hybridclrkit.tools",
                "logkit",
                "poolkit",
                "reskit.core",
                "reskit.tools",
                "reskit.yooasset",
                "toolshub.core"
            }));
        }

        [Test]
        public void PublisherDefinesStandaloneArchitectureAndExtensionsExports()
        {
            string source = ReadAssetText(
                "Assets/StellarFramework/Editor/StellarToolsHub/Modules/Packaging/StellarFrameworkPackagePublisher.cs");

            Assert.That(source, Does.Contain("ExportStandaloneArchitecture"));
            Assert.That(source, Does.Contain("ExportStandaloneExtensions"));
            Assert.That(source, Does.Contain("StellarArchitecture.cs"));
            Assert.That(source, Does.Contain("StellarExtensions.cs"));
        }

        [Test]
        public void StandaloneExportsReplaceLogKitCallsInsteadOfExportingLogKitDependency()
        {
            string source = ReadAssetText(
                "Assets/StellarFramework/Editor/StellarToolsHub/Modules/Packaging/StellarFrameworkPackagePublisher.cs");

            Assert.That(source, Does.Contain("ReplaceLogKitCalls"));
            Assert.That(source, Does.Contain("Debug.LogError"));
            Assert.That(source, Does.Contain("Debug.LogWarning"));
            Assert.That(source, Does.Contain("Debug.Log"));
        }

        [Test]
        public void DistributionCatalogKeepsHybridClrOutOfStandaloneProfiles()
        {
            string catalog = ReadAssetText("Assets/StellarFramework/KitCatalog/KitDistributionCatalog.json");

            Assert.That(catalog, Does.Contain("standalone.architecture"));
            Assert.That(catalog, Does.Contain("standalone.extensions"));
            Assert.That(catalog, Does.Contain("\"id\": \"hybridclrkit\""));
            Assert.That(catalog, Does.Contain("com.code-philosophy.hybridclr"));
            Assert.That(catalog, Does.Contain("Runtime/Kits/HybridCLRKit"));
            Assert.That(catalog, Does.Not.Contain("\"id\": \"hotupdate.addressables\""));
            Assert.That(catalog, Does.Contain("\"excludedCapabilities\": [\"Addressables\", \"HybridCLR\", \"CodeHotUpdate\"]"));
        }

        [Test]
        public void RuntimeToolsProfileIsIndependentFromRuntimeCoreAndOptionalPackages()
        {
            string catalog = ReadAssetText("Assets/StellarFramework/KitCatalog/KitDistributionCatalog.json");
            string runner = ReadAssetText("Assets/StellarFramework/Runtime/Tools/CoroutineRunner.cs");
            string asmdef = ReadAssetText("Assets/StellarFramework/Runtime/Tools/StellarFramework.Runtime.Tools.asmdef");

            Assert.That(catalog, Does.Contain("\"id\": \"runtime.tools\""));
            Assert.That(catalog, Does.Contain("StellarFramework-Runtime-Tools.unitypackage"));
            Assert.That(catalog, Does.Contain("\"requiredProfileIds\": [\"runtime.core\", \"singletonkit\"]"));
            Assert.That(catalog, Does.Contain("\"id\": \"uikit.tools\""));
            Assert.That(catalog, Does.Contain("\"id\": \"reskit.tools\""));
            Assert.That(catalog, Does.Contain("\"id\": \"hybridclrkit.tools\""));
            Assert.That(catalog, Does.Contain("\"id\": \"reskit.assetbundle\""));
            Assert.That(catalog,
                Does.Contain("\"requiredKits\": [\"ResKit.Core\", \"SingletonKit\", \"Generated.AssetMap\"]"));
            Assert.That(runner, Does.Not.Contain("MonoSingleton<"));
            Assert.That(runner, Does.Not.Contain("LogKit."));
            Assert.That(runner, Does.Not.Contain("[Singleton"));
            Assert.That(asmdef, Does.Contain("\"references\": []"));
        }

        [Test]
        public void KitExportsFlattenSharedRuntimeIntoArchitectureAndExtensionsFiles()
        {
            string publisher = ReadAssetText(
                "Assets/StellarFramework/Editor/StellarToolsHub/Modules/Packaging/StellarFrameworkPackagePublisher.cs");
            string bootstrap = ReadAssetText(
                "Assets/Editor/StellarFramework/KitPackageBootstrap/StellarFrameworkKitPackageBootstrapInstaller.cs");

            Assert.That(publisher, Does.Contain("flattenRuntimeSources = profiles.Any"));
            Assert.That(bootstrap, Does.Contain("TryFlattenRuntimeSources"));
            Assert.That(bootstrap, Does.Contain("Assets/StellarFramework/Runtime/StellarArchitecture.cs"));
            Assert.That(bootstrap, Does.Contain("Assets/StellarFramework/Runtime/StellarExtensions.cs"));
            Assert.That(bootstrap, Does.Contain("AssetDatabase.DeleteAsset(RuntimeArchitectureSourcePath)"));
            Assert.That(bootstrap, Does.Contain("RestoreRuntimeSources"));
            Assert.That(bootstrap, Does.Contain("StellarFramework-Architecture-"));
            Assert.That(
                bootstrap.TrimStart(),
                Does.StartWith("#if UNITY_EDITOR"),
                "Kit bootstrap must remain hard-gated from Player/SBP script compilation.");
        }

        [Test]
        public void EventKitHasNoFrameworkAssemblyDependency()
        {
            string asmdef = ReadAssetText(
                "Assets/StellarFramework/Runtime/Kits/EventKit/StellarFramework.EventKit.asmdef");
            string source = ReadAssetText("Assets/StellarFramework/Runtime/Kits/EventKit/EventCore.cs");

            Assert.That(asmdef, Does.Contain("\"references\": []"));
            Assert.That(source, Does.Not.Contain("LogKit."));
            Assert.That(source, Does.Contain("Debug.LogError"));
        }

        [Test]
        public void ConfigKitCoreLeavesNewtonsoftJsonInAnExplicitAdapter()
        {
            string coreAsmdef = ReadAssetText(
                "Assets/StellarFramework/Runtime/Kits/ConfigKit/StellarFramework.ConfigKit.asmdef");
            string textSource = ReadAssetText(
                "Assets/StellarFramework/Runtime/Kits/ConfigKit/Core/ConfigTextSource.cs");
            string jsonAsmdef = ReadAssetText(
                "Assets/StellarFramework/Runtime/Kits/ConfigKit/Adapters/NewtonsoftJson/StellarFramework.ConfigKit.Json.asmdef");
            string catalog = ReadAssetText("Assets/StellarFramework/KitCatalog/KitDistributionCatalog.json");

            Assert.That(coreAsmdef, Does.Contain("StellarFramework.ConfigKit.Core"));
            Assert.That(coreAsmdef, Does.Not.Contain("StellarFramework.LogKit"));
            Assert.That(textSource, Does.Not.Contain("Newtonsoft"));
            Assert.That(textSource, Does.Contain("IConfigTextSource"));
            Assert.That(jsonAsmdef, Does.Contain("StellarFramework.ConfigKit.Core"));
            Assert.That(jsonAsmdef, Does.Contain("f51ebe6a0ceec4240a699833d6309b23"));
            Assert.That(catalog, Does.Contain("\"id\": \"configkit.core\""));
            Assert.That(catalog, Does.Contain("\"id\": \"configkit.json\""));
        }

        [Test]
        public void AvailableIndependentKitProfilesHaveDirectExportSources()
        {
            string catalog = ReadAssetText("Assets/StellarFramework/KitCatalog/KitDistributionCatalog.json");
            string publisher = ReadAssetText(
                "Assets/StellarFramework/Editor/StellarToolsHub/Modules/Packaging/StellarFrameworkPackagePublisher.cs");

            Assert.That(catalog, Does.Contain("\"id\": \"eventkit\""));
            Assert.That(catalog, Does.Contain("\"id\": \"configkit.core\""));
            Assert.That(catalog, Does.Contain("\"id\": \"configkit.json\""));
            Assert.That(catalog, Does.Contain("\"id\": \"fsmkit\""));
            Assert.That(catalog, Does.Contain("\"id\": \"poolkit\""));
            Assert.That(catalog, Does.Contain("\"id\": \"singletonkit\""));
            Assert.That(catalog, Does.Contain("\"id\": \"actionkit\""));
            Assert.That(catalog, Does.Contain("\"id\": \"audiokit.core\""));
            Assert.That(catalog, Does.Contain("\"id\": \"audiokit.reskit\""));
            Assert.That(catalog, Does.Contain("\"id\": \"bindablekit\""));
            Assert.That(catalog, Does.Contain("\"id\": \"httpkit\""));
            Assert.That(catalog, Does.Contain("StellarFramework-EventKit.unitypackage"));
            Assert.That(catalog, Does.Contain("StellarFramework-ConfigKit-Core.unitypackage"));
            Assert.That(catalog, Does.Contain("StellarFramework-ConfigKit-NewtonsoftJson.unitypackage"));
            Assert.That(catalog, Does.Contain("StellarFramework-FSMKit.unitypackage"));
            Assert.That(catalog, Does.Contain("StellarFramework-PoolKit.unitypackage"));
            Assert.That(catalog, Does.Contain("StellarFramework-SingletonKit.unitypackage"));
            Assert.That(catalog, Does.Contain("StellarFramework-ActionKit.unitypackage"));
            Assert.That(catalog, Does.Contain("StellarFramework-AudioKit-Core.unitypackage"));
            Assert.That(catalog, Does.Contain("StellarFramework-AudioKit-ResKitAdapter.unitypackage"));
            Assert.That(catalog, Does.Contain("StellarFramework-BindableKit.unitypackage"));
            Assert.That(catalog, Does.Contain("StellarFramework-HttpKit.unitypackage"));
            Assert.That(publisher, Does.Contain("ExportKitPackageInternal"));
            Assert.That(publisher, Does.Contain("ExportEventKitPackage"));
            Assert.That(publisher, Does.Contain("ExportActionKitPackage"));
        }

        [Test]
        public void PoolKitHasNoFrameworkAssemblyDependency()
        {
            string asmdef = ReadAssetText(
                "Assets/StellarFramework/Runtime/Kits/PoolKit/StellarFramework.PoolKit.asmdef");
            string pool = ReadAssetText("Assets/StellarFramework/Runtime/Kits/PoolKit/PoolKit.cs");
            string diagnostics = ReadAssetText("Assets/StellarFramework/Runtime/Kits/PoolKit/PoolKitDiagnostics.cs");

            Assert.That(asmdef, Does.Contain("\"references\": []"));
            Assert.That(pool, Does.Not.Contain("LogKit."));
            Assert.That(diagnostics, Does.Contain("Debug.LogError"));
        }

        [Test]
        public void SingletonKitHasNoFrameworkAssemblyDependency()
        {
            string runtimeAsmdef = ReadAssetText(
                "Assets/StellarFramework/Runtime/Kits/SingletonKit/StellarFramework.SingletonKit.asmdef");
            string editorAsmdef = ReadAssetText(
                "Assets/StellarFramework/Runtime/Kits/SingletonKit/Editor/StellarFramework.Singleton.Editor.asmdef");
            string factory = ReadAssetText("Assets/StellarFramework/Runtime/Kits/SingletonKit/SingletonFactory.cs");
            string diagnostics = ReadAssetText("Assets/StellarFramework/Runtime/Kits/SingletonKit/SingletonKitDiagnostics.cs");

            Assert.That(runtimeAsmdef, Does.Contain("\"references\": []"));
            Assert.That(editorAsmdef, Does.Not.Contain("StellarFramework.LogKit"));
            Assert.That(factory, Does.Contain("using LogKit = StellarFramework.SingletonKitDiagnostics;"));
            Assert.That(diagnostics, Does.Contain("UnityEngine.Debug.LogError"));
        }

        [Test]
        public void AddressablesLoaderIsIndependentFromHybridClrKit()
        {
            string addressablesAsmdef = ReadAssetText(
                "Assets/StellarFramework/Runtime/Kits/Reskit/Loaders/AddressableLoader/StellarFramework.ResKit.Addressables.asmdef");
            string catalog = ReadAssetText("Assets/StellarFramework/KitCatalog/KitDistributionCatalog.json");

            Assert.That(addressablesAsmdef, Does.Not.Contain("StellarFramework.HybridCLRKit"));
            Assert.That(catalog, Does.Contain("\"id\": \"reskit.addressables\""));
            Assert.That(catalog, Does.Not.Contain("\"id\": \"hotupdate.addressables\""));
        }

        [Test]
        public void ResKitCoreDoesNotRequireAssetBundleAdapter()
        {
            string core = ReadAssetText("Assets/StellarFramework/Runtime/Kits/Reskit/ResKit.cs");
            string assetBundleAsmdef = ReadAssetText(
                "Assets/StellarFramework/Runtime/Kits/Reskit/Loaders/AssetBundleLoader/StellarFramework.ResKit.AssetBundle.asmdef");
            string installer = ReadAssetText(
                "Assets/StellarFramework/Runtime/Kits/Reskit/Loaders/AssetBundleLoader/AssetBundleResKitInstaller.cs");

            Assert.That(core, Does.Not.Contain("Allocate<AssetBundleLoader>()"));
            Assert.That(assetBundleAsmdef, Does.Contain("StellarFramework.ResKit"));
            Assert.That(installer, Does.Contain("ResKit.RegisterLoader(ResKit.KeyAssetBundle"));
        }

        [Test]
        public void UIKitCoreDoesNotRequireResKitAdapter()
        {
            string coreAsmdef = ReadAssetText(
                "Assets/StellarFramework/Runtime/Kits/UIKit/StellarFramework.UIKit.asmdef");
            string core = ReadAssetText("Assets/StellarFramework/Runtime/Kits/UIKit/UIKit.cs");
            string resourcesStrategy = ReadAssetText(
                "Assets/StellarFramework/Runtime/Kits/UIKit/LoadStrategy/ResourcesUILoadStrategy.cs");
            string adapterAsmdef = ReadAssetText(
                "Assets/StellarFramework/Runtime/Kits/UIKit/Adapters/ResKit/StellarFramework.UIKit.ResKit.asmdef");
            string catalog = ReadAssetText("Assets/StellarFramework/KitCatalog/KitDistributionCatalog.json");

            Assert.That(coreAsmdef, Does.Not.Contain("StellarFramework.ResKit"));
            Assert.That(core, Does.Contain("ResourcesUILoadStrategy"));
            Assert.That(resourcesStrategy, Does.Contain("Resources.Load<GameObject>"));
            Assert.That(adapterAsmdef, Does.Contain("StellarFramework.ResKit"));
            Assert.That(catalog, Does.Contain("\"id\": \"uikit.reskit\""));
            Assert.That(catalog, Does.Contain("StellarFramework-UIKit-Core.unitypackage"));
            Assert.That(catalog, Does.Contain("StellarFramework-UIKit-ResKitAdapter.unitypackage"));
            Assert.That(catalog, Does.Contain("\"id\": \"runtime.core\""));
        }

        [Test]
        public void AudioKitCoreDoesNotRequireResKitAdapter()
        {
            string coreAsmdef = ReadAssetText(
                "Assets/StellarFramework/Runtime/Kits/AudioKit/StellarFramework.AudioKit.asmdef");
            string core = ReadAssetText("Assets/StellarFramework/Runtime/Kits/AudioKit/AudioKit.cs");
            string resourcesLoader = ReadAssetText(
                "Assets/StellarFramework/Runtime/Kits/AudioKit/Core/AudioLoader/ResourcesAudioLoader.cs");
            string adapter = ReadAssetText(
                "Assets/StellarFramework/Runtime/Kits/AudioKit/Adapters/ResKit/AudioKitResKitAdapter.cs");
            string adapterAsmdef = ReadAssetText(
                "Assets/StellarFramework/Runtime/Kits/AudioKit/Adapters/ResKit/StellarFramework.AudioKit.ResKit.asmdef");
            string catalog = ReadAssetText("Assets/StellarFramework/KitCatalog/KitDistributionCatalog.json");

            Assert.That(coreAsmdef, Does.Not.Contain("StellarFramework.ResKit"));
            Assert.That(core, Does.Not.Contain("StellarFramework.Res"));
            Assert.That(core, Does.Contain("new ResourcesAudioLoader()"));
            Assert.That(resourcesLoader, Does.Contain("Resources.Load<AudioClip>"));
            Assert.That(adapter, Does.Contain("class DefaultResKitAudioLoader"));
            Assert.That(adapterAsmdef, Does.Contain("StellarFramework.ResKit"));
            Assert.That(catalog, Does.Contain("\"id\": \"audiokit.core\""));
            Assert.That(catalog, Does.Contain("\"id\": \"audiokit.reskit\""));
        }

        [Test]
        public void SettingsKitCoreDoesNotRequireLogKitOrAudioKit()
        {
            string coreAsmdef = ReadAssetText(
                "Assets/StellarFramework/Runtime/Kits/SettingsKit/StellarFramework.SettingsKit.asmdef");
            string diagnostics = ReadAssetText(
                "Assets/StellarFramework/Runtime/Kits/SettingsKit/Core/SettingsKitDiagnostics.cs");
            string unityAdaptersAsmdef = ReadAssetText(
                "Assets/StellarFramework/Runtime/Kits/SettingsKit/Adapters/StellarFramework.SettingsKit.Adapters.asmdef");
            string audioAdapterAsmdef = ReadAssetText(
                "Assets/StellarFramework/Runtime/Kits/SettingsKit/Adapters/AudioKit/StellarFramework.SettingsKit.AudioKit.asmdef");
            string catalog = ReadAssetText("Assets/StellarFramework/KitCatalog/KitDistributionCatalog.json");

            Assert.That(coreAsmdef, Does.Not.Contain("StellarFramework.LogKit"));
            Assert.That(coreAsmdef, Does.Not.Contain("StellarFramework.AudioKit"));
            Assert.That(diagnostics, Does.Contain("Debug.LogError"));
            Assert.That(unityAdaptersAsmdef, Does.Not.Contain("StellarFramework.AudioKit"));
            Assert.That(audioAdapterAsmdef, Does.Contain("StellarFramework.AudioKit"));
            Assert.That(catalog, Does.Contain("\"id\": \"settingskit.core\""));
            Assert.That(catalog, Does.Contain("\"id\": \"settingskit.audiokit\""));
        }

        [Test]
        public void PublisherResolvesClosureAndExcludesOptionalResourceAdapters()
        {
            string publisher = ReadAssetText(
                "Assets/StellarFramework/Editor/StellarToolsHub/Modules/Packaging/StellarFrameworkPackagePublisher.cs");
            string catalog = ReadAssetText("Assets/StellarFramework/KitCatalog/KitDistributionCatalog.json");

            Assert.That(publisher, Does.Contain("ResolveProfileClosure"));
            Assert.That(publisher, Does.Contain("requiredProfileIds"));
            Assert.That(publisher, Does.Contain("excludedSourcePaths"));
            Assert.That(publisher, Does.Contain("ExportResKitAssetBundlePackage"));
            Assert.That(catalog, Does.Contain("\"id\": \"reskit.core\""));
            Assert.That(catalog, Does.Contain("StellarFramework-ResKit-AssetBundle.unitypackage"));
            Assert.That(catalog, Does.Contain("StellarFramework-ResKit-Addressables.unitypackage"));
        }

        [Test]
        public void ToolsHubCoreFiltersUnavailableKitModulesByDeclaredAssemblies()
        {
            string attribute = ReadAssetText(
                "Assets/StellarFramework/Editor/StellarToolsHub/Core/StellarToolAttribute.cs");
            string hub = ReadAssetText(
                "Assets/StellarFramework/Editor/StellarToolsHub/Core/StellarFrameworkTools.cs");

            Assert.That(attribute, Does.Contain("RequiredAssemblyNames"));
            Assert.That(hub, Does.Contain("IsModuleAvailable(attr)"));
            Assert.That(hub, Does.Contain("AppDomain.CurrentDomain.GetAssemblies()"));
        }

        [Test]
        public void ToolsHubCoreIncludesAnAssemblyBasedKitInstallationReport()
        {
            string report = ReadAssetText(
                "Assets/StellarFramework/Editor/StellarToolsHub/Modules/KitInstallationHubModule.cs");
            string catalog = ReadAssetText("Assets/StellarFramework/KitCatalog/KitDistributionCatalog.json");

            Assert.That(report, Does.Contain("AppDomain.CurrentDomain.GetAssemblies()"));
            Assert.That(report, Does.Contain("Kit 安装状态"));
            Assert.That(report, Does.Contain("StellarFramework.AudioKit.ResKit"));
            Assert.That(report, Does.Contain("StellarFramework.ConfigKit.Json"));
            Assert.That(catalog, Does.Contain("Modules/KitInstallationHubModule.cs"));
        }

        [Test]
        public void SamplesAreNotDistributionProfilesOrExporterEntryPoints()
        {
            string catalog = ReadAssetText("Assets/StellarFramework/KitCatalog/KitDistributionCatalog.json");
            string publisher = ReadAssetText(
                "Assets/StellarFramework/Editor/StellarToolsHub/Modules/Packaging/StellarFrameworkPackagePublisher.cs");
            Assert.That(catalog, Does.Not.Contain("\"kind\": \"sample\""));
            Assert.That(catalog, Does.Not.Contain("\"id\": \"samples."));
            Assert.That(publisher, Does.Not.Contain("SamplePackage"));
            Assert.That(publisher, Does.Not.Contain("OptionalSampleProfileIds"));
            Assert.That(publisher, Does.Contain("\"Assets/StellarFramework/Samples\""));
        }

        [Test]
        public void GridKitKeepsZeroDependencyRuntimeBoundary()
        {
            string root = Path.Combine(Application.dataPath, "StellarFramework/Runtime/Kits/GridKit");
            string asmdef = ReadAssetText(
                "Assets/StellarFramework/Runtime/Kits/GridKit/StellarFramework.GridKit.Core.asmdef");
            string catalog = ReadAssetText("Assets/StellarFramework/KitCatalog/KitDistributionCatalog.json");

            Assert.That(Directory.Exists(root), Is.True);
            Assert.That(asmdef, Does.Contain("\"references\": []"));
            Assert.That(asmdef, Does.Contain("\"noEngineReferences\": true"));
            Assert.That(catalog, Does.Contain("\"id\": \"gridkit\""));
            Assert.That(catalog, Does.Contain("StellarFramework-GridKit.unitypackage"));

            foreach (string sourcePath in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string source = File.ReadAllText(sourcePath);
                Assert.That(source, Does.Not.Contain("using UnityEngine"), sourcePath);
                Assert.That(source, Does.Not.Contain("Addressables"), sourcePath);
                Assert.That(source, Does.Not.Contain("HybridCLR"), sourcePath);
                Assert.That(source, Does.Not.Contain("UniTask"), sourcePath);
                Assert.That(source, Does.Not.Contain("Newtonsoft"), sourcePath);
            }
        }

        [Test]
        public void SpatialKitKeepsZeroDependencyRuntimeBoundary()
        {
            string root = Path.Combine(Application.dataPath, "StellarFramework/Runtime/Kits/SpatialKit");
            string asmdef = ReadAssetText(
                "Assets/StellarFramework/Runtime/Kits/SpatialKit/StellarFramework.SpatialKit.Core.asmdef");
            string catalog = ReadAssetText("Assets/StellarFramework/KitCatalog/KitDistributionCatalog.json");

            Assert.That(Directory.Exists(root), Is.True);
            Assert.That(asmdef, Does.Contain("\"references\": []"));
            Assert.That(asmdef, Does.Contain("\"noEngineReferences\": true"));
            Assert.That(catalog, Does.Contain("\"id\": \"spatialkit\""));
            Assert.That(catalog, Does.Contain("StellarFramework-SpatialKit.unitypackage"));

            foreach (string sourcePath in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string source = File.ReadAllText(sourcePath);
                Assert.That(source, Does.Not.Contain("using UnityEngine"), sourcePath);
                Assert.That(source, Does.Not.Contain("GridKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("Addressables"), sourcePath);
                Assert.That(source, Does.Not.Contain("HybridCLR"), sourcePath);
                Assert.That(source, Does.Not.Contain("UniTask"), sourcePath);
                Assert.That(source, Does.Not.Contain("Newtonsoft"), sourcePath);
                Assert.That(source, Does.Not.Contain("MonoBehaviour"), sourcePath);
                Assert.That(source, Does.Not.Contain("IEnumerable"), sourcePath);
            }
        }

        [Test]
        public void SourceOnlyExporterWindowCombinesSelectedKitClosures()
        {
            string publisher = ReadAssetText(
                "Assets/StellarFramework/Editor/StellarToolsHub/Modules/Packaging/StellarFrameworkPackagePublisher.cs");
            string window = ReadAssetText(
                "Assets/StellarFramework/Editor/StellarToolsHub/Modules/Packaging/StellarFrameworkPackageExportWindow.cs");

            Assert.That(publisher, Does.Contain("ExportKitPackageGroupInternal"));
            Assert.That(publisher, Does.Contain("NormalizePackageFileName"));
            Assert.That(publisher, Does.Contain("WriteCombinedKitDependencyGuide"));
            Assert.That(publisher, Does.Contain("IsFrameworkSourceProject"));
            Assert.That(publisher, Does.Contain("Modules/Packaging"));
            Assert.That(window, Does.Contain("[MenuItem(\"StellarFramework/Export\")]"));
            Assert.That(window, Does.Contain("ExportKitPackageGroupInternal"));
            Assert.That(window, Does.Contain("自动合并依赖"));
            Assert.That(window, Does.Contain("01  基础功能"));
            Assert.That(window, Does.Contain("02  完整功能"));
            Assert.That(window, Does.Contain("03  扩展功能"));
            Assert.That(window, Does.Contain("GetBasicDeliveryProfiles"));
            Assert.That(window, Does.Contain("GetAtomicExtensionProfiles"));
            Assert.That(window, Does.Contain("deliveryGroup"));
            Assert.That(window, Does.Contain("TwoPaneSplitView"));
            Assert.That(window, Does.Contain("ToolbarSearchField"));
            Assert.That(window, Does.Contain("自动带依赖"));
            Assert.That(window, Does.Contain("独立"));
            Assert.That(window, Does.Contain("Architecture.cs"));
            Assert.That(window, Does.Contain("Extensions.cs"));
            Assert.That(window, Does.Not.Contain("StellarFrameworkTools"));
        }

        [Test]
        public void StellarFrameworkTopMenuOnlyExposesToolsHubAndExport()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string assetsRoot = Path.Combine(projectRoot, "Assets");
            var menuPaths = new List<string>();

            foreach (string filePath in Directory.GetFiles(assetsRoot, "*.cs", SearchOption.AllDirectories))
            {
                foreach (string line in File.ReadLines(filePath))
                {
                    const string marker = "[MenuItem(\"StellarFramework/";
                    int markerIndex = line.IndexOf(marker, StringComparison.Ordinal);
                    if (markerIndex < 0)
                    {
                        continue;
                    }

                    int pathStart = markerIndex + "[MenuItem(\"".Length;
                    int pathEnd = line.IndexOf('"', pathStart);
                    Assert.That(pathEnd, Is.GreaterThan(pathStart), filePath);
                    menuPaths.Add(line.Substring(pathStart, pathEnd - pathStart));
                }
            }

            Assert.That(menuPaths, Is.Not.Empty);
            Assert.That(menuPaths.All(path =>
                    path == "StellarFramework/Tools Hub %#t" ||
                    path == "StellarFramework/Export"),
                Is.True,
                "StellarFramework 顶层菜单只允许 Tools Hub 与 Export；Kit 专属功能应优先进入 ToolsHub。");
            Assert.That(menuPaths.Count(path => path == "StellarFramework/Tools Hub %#t"), Is.EqualTo(1));
            Assert.That(menuPaths.Count(path => path == "StellarFramework/Export"), Is.EqualTo(2),
                "Export 有一个执行 MenuItem 和一个 validation MenuItem。");

            const string legacyToolsPrefix = "[MenuItem(\"Tools/Stellar Framework/";
            foreach (string filePath in Directory.GetFiles(assetsRoot, "*.cs", SearchOption.AllDirectories))
            {
                string source = File.ReadAllText(filePath);
                Assert.That(source, Does.Not.Contain(legacyToolsPrefix),
                    $"Kit 工具应进入 ToolsHub，不应重新创建 legacy Tools/Stellar Framework 菜单：{filePath}");
            }

            string resKitHub = ReadAssetText(
                "Assets/StellarFramework/Editor/StellarToolsHub/Modules/ResKit/ResKitAuditHubModule.cs");
            string assetsMapGenerator = ReadAssetText(
                "Assets/StellarFramework/Editor/StellarToolsHub/Modules/ResKit/AssetsMapGenerator.cs");
            string hybridClr = ReadAssetText(
                "Assets/StellarFramework/Editor/StellarToolsHub/Modules/HybridCLRKit/HybridCLRHotUpdateAssetExporter.cs");

            Assert.That(resKitHub, Does.Contain("AssetsMapGenerator.GenerateIfNeeded"));
            Assert.That(resKitHub, Does.Contain("重建 AssetsMap"));
            Assert.That(assetsMapGenerator, Does.Not.Contain("StellarFramework/ResKit/Regenerate AssetsMap"));
            Assert.That(hybridClr, Does.Contain("[StellarTool(\"HybridCLR DLL 导出\""));
            Assert.That(hybridClr, Does.Not.Contain("StellarFramework/Verification/Export HybridCLR Generated Assets"));
        }

        [Test]
        public void KitExportsInstallOnlyTheirDeclaredUpmDependencies()
        {
            string publisher = ReadAssetText(
                "Assets/StellarFramework/Editor/StellarToolsHub/Modules/Packaging/StellarFrameworkPackagePublisher.cs");
            string installer = ReadAssetText(
                "Assets/Editor/StellarFramework/KitPackageBootstrap/StellarFrameworkKitPackageBootstrapInstaller.cs");
            string catalog = ReadAssetText("Assets/StellarFramework/KitCatalog/KitDistributionCatalog.json");

            Assert.That(publisher, Does.Contain("CreateKitBootstrapAssets"));
            Assert.That(publisher, Does.Contain("CreateTemporaryPayloadPath"));
            Assert.That(publisher, Does.Contain("KitBootstrapPayloadPrefix"));
            Assert.That(publisher, Does.Not.Contain("KitDependencyInstaller"));
            Assert.That(publisher, Does.Contain("GetRequiredUpm"));
            Assert.That(publisher, Does.Contain("自动调用 Unity Package Manager"));
            Assert.That(publisher, Does.Contain("UpmPackageSources"));
            Assert.That(publisher, Does.Contain("KitBootstrapRequestPrefix"));
            Assert.That(installer, Does.Contain("[InitializeOnLoad]"));
            Assert.That(installer, Does.Contain("AssetDatabase.ImportPackage"));
            Assert.That(installer, Does.Contain("PendingRequestSessionKey"));
            Assert.That(installer, Does.Contain("PayloadWasImported"));
            Assert.That(installer, Does.Contain("IsFrameworkSourceProject"));
            Assert.That(installer, Does.Contain("TryCleanupBootstrap"));
            Assert.That(installer, Does.Contain("RequestSearchPattern"));
            Assert.That(installer, Does.Contain("Client.Add(dependency.source)"));
            Assert.That(installer, Does.Contain("PackageDependency[] dependencies"));
            Assert.That(installer, Does.Not.Contain("com.code-philosophy.hybridclr"));
            Assert.That(installer, Does.Not.Contain("com.unity.addressables"));
            Assert.That(publisher, Does.Contain("com.cysharp.unitask"));
            Assert.That(publisher, Does.Contain("com.unity.nuget.newtonsoft-json"));
            Assert.That(publisher, Does.Contain("com.unity.addressables"));
            Assert.That(publisher, Does.Contain("com.unity.ugui"));
            Assert.That(publisher, Does.Contain("com.code-philosophy.hybridclr"));
            Assert.That(publisher, Does.Contain("4feac30cb2e105992986c737f7f54992b8300e1a"));
            Assert.That(catalog, Does.Contain("\"requiredUpm\""));
        }

        [Test]
        public void KitExportWrapsRuntimeSourcesInBootstrapPayload()
        {
            Type publisherType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("StellarFramework.Editor.Modules.StellarFrameworkPackagePublisher", false))
                .FirstOrDefault(type => type != null);
            Assert.That(publisherType, Is.Not.Null);

            MethodInfo exportMethod = publisherType.GetMethod("ExportKitPackageGroupInternal",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(exportMethod, Is.Not.Null);

            const string outputFileName = "Validation-KitBootstrap-EventKit.unitypackage";
            string outputPath = null;
            string guidePath = null;
            try
            {
                outputPath = (string)exportMethod.Invoke(null,
                    new object[] { new[] { "eventkit" }, outputFileName });
                guidePath = Path.Combine(Path.GetDirectoryName(outputPath) ?? string.Empty,
                    Path.GetFileNameWithoutExtension(outputFileName) + "-Dependencies.md");

                Assert.That(File.Exists(outputPath), Is.True);
                string[] outerPackagePaths = ReadUnityPackagePaths(outputPath);
                Assert.That(outerPackagePaths, Does.Contain(
                    "Assets/Editor/StellarFramework/KitPackageBootstrap/StellarFrameworkKitPackageBootstrapInstaller.cs"));
                Assert.That(outerPackagePaths, Does.Contain(
                    "Assets/Editor/StellarFramework/KitPackageBootstrap/__StellarFramework-KitBootstrap-Validation-KitBootstrap-EventKit.json"));
                Assert.That(outerPackagePaths, Does.Contain(
                    "Assets/Editor/StellarFramework/KitPackageBootstrap/__StellarFramework-KitPayload-Validation-KitBootstrap-EventKit.unitypackage.bytes"));
                Assert.That(outerPackagePaths, Does.Not.Contain(
                    "Assets/StellarFramework/Runtime/Kits/EventKit/StellarFramework.EventKit.asmdef"));
                Assert.That(File.ReadAllText(guidePath), Does.Contain("Bootstrap 会直接导入 Kit payload"));
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(outputPath) && File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }

                if (!string.IsNullOrWhiteSpace(guidePath) && File.Exists(guidePath))
                {
                    File.Delete(guidePath);
                }
            }
        }

        [Test]
        public void LocalizationCompleteExportSerializesTmpDependencyWithoutBundlingTmpProjectAssets()
        {
            Type publisherType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(
                    "StellarFramework.Editor.Modules.StellarFrameworkPackagePublisher",
                    false))
                .FirstOrDefault(type => type != null);
            Assert.That(publisherType, Is.Not.Null);

            MethodInfo exportMethod = publisherType.GetMethod(
                "ExportKitPackageGroupInternal",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(exportMethod, Is.Not.Null);

            const string outputFileName = "Validation-Localization-Complete.unitypackage";
            string outputPath = null;
            string guidePath = null;
            try
            {
                outputPath = (string)exportMethod.Invoke(
                    null,
                    new object[]
                    {
                        new[] { "localizationkit.tools", "localizationkit.tmp.tools" },
                        outputFileName
                    });
                guidePath = Path.Combine(
                    Path.GetDirectoryName(outputPath) ?? string.Empty,
                    Path.GetFileNameWithoutExtension(outputFileName) + "-Dependencies.md");

                string requestPath =
                    "Assets/Editor/StellarFramework/KitPackageBootstrap/" +
                    "__StellarFramework-KitBootstrap-Validation-Localization-Complete.json";
                string payloadPath =
                    "Assets/Editor/StellarFramework/KitPackageBootstrap/" +
                    "__StellarFramework-KitPayload-Validation-Localization-Complete.unitypackage.bytes";

                byte[] requestBytes = ReadUnityPackageAsset(outputPath, requestPath);
                byte[] payloadBytes = ReadUnityPackageAsset(outputPath, payloadPath);
                Assert.That(requestBytes, Is.Not.Null.And.Not.Empty);
                Assert.That(payloadBytes, Is.Not.Null.And.Not.Empty);

                string requestJson = Encoding.UTF8.GetString(requestBytes);
                Assert.That(requestJson, Does.Contain("\"packageId\": \"com.unity.textmeshpro\""));
                Assert.That(requestJson, Does.Contain("\"source\": \"com.unity.textmeshpro@3.0.7\""));
                Assert.That(requestJson, Does.Contain("\"packageId\": \"com.unity.ugui\""));

                string[] payloadPaths = ReadUnityPackagePaths(payloadBytes);
                Assert.That(
                    payloadPaths.Any(path =>
                        path.Contains("LocalizationKit/Adapters/TMP", StringComparison.Ordinal)),
                    Is.True);
                Assert.That(
                    payloadPaths.Any(path =>
                        path.StartsWith("Assets/TextMesh Pro", StringComparison.Ordinal)),
                    Is.False);
                Assert.That(
                    payloadPaths.Any(path =>
                        path.Contains("/Tests/", StringComparison.Ordinal)),
                    Is.False);
                Assert.That(
                    payloadPaths.Any(path =>
                        path.Contains("Modules/Packaging", StringComparison.Ordinal)),
                    Is.False);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(outputPath) && File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }

                if (!string.IsNullOrWhiteSpace(guidePath) && File.Exists(guidePath))
                {
                    File.Delete(guidePath);
                }
            }
        }

        [Test]
        public void ToolsHubAvailabilityCheckRejectsMissingAssemblyAtRuntime()
        {
            Type attributeType = Type.GetType(
                "StellarFramework.Editor.StellarToolAttribute, StellarFramework.ToolsHub.Editor");
            Type hubType = Type.GetType(
                "StellarFramework.Editor.StellarFrameworkTools, StellarFramework.ToolsHub.Editor");

            Assert.That(attributeType, Is.Not.Null);
            Assert.That(hubType, Is.Not.Null);

            object attribute = Activator.CreateInstance(attributeType, "test", "test", 0);
            PropertyInfo requiredAssemblies = attributeType.GetProperty("RequiredAssemblyNames");
            MethodInfo availabilityCheck = hubType.GetMethod("IsModuleAvailable",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.That(requiredAssemblies, Is.Not.Null);
            Assert.That(availabilityCheck, Is.Not.Null);

            requiredAssemblies.SetValue(attribute, new[] { "StellarFramework.DefinitelyMissingKit" });
            Assert.That((bool)availabilityCheck.Invoke(null, new[] { attribute }), Is.False);

            requiredAssemblies.SetValue(attribute, Array.Empty<string>());
            Assert.That((bool)availabilityCheck.Invoke(null, new[] { attribute }), Is.True);
        }

        [Test]
        public void ToolsHubKitModulesAreCompiledAsOptionalChildAssemblies()
        {
            string rootAsmdef = ReadAssetText(
                "Assets/StellarFramework/Editor/StellarToolsHub/StellarFramework.Editor.asmdef");

            Assert.That(rootAsmdef, Does.Not.Contain("StellarFramework.EventKit"));
            Assert.That(rootAsmdef, Does.Not.Contain("StellarFramework.ResKit"));
            Assert.That(rootAsmdef, Does.Not.Contain("StellarFramework.UIKit"));
            Assert.That(rootAsmdef, Does.Not.Contain("StellarFramework.SingletonKit"));

            AssertChildToolsHubAssembly("ActionKit", "StellarFramework.ActionKit");
            AssertChildToolsHubAssembly("AudioKit", "StellarFramework.AudioKit");
            AssertChildToolsHubAssembly("ConfigKit", "StellarFramework.ConfigKit.Json");
            AssertChildToolsHubAssembly("EventKit", "StellarFramework.EventKit");
            AssertChildToolsHubAssembly("ResKit", "StellarFramework.ResKit");
            AssertChildToolsHubAssembly("UIKit", "StellarFramework.UIKit");
            AssertChildToolsHubAssembly("SingletonKit", "StellarFramework.Singleton.Editor");
        }

        [Test]
        public void KitExportsIncludeToolsHubCoreAndTheirOwnOptionalToolModules()
        {
            string catalog = ReadAssetText("Assets/StellarFramework/KitCatalog/KitDistributionCatalog.json");
            string publisher = ReadAssetText(
                "Assets/StellarFramework/Editor/StellarToolsHub/Modules/Packaging/StellarFrameworkPackagePublisher.cs");

            Assert.That(catalog, Does.Contain("\"id\": \"toolshub.core\""));
            Assert.That(catalog, Does.Contain("StellarFramework-ToolsHub-Core.unitypackage"));
            Assert.That(catalog, Does.Contain("Modules/EventKit"));
            Assert.That(catalog, Does.Contain("Modules/ConfigKit"));
            Assert.That(catalog, Does.Contain("Modules/SingletonKit"));
            Assert.That(catalog, Does.Contain("Modules/ResKit"));
            Assert.That(catalog, Does.Contain("Modules/AssetBundle"));
            Assert.That(publisher, Does.Contain("ExportToolsHubCorePackage"));
        }

        [Test]
        public void HybridClrKitIsOneBackendAgnosticCodeUpdateProfile()
        {
            string catalog = ReadAssetText("Assets/StellarFramework/KitCatalog/KitDistributionCatalog.json");
            string publisher = ReadAssetText(
                "Assets/StellarFramework/Editor/StellarToolsHub/Modules/Packaging/StellarFrameworkPackagePublisher.cs");

            Assert.That(catalog, Does.Contain("\"id\": \"hybridclrkit\""));
            Assert.That(catalog, Does.Contain("StellarFramework-HybridCLRKit.unitypackage"));
            Assert.That(catalog, Does.Not.Contain("\"id\": \"hotupdate.core\""));
            Assert.That(catalog, Does.Not.Contain("\"id\": \"hotupdate.addressables\""));
            Assert.That(catalog, Does.Not.Contain("\"id\": \"hotupdate.hybridclr\""));
            Assert.That(catalog, Does.Contain("com.code-philosophy.hybridclr"));
            Assert.That(publisher, Does.Contain("ExportHybridCLRKitPackage"));
        }

        [Test]
        public void HybridClrKitUsesResKitAndDoesNotDependOnAddressables()
        {
            string core = ReadAssetText("Assets/StellarFramework/Runtime/Kits/HybridCLRKit/HotUpdateContracts.cs");
            string adapter = ReadAssetText(
                "Assets/StellarFramework/Runtime/Kits/HybridCLRKit/Runtime/HybridCLRHotUpdateAdapter.cs");
            string asmdef = ReadAssetText(
                "Assets/StellarFramework/Runtime/Kits/HybridCLRKit/StellarFramework.HybridCLRKit.asmdef");
            string catalog = ReadAssetText("Assets/StellarFramework/KitCatalog/KitDistributionCatalog.json");

            Assert.That(core, Does.Contain("public static class HybridCLRKit"));
            Assert.That(adapter, Does.Contain("class HybridCLRHook"));
            Assert.That(adapter, Does.Contain("class HybridCLRRunner"));
            Assert.That(adapter, Does.Contain("ResKit.CreateCustomScope"));
            Assert.That(adapter, Does.Contain("settings.ResourceLoaderKey"));
            Assert.That(adapter, Does.Not.Contain("AddressableHotUpdateManager"));
            Assert.That(adapter, Does.Not.Contain("CheckCatalogUpdates"));
            Assert.That(adapter, Does.Not.Contain("DownloadDependencies"));
            Assert.That(asmdef, Does.Not.Contain("Unity.Addressables"));
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.ResKit.Addressables"));
            Assert.That(catalog, Does.Contain("\"id\": \"hybridclrkit\""));
        }

        private static void AssertChildToolsHubAssembly(string kitFolder, string expectedReference)
        {
            string asmdef = ReadAssetText(
                $"Assets/StellarFramework/Editor/StellarToolsHub/Modules/{kitFolder}/StellarFramework.ToolsHub.{kitFolder}.Editor.asmdef");

            Assert.That(asmdef, Does.Contain("StellarFramework.ToolsHub.Editor"));
            Assert.That(asmdef, Does.Contain(expectedReference));
        }

        private static string ReadAssetText(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return File.ReadAllText(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string ToAbsoluteAssetPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string[] ReadUnityPackagePaths(string path)
        {
            return ReadUnityPackagePaths(File.ReadAllBytes(path));
        }

        private static string[] ReadUnityPackagePaths(byte[] packageBytes)
        {
            Dictionary<string, byte[]> entries = ReadUnityPackageTarEntries(packageBytes);
            return entries
                .Where(pair => pair.Key.EndsWith("/pathname", StringComparison.Ordinal))
                .Select(pair => Encoding.UTF8.GetString(pair.Value).Trim('\0'))
                .ToArray();
        }

        private static byte[] ReadUnityPackageAsset(string packagePath, string assetPath)
        {
            Dictionary<string, byte[]> entries =
                ReadUnityPackageTarEntries(File.ReadAllBytes(packagePath));
            foreach (KeyValuePair<string, byte[]> pair in entries
                         .Where(pair => pair.Key.EndsWith("/pathname", StringComparison.Ordinal)))
            {
                string path = Encoding.UTF8.GetString(pair.Value).Trim('\0');
                if (!string.Equals(path, assetPath, StringComparison.Ordinal))
                {
                    continue;
                }

                string folder = pair.Key.Substring(0, pair.Key.IndexOf('/', StringComparison.Ordinal));
                return entries.TryGetValue(folder + "/asset", out byte[] assetBytes)
                    ? assetBytes
                    : null;
            }

            return null;
        }

        private static Dictionary<string, byte[]> ReadUnityPackageTarEntries(byte[] packageBytes)
        {
            using (var input = new MemoryStream(packageBytes))
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                gzip.CopyTo(output);
                byte[] tarBytes = output.ToArray();
                var entries = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                int offset = 0;
                while (offset + 512 <= tarBytes.Length)
                {
                    string entryName = Encoding.ASCII.GetString(tarBytes, offset, 100).Trim('\0');
                    if (string.IsNullOrWhiteSpace(entryName))
                    {
                        break;
                    }

                    string sizeText = Encoding.ASCII.GetString(tarBytes, offset + 124, 12).Trim('\0', ' ');
                    long size = string.IsNullOrWhiteSpace(sizeText)
                        ? 0L
                        : Convert.ToInt64(sizeText, 8);
                    Assert.That(size, Is.LessThanOrEqualTo(int.MaxValue), entryName);
                    byte[] data = new byte[(int)size];
                    if (size > 0)
                    {
                        Buffer.BlockCopy(tarBytes, offset + 512, data, 0, (int)size);
                    }
                    entries[entryName] = data;
                    offset += 512 + (int)(((size + 511L) / 512L) * 512L);
                }

                return entries;
            }
        }
    }
}
