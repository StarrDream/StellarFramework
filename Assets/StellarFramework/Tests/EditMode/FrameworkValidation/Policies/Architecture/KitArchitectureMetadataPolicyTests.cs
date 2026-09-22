using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class KitArchitectureMetadataPolicyTests
    {
        private static readonly HashSet<string> ValidTiers = new HashSet<string>(StringComparer.Ordinal)
        {
            "foundation", "extension", "adapter"
        };

        private static readonly HashSet<string> ValidCategories = new HashSet<string>(StringComparer.Ordinal)
        {
            "diagnostics", "infrastructure", "flow", "data", "network", "resource", "simulation",
            "presentation", "world", "gameplay", "runtime-delivery"
        };

        [Test]
        public void RuntimeKitProfilesUseSchemaV3ArchitectureMetadata()
        {
            CatalogDocument catalog = ReadCatalog();
            Assert.That(catalog.schemaVersion, Is.EqualTo(3));
            Assert.That(catalog.profiles, Is.Not.Empty);

            foreach (ProfileDocument profile in catalog.profiles.Where(IsRuntimeKit))
            {
                Assert.That(profile.tier, Is.Not.Null.And.Not.Empty, profile.id);
                Assert.That(profile.category, Is.Not.Null.And.Not.Empty, profile.id);
                Assert.That(ValidTiers.Contains(profile.tier), Is.True, profile.id);
                Assert.That(ValidCategories.Contains(profile.category), Is.True, profile.id);
            }

            AssertProfile(catalog, "timekit", "foundation", "simulation");
            AssertProfile(catalog, "gridkit", "foundation", "world");
            AssertProfile(catalog, "spatialkit", "foundation", "world");
            AssertProfile(catalog, "simulationkit", "foundation", "simulation");
            AssertProfile(catalog, "pathkit", "foundation", "world");
            AssertProfile(catalog, "pathkit.gridkit", "adapter", "world");
            AssertProfile(catalog, "localizationkit.core", "foundation", "data");
            AssertProfile(catalog, "localizationkit.settings", "adapter", "data");
            AssertProfile(catalog, "localizationkit.ugui", "adapter", "presentation");
            AssertProfile(catalog, "localizationkit.tmp", "adapter", "presentation");
            AssertProfile(catalog, "worldgenkit.debugtexture", "adapter", "world");
            AssertProfile(catalog, "worldgenkit.mesh", "adapter", "world");
            AssertProfile(catalog, "worldgenkit.tilemap", "adapter", "world");
            AssertProfile(catalog, "worldgenkit.unityterrain", "adapter", "world");
            AssertProfile(catalog, "worldkit.streaming", "extension", "world");
            AssertProfile(catalog, "worldgenkit.streaming", "adapter", "world");
            AssertProfile(catalog, "worldkit.streaming.savekit", "adapter", "world");
            AssertProfile(catalog, "worldkit.streaming.unity", "adapter", "world");
            AssertProfile(catalog, "audiokit.core", "extension", "presentation");
            AssertProfile(catalog, "uikit.core", "extension", "presentation");
            AssertProfile(catalog, "uikit.adaptation", "adapter", "presentation");
            AssertProfile(catalog, "hybridclrkit", "extension", "runtime-delivery");
        }

        [Test]
        public void UIKitAdaptationRemainsOptionalAndCompleteUIKitComposesIt()
        {
            CatalogDocument catalog = ReadCatalog();
            ProfileDocument uiCore = catalog.profiles.Single(profile => profile.id == "uikit.core");
            ProfileDocument adaptation = catalog.profiles.Single(profile => profile.id == "uikit.adaptation");
            ProfileDocument adaptationTools =
                catalog.profiles.Single(profile => profile.id == "uikit.adaptation.tools");
            RecommendedProfileDocument complete =
                catalog.recommendedProfiles.Single(profile => profile.id == "uikit.complete");

            Assert.That(uiCore.excludedCapabilities, Does.Not.Contain("SafeArea"));
            Assert.That(uiCore.excludedSourcePaths,
                Does.Contain("Assets/StellarFramework/Runtime/Kits/UIKit/Adapters/Adaptation"));
            Assert.That(adaptation.requiredProfileIds, Is.EqualTo(new[] { "uikit.core" }));
            Assert.That(adaptation.requiredUpm, Is.EqualTo(new[] { "com.unity.ugui" }));
            Assert.That(adaptationTools.kind, Is.EqualTo("tooling"));
            Assert.That(adaptationTools.requiredProfileIds,
                Is.EqualTo(new[] { "uikit.adaptation", "toolshub.core" }));
            Assert.That(complete.profileIds, Does.Contain("uikit.adaptation.tools"));

            string panelBase = ReadAssetText(
                "Assets/StellarFramework/Runtime/Kits/UIKit/UIPanelBase.cs");
            string uiKit = ReadAssetText(
                "Assets/StellarFramework/Runtime/Kits/UIKit/UIKit.cs");
            string uiKitEditor = ReadAssetText(
                "Assets/StellarFramework/Runtime/Kits/UIKit/Editor/UIKitEditor.cs");
            string adaptationProfile = ReadAssetText(
                "Assets/StellarFramework/Runtime/Kits/UIKit/Adapters/Adaptation/UIAdaptationProfile.cs");

            Assert.That(panelBase, Does.Contain("PanelLayoutRegion"));
            Assert.That(panelBase, Does.Contain("FullScreen = 0"));
            Assert.That(panelBase, Does.Contain("SafeArea = 1"));
            Assert.That(uiKit, Does.Contain("panel.LayoutRegion"));
            Assert.That(uiKit, Does.Contain("_roleRegionLayers"));
            Assert.That(uiKitEditor, Does.Contain("FullScreenRoot"));
            Assert.That(uiKitEditor, Does.Contain("SafeAreaRoot"));
            Assert.That(adaptationProfile, Does.Contain("CalculateShapeAspect"));
            Assert.That(adaptationProfile, Does.Contain("ResolveOrientation"));
        }

        [Test]
        public void RecommendedProfilesComposeExistingKitProfilesWithoutCreatingRuntimeModules()
        {
            CatalogDocument catalog = ReadCatalog();
            Assert.That(catalog.recommendedProfiles, Is.Not.Null.And.Not.Empty);

            RecommendedProfileDocument localization =
                catalog.recommendedProfiles.Single(profile => profile.id == "localization.complete");
            Assert.That(localization.profileIds, Is.EqualTo(new[]
            {
                "localizationkit.tools",
                "localizationkit.tmp.tools"
            }));
            Assert.That(localization.deliveryGroup, Is.EqualTo("complete"));
            Assert.That(localization.output,
                Is.EqualTo("StellarFramework-Profile-Localization-Complete.unitypackage"));

            RecommendedProfileDocument resKit =
                catalog.recommendedProfiles.Single(profile => profile.id == "reskit.complete");
            Assert.That(resKit.profileIds, Is.EqualTo(new[] { "reskit.tools" }));
            Assert.That(resKit.deliveryGroup, Is.EqualTo("complete"));
            Assert.That(resKit.output, Is.EqualTo("StellarFramework-Profile-ResKit-Complete.unitypackage"));

            RecommendedProfileDocument ui =
                catalog.recommendedProfiles.Single(profile => profile.id == "uikit.complete");
            Assert.That(ui.profileIds, Is.EqualTo(new[]
            {
                "uikit.reskit",
                "uikit.tools",
                "uikit.adaptation.tools",
                "reskit.tools"
            }));
            Assert.That(ui.deliveryGroup, Is.EqualTo("complete"));
            Assert.That(ui.output, Is.EqualTo("StellarFramework-Profile-UIKit-Complete.unitypackage"));

            RecommendedProfileDocument hotUpdate =
                catalog.recommendedProfiles.Single(profile => profile.id == "hotupdate.full");
            Assert.That(hotUpdate.profileIds,
                Is.EqualTo(new[] { "reskit.yooasset", "reskit.tools", "hybridclrkit.tools" }));
            Assert.That(hotUpdate.deliveryGroup, Is.EqualTo("extension"));
            Assert.That(hotUpdate.output, Is.EqualTo("StellarFramework-Profile-HotUpdate-Full.unitypackage"));

            var kitProfileIds = catalog.profiles.Select(profile => profile.id).ToHashSet(StringComparer.Ordinal);
            foreach (RecommendedProfileDocument profile in catalog.recommendedProfiles)
            {
                Assert.That(profile.profileIds, Is.Not.Null.And.Not.Empty, profile.id);
                Assert.That(profile.profileIds.All(kitProfileIds.Contains), Is.True, profile.id);
            }
        }

        [Test]
        public void ResKitAndUIKitKeepRuntimeAndEditorDistributionBoundariesSeparated()
        {
            CatalogDocument catalog = ReadCatalog();
            ProfileDocument resCore = catalog.profiles.Single(profile => profile.id == "reskit.core");
            ProfileDocument resTools = catalog.profiles.Single(profile => profile.id == "reskit.tools");
            ProfileDocument uiCore = catalog.profiles.Single(profile => profile.id == "uikit.core");
            ProfileDocument uiTools = catalog.profiles.Single(profile => profile.id == "uikit.tools");
            ProfileDocument hybridCore = catalog.profiles.Single(profile => profile.id == "hybridclrkit");
            ProfileDocument hybridTools = catalog.profiles.Single(profile => profile.id == "hybridclrkit.tools");

            Assert.That(resCore.sourcePaths, Is.EqualTo(new[] { "Assets/StellarFramework/Runtime/Kits/Reskit" }));
            Assert.That(resCore.requiredProfileIds, Is.EqualTo(new[] { "logkit", "poolkit" }));
            Assert.That(resCore.requiredProfileIds, Does.Not.Contain("singletonkit"));
            Assert.That(resCore.requiredProfileIds, Does.Not.Contain("toolshub.core"));
            Assert.That(resCore.requiredProfileIds, Does.Not.Contain("generated.assetmap"));
            Assert.That(resTools.kind, Is.EqualTo("tooling"));
            Assert.That(resTools.requiredProfileIds,
                Is.EqualTo(new[] { "reskit.core", "generated.assetmap", "toolshub.core" }));

            Assert.That(uiCore.requiredProfileIds, Is.EqualTo(new[] { "runtime.core", "singletonkit" }));
            Assert.That(uiCore.sourcePaths,
                Does.Contain("Assets/StellarFramework/Resources/Managers/UIKit.prefab"));
            Assert.That(uiCore.requiredProfileIds, Does.Not.Contain("poolkit"));
            Assert.That(uiCore.requiredProfileIds, Does.Not.Contain("toolshub.core"));
            Assert.That(uiCore.requiredUpm, Does.Not.Contain("com.unity.nuget.newtonsoft-json"));
            Assert.That(uiTools.kind, Is.EqualTo("tooling"));
            Assert.That(uiTools.requiredProfileIds, Is.EqualTo(new[] { "uikit.core", "toolshub.core" }));

            Assert.That(hybridCore.sourcePaths,
                Is.EqualTo(new[] { "Assets/StellarFramework/Runtime/Kits/HybridCLRKit" }));
            Assert.That(hybridTools.kind, Is.EqualTo("tooling"));
            Assert.That(hybridTools.requiredProfileIds, Is.EqualTo(new[] { "hybridclrkit", "toolshub.core" }));
        }

        [Test]
        public void SingletonKitDistributionOwnsBuildEssentialRegistryBootstrap()
        {
            CatalogDocument catalog = ReadCatalog();
            ProfileDocument singleton = catalog.profiles.Single(profile => profile.id == "singletonkit");
            Assert.That(singleton.sourcePaths, Does.Contain("Assets/StellarFramework/Runtime/Kits/SingletonKit"));

            string generator = ReadAssetText(
                "Assets/StellarFramework/Runtime/Kits/SingletonKit/Editor/SingletonGenerator.cs");
            string kitInstaller = ReadAssetText(
                "Assets/Editor/StellarFramework/KitPackageBootstrap/StellarFrameworkKitPackageBootstrapInstaller.cs");

            Assert.That(generator,
                Does.Contain("Assets/Generated/StellarFramework/SingletonRegister"));
            Assert.That(generator,
                Does.Not.Contain("Assets/StellarFramework/Generated/SingletonRegister"));
            Assert.That(generator, Does.Contain("AppDomain.CurrentDomain.GetAssemblies()"));
            Assert.That(generator, Does.Contain("OnPreprocessBuild"));
            Assert.That(kitInstaller, Does.Contain("[InitializeOnLoad]"));
            Assert.That(kitInstaller, Does.Contain("[DidReloadScripts]"));
            Assert.That(kitInstaller, Does.Contain("TryGenerateSingletonRegistryIfAvailable"));
            Assert.That(kitInstaller,
                Does.Contain("Assembly.Load(\"StellarFramework.Singleton.Editor\")"));
            Assert.That(kitInstaller, Does.Contain("SingletonGenerator"));
            Assert.That(
                File.Exists(
                    "Assets/StellarFramework/Generated/SingletonRegister/" +
                    "StellarFramework.Generated.SingletonRegister.asmdef"),
                Is.False,
                "Standalone SingletonKit must not rely on a fixed generated asmdef with hard-coded Kit references.");
        }

        [Test]
        public void RuntimeProfilesDoNotDependOnToolsHubOrSourceToolsHubModules()
        {
            CatalogDocument catalog = ReadCatalog();
            foreach (ProfileDocument profile in catalog.profiles.Where(IsRuntimeKit))
            {
                Assert.That(profile.requiredProfileIds ?? Array.Empty<string>(),
                    Does.Not.Contain("toolshub.core"),
                    $"{profile.id} must keep ToolsHub as a tooling dependency, not a runtime dependency.");
                Assert.That((profile.sourcePaths ?? Array.Empty<string>()).Any(path =>
                        path.StartsWith("Assets/StellarFramework/Editor/StellarToolsHub/", StringComparison.Ordinal)),
                    Is.False,
                    $"{profile.id} must not source ToolsHub editor modules directly.");
            }
        }

        [Test]
        public void ResourceAndUiAsmdefsKeepDependenciesAtTheirOwningLayer()
        {
            string resCore = ReadAssetText(
                "Assets/StellarFramework/Runtime/Kits/Reskit/StellarFramework.ResKit.asmdef");
            string resAssetBundle = ReadAssetText(
                "Assets/StellarFramework/Runtime/Kits/Reskit/Loaders/AssetBundleLoader/StellarFramework.ResKit.AssetBundle.asmdef");
            string uiCore = ReadAssetText(
                "Assets/StellarFramework/Runtime/Kits/UIKit/StellarFramework.UIKit.asmdef");
            string uiResKit = ReadAssetText(
                "Assets/StellarFramework/Runtime/Kits/UIKit/Adapters/ResKit/StellarFramework.UIKit.ResKit.asmdef");
            string hybridClr = ReadAssetText(
                "Assets/StellarFramework/Runtime/Kits/HybridCLRKit/StellarFramework.HybridCLRKit.asmdef");

            Assert.That(resCore, Does.Contain("StellarFramework.LogKit"));
            Assert.That(resCore, Does.Contain("StellarFramework.PoolKit"));
            Assert.That(resCore, Does.Not.Contain("StellarFramework.SingletonKit"));
            Assert.That(resCore, Does.Not.Contain("StellarFramework.Generated.AssetMap"));
            Assert.That(resAssetBundle, Does.Contain("StellarFramework.SingletonKit"));
            Assert.That(resAssetBundle, Does.Contain("StellarFramework.Generated.AssetMap"));

            Assert.That(uiCore, Does.Contain("StellarFramework.SingletonKit"));
            Assert.That(uiCore, Does.Not.Contain("StellarFramework.PoolKit"));
            Assert.That(uiResKit, Does.Contain("StellarFramework.SingletonKit"));
            Assert.That(uiResKit, Does.Not.Contain("StellarFramework.PoolKit"));

            Assert.That(hybridClr, Does.Contain("StellarFramework.ResKit"));
            Assert.That(hybridClr, Does.Not.Contain("StellarFramework.PoolKit"));
            Assert.That(hybridClr, Does.Not.Contain("StellarFramework.SingletonKit"));
        }

        [Test]
        public void TimeKitProfileKeepsItsMinimalFoundationDependencyClosure()
        {
            CatalogDocument catalog = ReadCatalog();
            ProfileDocument timeKit = catalog.profiles.Single(profile => profile.id == "timekit");

            Assert.That(timeKit.requiredProfileIds, Is.EqualTo(new[] { "logkit" }));
            Assert.That(timeKit.requiredKits, Is.EqualTo(new[] { "LogKit" }));
            Assert.That(timeKit.requiredUpm, Is.Empty);
            Assert.That(timeKit.excludedCapabilities,
                Is.EquivalentTo(new[] { "Addressables", "HybridCLR", "CodeHotUpdate" }));
        }

        [Test]
        public void WorldFrameworkToolsProfileKeepsEditorOnlyDependencyClosure()
        {
            CatalogDocument catalog = ReadCatalog();
            ProfileDocument tools = catalog.profiles.Single(profile => profile.id == "worldframework.tools");

            Assert.That(tools.kind, Is.EqualTo("tooling"));
            Assert.That(tools.tier, Is.Null.Or.Empty);
            Assert.That(tools.category, Is.Null.Or.Empty);
            Assert.That(tools.requiredProfileIds, Is.EqualTo(new[]
            {
                "toolshub.core",
                "worldkit.core",
                "worldgenkit.core",
                "worldgenkit.builtins",
                "worldgenkit.resources",
                "worldgenkit.feature",
                "placementkit.core"
            }));
            Assert.That(tools.requiredKits, Is.EqualTo(new[]
            {
                "ToolsHub.Core",
                "WorldKit.Core",
                "WorldGenKit.Core",
                "WorldGenKit.Builtins",
                "WorldGenKit.Resources",
                "WorldGenKit.Feature",
                "PlacementKit.Core"
            }));
            Assert.That(tools.requiredUpm, Is.Empty);
            Assert.That(tools.excludedCapabilities,
                Does.Contain("PlayerRuntime"));
        }

        [Test]
        public void GridKitUnityProjectionProfileStaysAnIndependentAdapter()
        {
            CatalogDocument catalog = ReadCatalog();
            ProfileDocument grid =
                catalog.profiles.Single(profile => profile.id == "gridkit");
            ProfileDocument projection =
                catalog.profiles.Single(
                    profile => profile.id == "gridkit.unityprojection");

            Assert.That(projection.kind, Is.EqualTo("kit-with-dependencies"));
            Assert.That(projection.tier, Is.EqualTo("adapter"));
            Assert.That(projection.category, Is.EqualTo("world"));
            Assert.That(
                projection.requiredProfileIds,
                Is.EqualTo(new[] { "gridkit" }));
            Assert.That(
                projection.requiredKits,
                Is.EqualTo(new[] { "GridKit" }));
            Assert.That(projection.requiredUpm, Is.Empty);
            Assert.That(
                projection.sourcePaths,
                Is.EqualTo(new[]
                {
                    "Assets/StellarFramework/Runtime/Kits/GridKitUnityProjection"
                }));
            Assert.That(
                grid.sourcePaths.Any(
                    path => path.Contains(
                        "GridKitUnityProjection",
                        StringComparison.Ordinal)),
                Is.False);
            Assert.That(
                grid.sourcePaths.Any(
                    path => path.Contains(
                        "/Adapters/",
                        StringComparison.Ordinal)),
                Is.False);
        }

        [Test]
        public void LocalizationKitCoreProfileStaysStandaloneAndEngineFree()
        {
            CatalogDocument catalog = ReadCatalog();
            ProfileDocument localization = catalog.profiles.Single(
                profile => profile.id == "localizationkit.core");

            Assert.That(localization.kind, Is.EqualTo("kit"));
            Assert.That(localization.tier, Is.EqualTo("foundation"));
            Assert.That(localization.category, Is.EqualTo("data"));
            Assert.That(localization.requiredProfileIds, Is.Empty);
            Assert.That(localization.requiredKits, Is.Empty);
            Assert.That(localization.requiredUpm, Is.Empty);
            Assert.That(localization.sourcePaths, Is.EqualTo(new[]
            {
                "Assets/StellarFramework/Runtime/Kits/LocalizationKit/Core",
                "Assets/StellarFramework/FrameworkDoc/02-Kits/LocalizationKit/LocalizationKit-Guide.md"
            }));
            Assert.That(localization.excludedCapabilities, Does.Contain("UnityEngine"));
            Assert.That(localization.excludedCapabilities, Does.Contain("SettingsKit"));
            Assert.That(localization.excludedCapabilities, Does.Contain("UIKit"));
        }

        [Test]
        public void LocalizationKitAdaptersKeepOneWayDependencyBoundaries()
        {
            CatalogDocument catalog = ReadCatalog();
            ProfileDocument settings = catalog.profiles.Single(
                profile => profile.id == "localizationkit.settings");
            ProfileDocument ugui = catalog.profiles.Single(
                profile => profile.id == "localizationkit.ugui");
            ProfileDocument editor = catalog.profiles.Single(
                profile => profile.id == "localizationkit.editor");
            ProfileDocument tools = catalog.profiles.Single(
                profile => profile.id == "localizationkit.tools");
            ProfileDocument tmp = catalog.profiles.Single(
                profile => profile.id == "localizationkit.tmp");
            ProfileDocument tmpEditor = catalog.profiles.Single(
                profile => profile.id == "localizationkit.tmp.editor");
            ProfileDocument tmpTools = catalog.profiles.Single(
                profile => profile.id == "localizationkit.tmp.tools");

            Assert.That(settings.requiredProfileIds, Is.EqualTo(new[]
            {
                "localizationkit.core",
                "settingskit.core"
            }));
            Assert.That(settings.requiredUpm, Is.Empty);
            Assert.That(ugui.requiredProfileIds, Is.EqualTo(new[]
            {
                "localizationkit.core"
            }));
            Assert.That(ugui.requiredUpm, Is.EqualTo(new[] { "com.unity.ugui" }));
            Assert.That(editor.kind, Is.EqualTo("tooling"));
            Assert.That(editor.tier, Is.Null.Or.Empty);
            Assert.That(editor.category, Is.Null.Or.Empty);
            Assert.That(editor.requiredProfileIds, Is.EqualTo(new[]
            {
                "localizationkit.core",
                "localizationkit.ugui"
            }));
            Assert.That(settings.sourcePaths, Is.EqualTo(new[]
            {
                "Assets/StellarFramework/Runtime/Kits/LocalizationKit/Adapters/Settings"
            }));
            Assert.That(ugui.sourcePaths, Is.EqualTo(new[]
            {
                "Assets/StellarFramework/Runtime/Kits/LocalizationKit/Adapters/UnityUGUI"
            }));
            Assert.That(editor.sourcePaths, Is.EqualTo(new[]
            {
                "Assets/StellarFramework/Editor/LocalizationKit"
            }));
            Assert.That(tools.kind, Is.EqualTo("tooling"));
            Assert.That(tools.requiredProfileIds, Is.EqualTo(new[]
            {
                "localizationkit.editor",
                "toolshub.core"
            }));
            Assert.That(tools.sourcePaths, Is.EqualTo(new[]
            {
                "Assets/StellarFramework/Editor/StellarToolsHub/Modules/LocalizationKit"
            }));
            Assert.That(tmp.requiredProfileIds, Is.EqualTo(new[] { "localizationkit.core" }));
            Assert.That(tmp.requiredUpm, Is.EqualTo(new[] { "com.unity.textmeshpro" }));
            Assert.That(tmpEditor.requiredProfileIds,
                Is.EqualTo(new[] { "localizationkit.tmp", "localizationkit.editor" }));
            Assert.That(tmpTools.requiredProfileIds,
                Is.EqualTo(new[] { "localizationkit.tmp.editor", "toolshub.core" }));
        }

        [Test]
        public void DistributionCatalogContainsNoSampleProfiles()
        {
            CatalogDocument catalog = ReadCatalog();
            Assert.That(catalog.profiles.Any(profile => profile.kind == "sample"), Is.False);
            Assert.That(catalog.profiles.Any(profile => profile.id != null && profile.id.StartsWith("samples.", StringComparison.Ordinal)), Is.False);
        }

        [Test]
        public void FoundationProfilesDoNotReferenceExtensionProfiles()
        {
            CatalogDocument catalog = ReadCatalog();
            var profilesById = catalog.profiles.ToDictionary(profile => profile.id, StringComparer.Ordinal);
            foreach (ProfileDocument profile in catalog.profiles.Where(profile => profile.tier == "foundation"))
            {
                foreach (string dependencyId in profile.requiredProfileIds ?? Array.Empty<string>())
                {
                    Assert.That(profilesById.ContainsKey(dependencyId), Is.True,
                        $"{profile.id} references {dependencyId}");
                    Assert.That(profilesById[dependencyId].tier, Is.Not.EqualTo("extension"),
                        $"Foundation profile {profile.id} must not depend on Extension profile {dependencyId}.");
                }
            }
        }

        [Test]
        public void ExporterUsesArchitectureTierGroupsWithoutChangingDependencyClosureEntryPoints()
        {
            string exporter = ReadAssetText(
                "Assets/StellarFramework/Editor/StellarToolsHub/Modules/Packaging/StellarFrameworkPackageExportWindow.cs");
            string publisher = ReadAssetText(
                "Assets/StellarFramework/Editor/StellarToolsHub/Modules/Packaging/StellarFrameworkPackagePublisher.cs");

            Assert.That(exporter, Does.Contain("01  基础功能"));
            Assert.That(exporter, Does.Contain("02  完整功能"));
            Assert.That(exporter, Does.Contain("03  扩展功能"));
            Assert.That(exporter, Does.Contain("GetBasicDeliveryProfiles"));
            Assert.That(exporter, Does.Contain("GetAtomicExtensionProfiles"));
            Assert.That(exporter, Does.Contain("deliveryGroup"));
            Assert.That(exporter, Does.Contain("TwoPaneSplitView"));
            Assert.That(exporter, Does.Contain("ToolbarSearchField"));
            Assert.That(exporter, Does.Contain("GetProfileBadge"));
            Assert.That(exporter, Does.Contain("MatchesSearch"));
            Assert.That(exporter, Does.Contain("ExportKitPackageGroupInternal"));
            Assert.That(publisher, Does.Contain("CurrentDistributionCatalogSchemaVersion = 3"));
            Assert.That(exporter, Does.Contain("推荐组合"));
            Assert.That(exporter, Does.Contain("ResolveRecommendedProfileClosureIds"));
            Assert.That(publisher, Does.Contain("ExportRecommendedProfileInternal"));
            Assert.That(publisher, Does.Contain("ValidateDistributionCatalog"));
            Assert.That(publisher, Does.Contain("Foundation Kit profile"));
        }

        [Test]
        public void ArchitectureRulesAndTimeKitDistributionAreDocumented()
        {
            string guide = ReadAssetText("Assets/StellarFramework/FrameworkDoc/01-Architecture/KitArchitectureGuide.md");
            string matrix = ReadAssetText("Assets/StellarFramework/FrameworkDoc/08-Validation/KitExportValidationMatrix.md");
            string readme = ReadAssetText("README.md");

            Assert.That(guide, Does.Contain("Foundation 不能依赖 Extension"));
            Assert.That(guide, Does.Contain("所有 Kit 继续按需导出"));
            Assert.That(guide, Does.Contain("TimeKit 是 `foundation / simulation`"));
            Assert.That(guide, Does.Contain("GridKit 是 `foundation / world`"));
            Assert.That(guide, Does.Contain("SpatialKit 是 `foundation / world`"));
            Assert.That(guide, Does.Contain("SimulationKit 是 `foundation / simulation`"));
            Assert.That(guide, Does.Contain("PathKit 是 `foundation / world`"));
            Assert.That(guide, Does.Contain("PathKit.GridKitAdapter Profile"));
            Assert.That(guide, Does.Contain("PathKit V1 Core Semantics 已冻结"));
            Assert.That(guide, Does.Contain("Tiny Foundation Integration"));
            Assert.That(guide, Does.Contain("WorldFramework.ToolsHub"));
            Assert.That(guide, Does.Contain("GridKit.UnityProjectionAdapter"));
            Assert.That(guide, Does.Contain("LocalizationKit.Core"));
            Assert.That(guide, Does.Contain("LocalizationKit.SettingsAdapter"));
            Assert.That(guide, Does.Contain("LocalizationKit.UnityUGUIAdapter"));
            Assert.That(guide, Does.Contain("LocalizationKit.Tools"));
            Assert.That(guide, Does.Contain("LocalizationKit.TMPAdapter"));
            Assert.That(matrix, Does.Contain("| TimeKit |"));
            Assert.That(matrix, Does.Contain("| GridKit |"));
            Assert.That(matrix, Does.Contain("| SpatialKit |"));
            Assert.That(matrix, Does.Contain("SimulationKit V1 Final Hardening"));
            Assert.That(matrix, Does.Contain("| PathKit |"));
            Assert.That(matrix, Does.Contain("PathKit V1 Final Hardening"));
            Assert.That(matrix, Does.Contain("Core semantic diff：新增 `None=0`"));
            Assert.That(matrix, Does.Contain("Core Semantics Frozen = YES"));
            Assert.That(matrix, Does.Contain("Explicit Backlog Drain Throughput"));
            Assert.That(matrix, Does.Contain("| WorldFramework.ToolsHub |"));
            Assert.That(matrix, Does.Contain("| GridKit.UnityProjectionAdapter |"));
            Assert.That(matrix, Does.Contain("| LocalizationKit.Core |"));
            Assert.That(matrix, Does.Contain("| LocalizationKit.SettingsAdapter |"));
            Assert.That(matrix, Does.Contain("| LocalizationKit.UnityUGUIAdapter |"));
            Assert.That(matrix, Does.Contain("| LocalizationKit.Tools |"));
            Assert.That(matrix, Does.Contain("| LocalizationKit.TMPAdapter |"));
            Assert.That(readme, Does.Contain("`TimeKit`"));
            Assert.That(readme, Does.Contain("`GridKit`"));
            Assert.That(readme, Does.Contain("`SpatialKit`"));
            Assert.That(readme, Does.Contain("`SimulationKit`"));
            Assert.That(readme, Does.Contain("`PathKit`"));
            Assert.That(readme, Does.Contain("KitArchitectureGuide.md"));
            Assert.That(readme, Does.Contain("WorldFramework.ToolsHub"));
            Assert.That(readme, Does.Contain("GridKit.UnityProjectionAdapter"));
            Assert.That(readme, Does.Contain("LocalizationKit"));
        }

        private static bool IsRuntimeKit(ProfileDocument profile)
        {
            return profile.kind == "kit" || profile.kind == "kit-with-dependencies";
        }

        private static CatalogDocument ReadCatalog()
        {
            return JsonUtility.FromJson<CatalogDocument>(
                ReadAssetText("Assets/StellarFramework/KitCatalog/KitDistributionCatalog.json"));
        }

        private static void AssertProfile(CatalogDocument catalog, string id, string tier, string category)
        {
            ProfileDocument profile = catalog.profiles.FirstOrDefault(candidate => candidate.id == id);
            Assert.That(profile, Is.Not.Null, id);
            Assert.That(profile.tier, Is.EqualTo(tier), id);
            Assert.That(profile.category, Is.EqualTo(category), id);
        }

        private static string ReadAssetText(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return File.ReadAllText(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        [Serializable]
        private sealed class CatalogDocument
        {
            public int schemaVersion;
            public ProfileDocument[] profiles;
            public RecommendedProfileDocument[] recommendedProfiles;
        }

        [Serializable]
        private sealed class ProfileDocument
        {
            public string id;
            public string kind;
            public string tier;
            public string category;
            public string[] sourcePaths;
            public string[] excludedSourcePaths;
            public string[] requiredProfileIds;
            public string[] requiredKits;
            public string[] requiredUpm;
            public string[] excludedCapabilities;
        }

        [Serializable]
        private sealed class RecommendedProfileDocument
        {
            public string id;
            public string output;
            public string deliveryGroup;
            public string[] profileIds;
        }
    }
}
