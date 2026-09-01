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
            AssertProfile(catalog, "audiokit.core", "extension", "presentation");
            AssertProfile(catalog, "uikit.core", "extension", "presentation");
            AssertProfile(catalog, "hotupdate.hybridclr", "adapter", "runtime-delivery");
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
            string guide = ReadAssetText("Assets/StellarFramework/KitCatalog/KitArchitectureGuide.md");
            string matrix = ReadAssetText("Assets/StellarFramework/KitCatalog/KitExportValidationMatrix.md");
            string readme = ReadAssetText("README.md");

            Assert.That(guide, Does.Contain("Foundation 不能依赖 Extension"));
            Assert.That(guide, Does.Contain("所有 Kit 继续按需导出"));
            Assert.That(guide, Does.Contain("TimeKit 是 `foundation / simulation`"));
            Assert.That(guide, Does.Contain("GridKit 是 `foundation / world`"));
            Assert.That(guide, Does.Contain("SpatialKit 是 `foundation / world`"));
            Assert.That(guide, Does.Contain("SimulationKit 是 `foundation / simulation`"));
            Assert.That(matrix, Does.Contain("| TimeKit |"));
            Assert.That(matrix, Does.Contain("| GridKit |"));
            Assert.That(matrix, Does.Contain("| SpatialKit |"));
            Assert.That(readme, Does.Contain("`TimeKit`"));
            Assert.That(readme, Does.Contain("`GridKit`"));
            Assert.That(readme, Does.Contain("`SpatialKit`"));
            Assert.That(readme, Does.Contain("`SimulationKit`"));
            Assert.That(readme, Does.Contain("KitArchitectureGuide.md"));
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
            public string[] requiredProfileIds;
            public string[] requiredKits;
            public string[] requiredUpm;
            public string[] excludedCapabilities;
        }
    }
}
