using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace StellarFramework.Tests.FrameworkValidation
{
    /// <summary>
    /// Locks the independent Foundation boundaries that the World Framework will build on.
    /// These tests intentionally validate assembly dependency direction instead of gameplay behavior.
    /// </summary>
    public sealed class WorldFrameworkFoundationBoundaryTests
    {
        [Test]
        public void WorldFoundationCoresRemainZeroDependencyAndEngineFree()
        {
            AssertZeroDependencyCore(
                "Assets/StellarFramework/Runtime/Kits/GridKit/StellarFramework.GridKit.Core.asmdef");
            AssertZeroDependencyCore(
                "Assets/StellarFramework/Runtime/Kits/SpatialKit/StellarFramework.SpatialKit.Core.asmdef");
            AssertZeroDependencyCore(
                "Assets/StellarFramework/Runtime/Kits/PathKit/StellarFramework.PathKit.Core.asmdef");
            AssertZeroDependencyCore(
                "Assets/StellarFramework/Runtime/Kits/SimulationKit/StellarFramework.SimulationKit.Core.asmdef");
            AssertZeroDependencyCore(
                "Assets/StellarFramework/Runtime/Kits/WorldKit/StellarFramework.WorldKit.Core.asmdef");
            AssertZeroDependencyCore(
                "Assets/StellarFramework/Runtime/Kits/WorldGenKit/StellarFramework.WorldGenKit.Core.asmdef");
        }

        [Test]
        public void PathKitGridKitAdapterReferencesOnlyItsTwoFoundationCores()
        {
            string asmdef = Read(
                "Assets/StellarFramework/Runtime/Kits/PathKit/Adapters/GridKit/StellarFramework.PathKit.GridKitAdapter.asmdef");

            Assert.That(asmdef, Does.Contain("\"StellarFramework.PathKit.Core\""));
            Assert.That(asmdef, Does.Contain("\"StellarFramework.GridKit.Core\""));
            Assert.That(asmdef, Does.Contain("\"noEngineReferences\": true"));

            Assert.That(asmdef, Does.Not.Contain("WorldKit"));
            Assert.That(asmdef, Does.Not.Contain("WorldGenKit"));
            Assert.That(asmdef, Does.Not.Contain("PlacementKit"));
            Assert.That(asmdef, Does.Not.Contain("SaveKit"));
            Assert.That(asmdef, Does.Not.Contain("SimulationKit"));
            Assert.That(asmdef, Does.Not.Contain("UnityEngine"));
            Assert.That(asmdef, Does.Not.Contain("UniTask"));
        }

        [Test]
        public void GridKitUnityProjectionAdapterReferencesOnlyGridKitCoreAndUnity()
        {
            string asmdefPath =
                "Assets/StellarFramework/Runtime/Kits/GridKitUnityProjection/StellarFramework.GridKit.UnityProjectionAdapter.asmdef";
            string asmdef = Read(asmdefPath);

            Assert.That(
                asmdef,
                Does.Contain("\"StellarFramework.GridKit.Core\""),
                asmdefPath);
            Assert.That(
                asmdef,
                Does.Contain("\"noEngineReferences\": false"),
                asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("WorldKit"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("WorldGenKit"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("PlacementKit"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("PathKit"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("SaveKit"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("SpatialKit"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("UniTask"), asmdefPath);

            string sourceRoot =
                Absolute("Assets/StellarFramework/Runtime/Kits/GridKitUnityProjection");
            foreach (string sourcePath in Directory.GetFiles(
                         sourceRoot,
                         "*.cs",
                         SearchOption.AllDirectories))
            {
                string source = File.ReadAllText(sourcePath);
                Assert.That(source, Does.Not.Contain("StellarFramework.WorldKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("StellarFramework.WorldGenKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("StellarFramework.PlacementKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("StellarFramework.PathKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("StellarFramework.SaveKit"), sourcePath);
            }
        }

        [Test]
        public void ExistingFoundationCoresDoNotAcquireFutureWorldStackDependencies()
        {
            string[] sourceRoots =
            {
                "Assets/StellarFramework/Runtime/Kits/GridKit",
                "Assets/StellarFramework/Runtime/Kits/SpatialKit",
                "Assets/StellarFramework/Runtime/Kits/PathKit/Identity",
                "Assets/StellarFramework/Runtime/Kits/PathKit/Graph",
                "Assets/StellarFramework/Runtime/Kits/PathKit/Search",
                "Assets/StellarFramework/Runtime/Kits/PathKit/Internal",
                "Assets/StellarFramework/Runtime/Kits/SimulationKit"
            };

            foreach (string assetRoot in sourceRoots)
            {
                string absoluteRoot = Absolute(assetRoot);
                Assert.That(Directory.Exists(absoluteRoot), Is.True, assetRoot);

                foreach (string sourcePath in Directory.GetFiles(absoluteRoot, "*.cs", SearchOption.AllDirectories))
                {
                    string source = File.ReadAllText(sourcePath);
                    Assert.That(source, Does.Not.Contain("WorldKit"), sourcePath);
                    Assert.That(source, Does.Not.Contain("WorldGenKit"), sourcePath);
                    Assert.That(source, Does.Not.Contain("PlacementKit"), sourcePath);
                }
            }
        }

        [Test]
        public void GridTopologyHotPathSourceStaysEngineAndIteratorFree()
        {
            string topologyRoot = Absolute("Assets/StellarFramework/Runtime/Kits/GridKit/Topology");
            Assert.That(Directory.Exists(topologyRoot), Is.True);

            foreach (string sourcePath in Directory.GetFiles(topologyRoot, "*.cs", SearchOption.AllDirectories))
            {
                string source = File.ReadAllText(sourcePath);
                Assert.That(source, Does.Not.Contain("using UnityEngine"), sourcePath);
                Assert.That(source, Does.Not.Contain("System.Linq"), sourcePath);
                Assert.That(source, Does.Not.Contain("IEnumerable"), sourcePath);
                Assert.That(source, Does.Not.Contain("yield return"), sourcePath);
                Assert.That(source, Does.Not.Contain("WorldKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("WorldGenKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("PlacementKit"), sourcePath);
            }
        }

        [Test]
        public void WorldKitCoreDoesNotAcquireExistingFoundationOrUnityDependencies()
        {
            string worldRoot = Absolute("Assets/StellarFramework/Runtime/Kits/WorldKit");
            Assert.That(Directory.Exists(worldRoot), Is.True);

            foreach (string sourcePath in Directory.GetFiles(worldRoot, "*.cs", SearchOption.AllDirectories))
            {
                string source = File.ReadAllText(sourcePath);
                Assert.That(source, Does.Not.Contain("using UnityEngine"), sourcePath);
                Assert.That(source, Does.Not.Contain("StellarFramework.GridKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("StellarFramework.SpatialKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("StellarFramework.PathKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("StellarFramework.SaveKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("StellarFramework.SimulationKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("StellarFramework.WorldGenKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("StellarFramework.PlacementKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("System.Reflection"), sourcePath);
                Assert.That(source, Does.Not.Contain("Assembly.GetTypes"), sourcePath);
                Assert.That(source, Does.Not.Contain("Activator.CreateInstance"), sourcePath);
                Assert.That(source, Does.Not.Contain("System.Linq"), sourcePath);
                Assert.That(source, Does.Not.Contain("yield return"), sourcePath);
                Assert.That(source, Does.Not.Contain("Dictionary<string, object>"), sourcePath);
            }
        }

        [Test]
        public void WorldGenKitCoreStaysStandaloneDeterministicAndReflectionFree()
        {
            string[] coreRoots =
            {
                "Assets/StellarFramework/Runtime/Kits/WorldGenKit/Identity",
                "Assets/StellarFramework/Runtime/Kits/WorldGenKit/Channels",
                "Assets/StellarFramework/Runtime/Kits/WorldGenKit/Storage",
                "Assets/StellarFramework/Runtime/Kits/WorldGenKit/Pipeline",
                "Assets/StellarFramework/Runtime/Kits/WorldGenKit/Determinism",
                "Assets/StellarFramework/Runtime/Kits/WorldGenKit/Rules",
                "Assets/StellarFramework/Runtime/Kits/WorldGenKit/Diagnostics",
                "Assets/StellarFramework/Runtime/Kits/WorldGenKit/Internal"
            };

            foreach (string root in coreRoots)
            {
                string worldGenRoot = Absolute(root);
                Assert.That(Directory.Exists(worldGenRoot), Is.True, root);
                foreach (string sourcePath in Directory.GetFiles(worldGenRoot, "*.cs", SearchOption.AllDirectories))
                {
                    string source = File.ReadAllText(sourcePath);
                    Assert.That(source, Does.Not.Contain("using UnityEngine"), sourcePath);
                    Assert.That(source, Does.Not.Contain("StellarFramework.WorldKit"), sourcePath);
                    Assert.That(source, Does.Not.Contain("StellarFramework.GridKit"), sourcePath);
                    Assert.That(source, Does.Not.Contain("StellarFramework.SpatialKit"), sourcePath);
                    Assert.That(source, Does.Not.Contain("StellarFramework.PathKit"), sourcePath);
                    Assert.That(source, Does.Not.Contain("StellarFramework.SaveKit"), sourcePath);
                    Assert.That(source, Does.Not.Contain("StellarFramework.SimulationKit"), sourcePath);
                    Assert.That(source, Does.Not.Contain("StellarFramework.PlacementKit"), sourcePath);
                    Assert.That(source, Does.Not.Contain("System.Reflection"), sourcePath);
                    Assert.That(source, Does.Not.Contain("Assembly.GetTypes"), sourcePath);
                    Assert.That(source, Does.Not.Contain("Activator.CreateInstance"), sourcePath);
                    Assert.That(source, Does.Not.Contain("System.Linq"), sourcePath);
                    Assert.That(source, Does.Not.Contain("yield return"), sourcePath);
                    Assert.That(source, Does.Not.Contain("Dictionary<string, object>"), sourcePath);
                    Assert.That(source, Does.Not.Contain("string.GetHashCode"), sourcePath);
                    Assert.That(source, Does.Not.Contain("UnityEngine.Random"), sourcePath);
                }
            }
        }

        [Test]
        public void WorldGenKitBuiltinsRemainEngineFreeAndDependOnlyOnWorldGenCore()
        {
            string asmdefPath = "Assets/StellarFramework/Runtime/Kits/WorldGenKit/Builtins/StellarFramework.WorldGenKit.Builtins.asmdef";
            string asmdef = Read(asmdefPath);
            Assert.That(asmdef, Does.Contain("\"StellarFramework.WorldGenKit.Core\""), asmdefPath);
            Assert.That(asmdef, Does.Contain("\"noEngineReferences\": true"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.WorldKit.Core"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.GridKit.Core"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("UnityEngine"), asmdefPath);

            string builtinsRoot = Absolute("Assets/StellarFramework/Runtime/Kits/WorldGenKit/Builtins");
            Assert.That(Directory.Exists(builtinsRoot), Is.True);
            foreach (string sourcePath in Directory.GetFiles(builtinsRoot, "*.cs", SearchOption.AllDirectories))
            {
                string source = File.ReadAllText(sourcePath);
                Assert.That(source, Does.Not.Contain("using UnityEngine"), sourcePath);
                Assert.That(source, Does.Not.Contain("using UnityEditor"), sourcePath);
                Assert.That(source, Does.Not.Contain("StellarFramework.WorldKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("StellarFramework.GridKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("StellarFramework.SpatialKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("StellarFramework.PathKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("StellarFramework.SaveKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("StellarFramework.SimulationKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("StellarFramework.PlacementKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("System.Reflection"), sourcePath);
                Assert.That(source, Does.Not.Contain("System.Linq"), sourcePath);
                Assert.That(source, Does.Not.Contain("yield return"), sourcePath);
                Assert.That(source, Does.Not.Contain("Dictionary<string, object>"), sourcePath);
                Assert.That(source, Does.Not.Contain("string.GetHashCode"), sourcePath);
                Assert.That(source, Does.Not.Contain("UnityEngine.Random"), sourcePath);
            }
        }

        [Test]
        public void WorldGenKitAuthoringRemainsEngineFreeAndDependsOnlyOnCoreAndBuiltins()
        {
            string asmdefPath = "Assets/StellarFramework/Runtime/Kits/WorldGenKit/Authoring/StellarFramework.WorldGenKit.Authoring.asmdef";
            string asmdef = Read(asmdefPath);
            Assert.That(asmdef, Does.Contain("\"StellarFramework.WorldGenKit.Core\""), asmdefPath);
            Assert.That(asmdef, Does.Contain("\"StellarFramework.WorldGenKit.Builtins\""), asmdefPath);
            Assert.That(asmdef, Does.Contain("\"noEngineReferences\": true"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.WorldKit.Core"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.GridKit.Core"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("UnityEngine"), asmdefPath);

            string authoringRoot = Absolute("Assets/StellarFramework/Runtime/Kits/WorldGenKit/Authoring");
            Assert.That(Directory.Exists(authoringRoot), Is.True);
            foreach (string sourcePath in Directory.GetFiles(authoringRoot, "*.cs", SearchOption.AllDirectories))
            {
                string source = File.ReadAllText(sourcePath);
                Assert.That(source, Does.Not.Contain("using UnityEngine"), sourcePath);
                Assert.That(source, Does.Not.Contain("using UnityEditor"), sourcePath);
                Assert.That(source, Does.Not.Contain("StellarFramework.WorldKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("StellarFramework.GridKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("StellarFramework.SpatialKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("StellarFramework.PathKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("StellarFramework.SaveKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("StellarFramework.SimulationKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("StellarFramework.PlacementKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("System.Reflection"), sourcePath);
                Assert.That(source, Does.Not.Contain("System.Linq"), sourcePath);
                Assert.That(source, Does.Not.Contain("yield return"), sourcePath);
                Assert.That(source, Does.Not.Contain("Dictionary<string, object>"), sourcePath);
                Assert.That(source, Does.Not.Contain("string.GetHashCode"), sourcePath);
                Assert.That(source, Does.Not.Contain("UnityEngine.Random"), sourcePath);
            }
        }

        [Test]
        public void WorldGenKitResourcesRemainEngineFreeAndDependOnlyOnWorldGenCore()
        {
            string asmdefPath = "Assets/StellarFramework/Runtime/Kits/WorldGenKit/Resources/StellarFramework.WorldGenKit.Resources.asmdef";
            string asmdef = Read(asmdefPath);
            Assert.That(asmdef, Does.Contain("\"StellarFramework.WorldGenKit.Core\""), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.WorldGenKit.Builtins"), asmdefPath);
            Assert.That(asmdef, Does.Contain("\"noEngineReferences\": true"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.WorldGenKit.Authoring"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.WorldKit.Core"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("UnityEngine"), asmdefPath);

            string resourcesRoot = Absolute("Assets/StellarFramework/Runtime/Kits/WorldGenKit/Resources");
            Assert.That(Directory.Exists(resourcesRoot), Is.True);
            foreach (string sourcePath in Directory.GetFiles(resourcesRoot, "*.cs", SearchOption.AllDirectories))
            {
                string source = File.ReadAllText(sourcePath);
                Assert.That(source, Does.Not.Contain("using UnityEngine"), sourcePath);
                Assert.That(source, Does.Not.Contain("using UnityEditor"), sourcePath);
                Assert.That(source, Does.Not.Contain("StellarFramework.WorldKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("StellarFramework.GridKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("StellarFramework.SpatialKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("StellarFramework.PathKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("StellarFramework.SaveKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("StellarFramework.SimulationKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("StellarFramework.PlacementKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("StellarFramework.WorldGenKit.Builtins"), sourcePath);
                Assert.That(source, Does.Not.Contain("StellarFramework.WorldGenKit.Authoring"), sourcePath);
                Assert.That(source, Does.Not.Contain("System.Reflection"), sourcePath);
                Assert.That(source, Does.Not.Contain("Enum.IsDefined"), sourcePath);
                Assert.That(source, Does.Not.Contain("System.Linq"), sourcePath);
                Assert.That(source, Does.Not.Contain("yield return"), sourcePath);
                Assert.That(source, Does.Not.Contain("Dictionary<string, object>"), sourcePath);
                Assert.That(source, Does.Not.Contain("string.GetHashCode"), sourcePath);
                Assert.That(source, Does.Not.Contain("UnityEngine.Random"), sourcePath);
            }
        }

        [Test]
        public void WorldGenKitFeatureCoreRemainsEngineFreeAndDependsOnlyOnWorldGenCore()
        {
            string asmdefPath = "Assets/StellarFramework/Runtime/Kits/WorldGenKit/Feature/StellarFramework.WorldGenKit.Feature.asmdef";
            string asmdef = Read(asmdefPath);
            Assert.That(asmdef, Does.Contain("\"StellarFramework.WorldGenKit.Core\""), asmdefPath);
            Assert.That(asmdef, Does.Contain("\"noEngineReferences\": true"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.WorldGenKit.Resources"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.WorldGenKit.Authoring"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.WorldGenKit.Builtins"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.PlacementKit"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.WorldKit"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("UnityEngine"), asmdefPath);

            string featureRoot = Absolute("Assets/StellarFramework/Runtime/Kits/WorldGenKit/Feature");
            string[] coreDirectories = { "Identity", "Geometry", "Definitions", "Generation", "Terrain", "Compound" };
            for (int directoryIndex = 0; directoryIndex < coreDirectories.Length; directoryIndex++)
            {
                string directory = Path.Combine(featureRoot, coreDirectories[directoryIndex]);
                Assert.That(Directory.Exists(directory), Is.True, directory);
                foreach (string sourcePath in Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories))
                {
                    string source = File.ReadAllText(sourcePath);
                    Assert.That(source, Does.Not.Contain("using UnityEngine"), sourcePath);
                    Assert.That(source, Does.Not.Contain("using UnityEditor"), sourcePath);
                    Assert.That(source, Does.Not.Contain("StellarFramework.WorldGenKit.Resources"), sourcePath);
                    Assert.That(source, Does.Not.Contain("StellarFramework.WorldGenKit.Authoring"), sourcePath);
                    Assert.That(source, Does.Not.Contain("StellarFramework.WorldGenKit.Builtins"), sourcePath);
                    Assert.That(source, Does.Not.Contain("StellarFramework.PlacementKit"), sourcePath);
                    Assert.That(source, Does.Not.Contain("StellarFramework.WorldKit"), sourcePath);
                    Assert.That(source, Does.Not.Contain("System.Reflection"), sourcePath);
                    Assert.That(source, Does.Not.Contain("System.Linq"), sourcePath);
                    Assert.That(source, Does.Not.Contain("yield return"), sourcePath);
                    Assert.That(source, Does.Not.Contain("Dictionary<string, object>"), sourcePath);
                    Assert.That(source, Does.Not.Contain("string.GetHashCode"), sourcePath);
                    Assert.That(source, Does.Not.Contain("UnityEngine.Random"), sourcePath);
                }
            }
        }

        [Test]
        public void PlacementKitCoreRemainsZeroDependencyAndEngineFree()
        {
            string asmdefPath = "Assets/StellarFramework/Runtime/Kits/PlacementKit/Core/StellarFramework.PlacementKit.Core.asmdef";
            AssertZeroDependencyCore(asmdefPath);
            string placementRoot = Absolute("Assets/StellarFramework/Runtime/Kits/PlacementKit/Core");
            Assert.That(Directory.Exists(placementRoot), Is.True);
            foreach (string sourcePath in Directory.GetFiles(placementRoot, "*.cs", SearchOption.AllDirectories))
            {
                string source = File.ReadAllText(sourcePath);
                Assert.That(source, Does.Not.Contain("using UnityEngine"), sourcePath);
                Assert.That(source, Does.Not.Contain("using UnityEditor"), sourcePath);
                Assert.That(source, Does.Not.Contain("StellarFramework.WorldKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("StellarFramework.WorldGenKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("StellarFramework.GridKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("StellarFramework.SpatialKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("StellarFramework.PathKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("StellarFramework.SaveKit"), sourcePath);
                Assert.That(source, Does.Not.Contain("System.Reflection"), sourcePath);
                Assert.That(source, Does.Not.Contain("System.Linq"), sourcePath);
                Assert.That(source, Does.Not.Contain("yield return"), sourcePath);
                Assert.That(source, Does.Not.Contain("Dictionary<string, object>"), sourcePath);
                Assert.That(source, Does.Not.Contain("UnityEngine.Random"), sourcePath);
            }
        }

        [Test]
        public void FeatureResourcesAdapterDependsOnlyOnFeatureAndResources()
        {
            AssertAdapterBoundary(
                "Assets/StellarFramework/Runtime/Kits/WorldGenKit/Feature/Adapters/Resources/StellarFramework.WorldGenKit.Feature.ResourcesAdapter.asmdef",
                "Assets/StellarFramework/Runtime/Kits/WorldGenKit/Feature/Adapters/Resources",
                "StellarFramework.WorldGenKit.Feature",
                "StellarFramework.WorldGenKit.Resources");
        }

        [Test]
        public void FeaturePlacementAdapterDependsOnlyOnFeatureAndPlacementCore()
        {
            AssertAdapterBoundary(
                "Assets/StellarFramework/Runtime/Kits/WorldGenKit/Feature/Adapters/Placement/StellarFramework.WorldGenKit.Feature.PlacementAdapter.asmdef",
                "Assets/StellarFramework/Runtime/Kits/WorldGenKit/Feature/Adapters/Placement",
                "StellarFramework.WorldGenKit.Feature",
                "StellarFramework.PlacementKit.Core");
        }

        [Test]
        public void FeatureAuthoringAdapterUsesOnlyDeclaredWorldGenLayers()
        {
            string asmdefPath = "Assets/StellarFramework/Runtime/Kits/WorldGenKit/Feature/Adapters/Authoring/StellarFramework.WorldGenKit.Feature.AuthoringAdapter.asmdef";
            string asmdef = Read(asmdefPath);
            Assert.That(asmdef, Does.Contain("\"StellarFramework.WorldGenKit.Feature\""), asmdefPath);
            Assert.That(asmdef, Does.Contain("\"StellarFramework.WorldGenKit.Authoring\""), asmdefPath);
            Assert.That(asmdef, Does.Contain("\"StellarFramework.WorldGenKit.Builtins\""), asmdefPath);
            Assert.That(asmdef, Does.Contain("\"StellarFramework.WorldGenKit.Core\""), asmdefPath);
            Assert.That(asmdef, Does.Contain("\"noEngineReferences\": true"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.WorldKit"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.PlacementKit"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.WorldGenKit.Resources"), asmdefPath);
            AssertAdapterSourcesRemainEngineFree("Assets/StellarFramework/Runtime/Kits/WorldGenKit/Feature/Adapters/Authoring");
        }

        [Test]
        public void FeatureWorldKitAdapterDependsOnlyOnFeatureAndWorldKitCore()
        {
            string asmdefPath = "Assets/StellarFramework/Runtime/Kits/WorldGenKit/Feature/Adapters/WorldKit/StellarFramework.WorldGenKit.Feature.WorldKitAdapter.asmdef";
            string asmdef = Read(asmdefPath);
            Assert.That(asmdef, Does.Contain("\"StellarFramework.WorldGenKit.Feature\""), asmdefPath);
            Assert.That(asmdef, Does.Contain("\"StellarFramework.WorldKit.Core\""), asmdefPath);
            Assert.That(asmdef, Does.Contain("\"noEngineReferences\": true"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.SaveKit"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.WorldGenKit.Resources"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.WorldGenKit.Authoring"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.PlacementKit"), asmdefPath);
            AssertAdapterSourcesRemainEngineFree(
                "Assets/StellarFramework/Runtime/Kits/WorldGenKit/Feature/Adapters/WorldKit");
        }

        [Test]
        public void FeatureSaveKitAdapterDependsOnlyOnWorldKitAdapterAndSaveKitCore()
        {
            string asmdefPath = "Assets/StellarFramework/Runtime/Kits/WorldGenKit/Feature/Adapters/SaveKit/StellarFramework.WorldGenKit.Feature.SaveKitAdapter.asmdef";
            string asmdef = Read(asmdefPath);
            Assert.That(asmdef, Does.Contain("\"StellarFramework.WorldGenKit.Feature.WorldKitAdapter\""), asmdefPath);
            Assert.That(asmdef, Does.Contain("\"StellarFramework.SaveKit.Core\""), asmdefPath);
            Assert.That(asmdef, Does.Contain("\"noEngineReferences\": true"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("\"StellarFramework.WorldGenKit.Feature\""), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.WorldGenKit.Resources"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.WorldGenKit.Authoring"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.PlacementKit"), asmdefPath);
            AssertAdapterSourcesRemainEngineFree(
                "Assets/StellarFramework/Runtime/Kits/WorldGenKit/Feature/Adapters/SaveKit");
        }

        [Test]
        public void WorldGenDebugTextureAdapterIsUnityFacingAndDoesNotPullWorldStackDependencies()
        {
            AssertUnityPresentationAdapterBoundary(
                "Assets/StellarFramework/Runtime/Kits/WorldGenKit/Adapters/DebugTexture/StellarFramework.WorldGenKit.DebugTextureAdapter.asmdef",
                "Assets/StellarFramework/Runtime/Kits/WorldGenKit/Adapters/DebugTexture");
        }

        [Test]
        public void WorldGenMeshAdapterIsUnityFacingAndDoesNotPullWorldStackDependencies()
        {
            AssertUnityPresentationAdapterBoundary(
                "Assets/StellarFramework/Runtime/Kits/WorldGenKit/Adapters/Mesh/StellarFramework.WorldGenKit.MeshAdapter.asmdef",
                "Assets/StellarFramework/Runtime/Kits/WorldGenKit/Adapters/Mesh");
        }

        [Test]
        public void WorldGenTilemapAdapterIsUnityFacingAndDoesNotPullWorldStackDependencies()
        {
            AssertUnityPresentationAdapterBoundary(
                "Assets/StellarFramework/Runtime/Kits/WorldGenKit/Adapters/Tilemap/StellarFramework.WorldGenKit.TilemapAdapter.asmdef",
                "Assets/StellarFramework/Runtime/Kits/WorldGenKit/Adapters/Tilemap");
        }

        [Test]
        public void WorldGenUnityTerrainAdapterIsUnityFacingAndDoesNotPullWorldStackDependencies()
        {
            AssertUnityPresentationAdapterBoundary(
                "Assets/StellarFramework/Runtime/Kits/WorldGenKit/Adapters/UnityTerrain/StellarFramework.WorldGenKit.UnityTerrainAdapter.asmdef",
                "Assets/StellarFramework/Runtime/Kits/WorldGenKit/Adapters/UnityTerrain");
        }

        [Test]
        public void WorldKitStreamingDependsOnlyOnFrozenWorldKitCoreAndRemainsEngineFree()
        {
            const string asmdefPath =
                "Assets/StellarFramework/Runtime/Kits/WorldKitStreaming/StellarFramework.WorldKit.Streaming.asmdef";
            string asmdef = Read(asmdefPath);
            Assert.That(asmdef, Does.Contain("\"StellarFramework.WorldKit.Core\""), asmdefPath);
            Assert.That(asmdef, Does.Contain("\"noEngineReferences\": true"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.WorldGenKit"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.SaveKit"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.SimulationKit"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("UnityEngine"), asmdefPath);

            AssertAdapterSourcesRemainEngineFree(
                "Assets/StellarFramework/Runtime/Kits/WorldKitStreaming/Geometry");
            AssertAdapterSourcesRemainEngineFree(
                "Assets/StellarFramework/Runtime/Kits/WorldKitStreaming/Streaming");
        }

        [Test]
        public void WorldGenStreamingAdapterIsEngineFreeAndOnlyBridgesWorldStreamingToWorldGen()
        {
            const string asmdefPath =
                "Assets/StellarFramework/Runtime/Kits/WorldGenKit/Adapters/Streaming/StellarFramework.WorldGenKit.StreamingAdapter.asmdef";
            string asmdef = Read(asmdefPath);
            Assert.That(asmdef, Does.Contain("\"StellarFramework.WorldKit.Core\""), asmdefPath);
            Assert.That(asmdef, Does.Contain("\"StellarFramework.WorldKit.Streaming\""), asmdefPath);
            Assert.That(asmdef, Does.Contain("\"StellarFramework.WorldGenKit.Core\""), asmdefPath);
            Assert.That(asmdef, Does.Contain("\"StellarFramework.WorldGenKit.Builtins\""), asmdefPath);
            Assert.That(asmdef, Does.Contain("\"noEngineReferences\": true"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.SaveKit"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.SimulationKit"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("UnityEngine"), asmdefPath);

            AssertAdapterSourcesRemainEngineFree(
                "Assets/StellarFramework/Runtime/Kits/WorldGenKit/Adapters/Streaming");
        }

        [Test]
        public void WorldStreamingSaveKitAdapterUsesExplicitBoundaryWithoutWorldGenOrUnity()
        {
            const string asmdefPath =
                "Assets/StellarFramework/Runtime/Kits/WorldKitStreaming/Adapters/SaveKit/StellarFramework.WorldKit.Streaming.SaveKitAdapter.asmdef";
            string asmdef = Read(asmdefPath);
            Assert.That(asmdef, Does.Contain("\"StellarFramework.WorldKit.Core\""), asmdefPath);
            Assert.That(asmdef, Does.Contain("\"StellarFramework.WorldKit.Streaming\""), asmdefPath);
            Assert.That(asmdef, Does.Contain("\"StellarFramework.SaveKit.Core\""), asmdefPath);
            Assert.That(asmdef, Does.Contain("\"noEngineReferences\": true"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.WorldGenKit"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.SimulationKit"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("UnityEngine"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("System.Reflection"), asmdefPath);

            AssertAdapterSourcesRemainEngineFree(
                "Assets/StellarFramework/Runtime/Kits/WorldKitStreaming/Adapters/SaveKit");
        }

        [Test]
        public void WorldStreamingUnityAdapterIsTheOnlyFloatingOriginUnityBoundary()
        {
            const string asmdefPath =
                "Assets/StellarFramework/Runtime/Kits/WorldKitStreaming/Adapters/Unity/StellarFramework.WorldKit.Streaming.UnityAdapter.asmdef";
            string asmdef = Read(asmdefPath);
            Assert.That(asmdef, Does.Contain("\"StellarFramework.WorldKit.Core\""), asmdefPath);
            Assert.That(asmdef, Does.Contain("\"StellarFramework.WorldKit.Streaming\""), asmdefPath);
            Assert.That(asmdef, Does.Contain("\"noEngineReferences\": false"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.WorldGenKit"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.SaveKit"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.SimulationKit"), asmdefPath);

            string sourceRoot = Absolute("Assets/StellarFramework/Runtime/Kits/WorldKitStreaming");
            foreach (string sourcePath in Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
            {
                string source = File.ReadAllText(sourcePath);
                bool unityAdapter = sourcePath.Replace('\\', '/').Contains("/Adapters/Unity/");
                if (unityAdapter) continue;
                Assert.That(source, Does.Not.Contain("using UnityEngine"), sourcePath);
                Assert.That(source, Does.Not.Contain("UnityEngine."), sourcePath);
            }
        }

        [Test]
        public void WorldFrameworkToolsHubRemainsEditorOnlyAndRuntimeDoesNotDependOnIt()
        {
            const string asmdefPath =
                "Assets/StellarFramework/Editor/StellarToolsHub/Modules/WorldFramework/StellarFramework.ToolsHub.WorldFramework.Editor.asmdef";
            string asmdef = Read(asmdefPath);
            Assert.That(asmdef, Does.Contain("\"StellarFramework.ToolsHub.Editor\""), asmdefPath);
            Assert.That(asmdef, Does.Contain("\"StellarFramework.WorldKit.Core\""), asmdefPath);
            Assert.That(asmdef, Does.Contain("\"StellarFramework.WorldGenKit.Core\""), asmdefPath);
            Assert.That(asmdef, Does.Contain("\"includePlatforms\": [\"Editor\"]"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.WorldKit.Streaming"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.SaveKit"), asmdefPath);

            string[] runtimeRoots =
            {
                "Assets/StellarFramework/Runtime/Kits/WorldKit",
                "Assets/StellarFramework/Runtime/Kits/WorldKitStreaming",
                "Assets/StellarFramework/Runtime/Kits/WorldGenKit",
                "Assets/StellarFramework/Runtime/Kits/PlacementKit"
            };
            foreach (string runtimeRoot in runtimeRoots)
            {
                string absoluteRoot = Absolute(runtimeRoot);
                foreach (string sourcePath in Directory.GetFiles(absoluteRoot, "*.cs", SearchOption.AllDirectories))
                {
                    string source = File.ReadAllText(sourcePath);
                    Assert.That(source, Does.Not.Contain("using UnityEditor"), sourcePath);
                    Assert.That(source, Does.Not.Contain("StellarFramework.ToolsHub"), sourcePath);
                }
            }
        }

        [Test]
        public void WorldFrameworkRuntimeAvoidsReflectionDiscoveryAndPerCellDynamicBags()
        {
            string[] runtimeRoots =
            {
                "Assets/StellarFramework/Runtime/Kits/WorldKit",
                "Assets/StellarFramework/Runtime/Kits/WorldKitStreaming",
                "Assets/StellarFramework/Runtime/Kits/WorldGenKit",
                "Assets/StellarFramework/Runtime/Kits/PlacementKit",
                "Assets/StellarFramework/Runtime/Kits/GridKitUnityProjection"
            };

            string[] forbiddenPatterns =
            {
                "using System.Reflection",
                "System.Reflection.",
                "Assembly.GetTypes",
                "GetAssemblies()",
                "Activator.CreateInstance",
                "Dictionary<string, object>",
                "Dictionary<string,object>",
                "Dictionary<string, dynamic>",
                "Dictionary<string,dynamic>"
            };

            foreach (string runtimeRoot in runtimeRoots)
            {
                string absoluteRoot = Absolute(runtimeRoot);
                Assert.That(
                    Directory.Exists(absoluteRoot),
                    Is.True,
                    runtimeRoot);

                foreach (string sourcePath in Directory.GetFiles(
                             absoluteRoot,
                             "*.cs",
                             SearchOption.AllDirectories))
                {
                    string source = File.ReadAllText(sourcePath);
                    foreach (string forbiddenPattern in forbiddenPatterns)
                    {
                        Assert.That(
                            source,
                            Does.Not.Contain(forbiddenPattern),
                            sourcePath + " contains forbidden runtime pattern " +
                            forbiddenPattern);
                    }
                }
            }
        }

        [Test]
        public void LocalizationKitCoreRemainsZeroDependencyEngineAndReflectionFree()
        {
            const string asmdefPath =
                "Assets/StellarFramework/Runtime/Kits/LocalizationKit/Core/StellarFramework.LocalizationKit.Core.asmdef";
            AssertZeroDependencyCore(asmdefPath);

            string root = Absolute("Assets/StellarFramework/Runtime/Kits/LocalizationKit/Core");
            Assert.That(Directory.Exists(root), Is.True, root);
            foreach (string sourcePath in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string source = File.ReadAllText(sourcePath);
                Assert.That(source, Does.Not.Contain("using UnityEngine"), sourcePath);
                Assert.That(source, Does.Not.Contain("using UnityEditor"), sourcePath);
                Assert.That(source, Does.Not.Contain("StellarFramework.Settings"), sourcePath);
                Assert.That(source, Does.Not.Contain("StellarFramework.UI"), sourcePath);
                Assert.That(source, Does.Not.Contain("System.Reflection"), sourcePath);
                Assert.That(source, Does.Not.Contain("Assembly.GetTypes"), sourcePath);
                Assert.That(source, Does.Not.Contain("GetAssemblies()"), sourcePath);
                Assert.That(source, Does.Not.Contain("Activator.CreateInstance"), sourcePath);
            }
        }

        [Test]
        public void LocalizationKitAdaptersRemainExplicitAndOneWay()
        {
            const string settingsAsmdef =
                "Assets/StellarFramework/Runtime/Kits/LocalizationKit/Adapters/Settings/StellarFramework.LocalizationKit.SettingsAdapter.asmdef";
            string settings = Read(settingsAsmdef);
            Assert.That(settings, Does.Contain("\"StellarFramework.LocalizationKit.Core\""), settingsAsmdef);
            Assert.That(settings, Does.Contain("\"StellarFramework.SettingsKit\""), settingsAsmdef);
            Assert.That(settings, Does.Not.Contain("StellarFramework.UIKit"), settingsAsmdef);
            Assert.That(settings, Does.Not.Contain("UnityEngine.UI"), settingsAsmdef);

            const string uguiAsmdef =
                "Assets/StellarFramework/Runtime/Kits/LocalizationKit/Adapters/UnityUGUI/StellarFramework.LocalizationKit.UnityUGUIAdapter.asmdef";
            string ugui = Read(uguiAsmdef);
            Assert.That(ugui, Does.Contain("\"StellarFramework.LocalizationKit.Core\""), uguiAsmdef);
            Assert.That(ugui, Does.Contain("\"UnityEngine.UI\""), uguiAsmdef);
            Assert.That(ugui, Does.Not.Contain("StellarFramework.SettingsKit"), uguiAsmdef);
            Assert.That(ugui, Does.Not.Contain("StellarFramework.UIKit"), uguiAsmdef);

            const string editorAsmdef =
                "Assets/StellarFramework/Editor/LocalizationKit/StellarFramework.LocalizationKit.Editor.asmdef";
            string editor = Read(editorAsmdef);
            Assert.That(editor, Does.Contain("\"StellarFramework.LocalizationKit.Core\""), editorAsmdef);
            Assert.That(editor, Does.Contain("\"StellarFramework.LocalizationKit.UnityUGUIAdapter\""), editorAsmdef);
            Assert.That(editor, Does.Contain("\"includePlatforms\": ["), editorAsmdef);
            Assert.That(editor, Does.Contain("\"Editor\""), editorAsmdef);

            string localizationRoot = Absolute("Assets/StellarFramework/Runtime/Kits/LocalizationKit");
            foreach (string sourcePath in Directory.GetFiles(localizationRoot, "*.cs", SearchOption.AllDirectories))
            {
                string source = File.ReadAllText(sourcePath);
                Assert.That(source, Does.Not.Contain("System.Reflection"), sourcePath);
                Assert.That(source, Does.Not.Contain("Assembly.GetTypes"), sourcePath);
                Assert.That(source, Does.Not.Contain("GetAssemblies()"), sourcePath);
                Assert.That(source, Does.Not.Contain("Activator.CreateInstance"), sourcePath);
            }
        }

        private static void AssertAdapterBoundary(string asmdefPath, string sourceRoot, string firstReference, string secondReference)
        {
            string asmdef = Read(asmdefPath);
            Assert.That(asmdef, Does.Contain("\"" + firstReference + "\""), asmdefPath);
            Assert.That(asmdef, Does.Contain("\"" + secondReference + "\""), asmdefPath);
            Assert.That(asmdef, Does.Contain("\"noEngineReferences\": true"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.WorldKit"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.GridKit"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.SpatialKit"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.PathKit"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.SaveKit"), asmdefPath);
            AssertAdapterSourcesRemainEngineFree(sourceRoot);
        }

        private static void AssertAdapterSourcesRemainEngineFree(string sourceRoot)
        {
            string absoluteRoot = Absolute(sourceRoot);
            Assert.That(Directory.Exists(absoluteRoot), Is.True, absoluteRoot);
            foreach (string sourcePath in Directory.GetFiles(absoluteRoot, "*.cs", SearchOption.AllDirectories))
            {
                string source = File.ReadAllText(sourcePath);
                Assert.That(source, Does.Not.Contain("using UnityEngine"), sourcePath);
                Assert.That(source, Does.Not.Contain("using UnityEditor"), sourcePath);
                Assert.That(source, Does.Not.Contain("System.Reflection"), sourcePath);
                Assert.That(source, Does.Not.Contain("UnityEngine.Random"), sourcePath);
            }
        }

        private static void AssertUnityPresentationAdapterBoundary(string asmdefPath, string sourceRoot)
        {
            string asmdef = Read(asmdefPath);
            Assert.That(asmdef, Does.Contain("\"StellarFramework.WorldGenKit.Core\""), asmdefPath);
            Assert.That(asmdef, Does.Contain("\"StellarFramework.WorldGenKit.Builtins\""), asmdefPath);
            Assert.That(asmdef, Does.Contain("\"noEngineReferences\": false"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.WorldKit"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.GridKit"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.SpatialKit"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.PathKit"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.SaveKit"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.PlacementKit"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.WorldGenKit.Resources"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.WorldGenKit.Feature"), asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("StellarFramework.WorldGenKit.Authoring"), asmdefPath);

            string absoluteRoot = Absolute(sourceRoot);
            Assert.That(Directory.Exists(absoluteRoot), Is.True, absoluteRoot);
            foreach (string sourcePath in Directory.GetFiles(absoluteRoot, "*.cs", SearchOption.AllDirectories))
            {
                string source = File.ReadAllText(sourcePath);
                Assert.That(source, Does.Not.Contain("using UnityEditor"), sourcePath);
                Assert.That(source, Does.Not.Contain("System.Reflection"), sourcePath);
                Assert.That(source, Does.Not.Contain("UnityEngine.Random"), sourcePath);
            }
        }

        private static void AssertZeroDependencyCore(string asmdefPath)
        {
            string asmdef = Read(asmdefPath);
            Assert.That(asmdef, Does.Contain("\"references\": []"), asmdefPath);
            Assert.That(asmdef, Does.Contain("\"noEngineReferences\": true"), asmdefPath);
        }

        private static string Read(string assetPath) => File.ReadAllText(Absolute(assetPath));

        private static string Absolute(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
