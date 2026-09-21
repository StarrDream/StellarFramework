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
        public void RuntimeKitProfilesUseSchemaV2ArchitectureMetadata()
        {
            CatalogDocument catalog = ReadCatalog();
            Assert.That(catalog.schemaVersion, Is.EqualTo(2));
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
            AssertProfile(catalog, "hybridclrkit", "extension", "runtime-delivery");
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

            Assert.That(exporter, Does.Contain("Foundation Kits"));
            Assert.That(exporter, Does.Contain("Extension Kits"));
            Assert.That(exporter, Does.Contain("Adapter Profiles"));
            Assert.That(exporter, Does.Contain("GetProfileBadge"));
            Assert.That(exporter, Does.Contain("MatchesSearch"));
            Assert.That(exporter, Does.Contain("EditorStyles.toolbarSearchField"));
            Assert.That(exporter, Does.Contain("ExportKitPackageGroupInternal"));
            Assert.That(publisher, Does.Contain("CurrentDistributionCatalogSchemaVersion = 2"));
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
        }

        [Serializable]
        private sealed class ProfileDocument
        {
            public string id;
            public string kind;
            public string tier;
            public string category;
            public string[] sourcePaths;
            public string[] requiredProfileIds;
            public string[] requiredKits;
            public string[] requiredUpm;
            public string[] excludedCapabilities;
        }
    }
}
