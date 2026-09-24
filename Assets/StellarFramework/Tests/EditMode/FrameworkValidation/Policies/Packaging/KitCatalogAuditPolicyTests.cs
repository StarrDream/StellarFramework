using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class KitCatalogAuditPolicyTests
    {
        [Test]
        public void CatalogPathsDependenciesAndOutputsAreValid()
        {
            CatalogDocument catalog = ReadCatalog();
            Assert.That(catalog.schemaVersion, Is.EqualTo(4));
            Assert.That(catalog.profiles, Is.Not.Null.And.Not.Empty);

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var outputs = new HashSet<string>(StringComparer.Ordinal);

            foreach (ProfileDocument profile in catalog.profiles)
            {
                Assert.That(ids.Add(profile.id), Is.True, $"Duplicate profile id: {profile.id}");
                Assert.That(outputs.Add(profile.output), Is.True, $"Duplicate profile output: {profile.output}");

                AssertPathsExist(projectRoot, profile.id, "source", profile.sourcePaths);
                AssertPathsExist(projectRoot, profile.id, "documentation", profile.documentationPaths);

                if (IsRuntimeProfile(profile))
                {
                    Assert.That(profile.documentationPaths, Is.Not.Null.And.Not.Empty,
                        $"Runtime profile '{profile.id}' must ship formal documentation.");
                    Assert.That((profile.sourcePaths ?? Array.Empty<string>()).Any(path =>
                            path.StartsWith("Assets/StellarFramework/Editor/StellarToolsHub/", StringComparison.Ordinal)),
                        Is.False,
                        $"Runtime profile '{profile.id}' must not package ToolsHub modules directly.");
                }

                if (profile.kind == "tooling")
                {
                    Assert.That((profile.excludedCapabilities ?? Array.Empty<string>()).Contains("PlayerRuntime"),
                        Is.True,
                        $"Tooling profile '{profile.id}' must explicitly exclude PlayerRuntime.");
                    Assert.That((profile.sourcePaths ?? Array.Empty<string>()).All(path =>
                            IsEditorSourcePath(path)),
                        Is.True,
                        $"Tooling profile '{profile.id}' contains a non-Editor source path.");
                }
            }

            foreach (ProfileDocument profile in catalog.profiles)
            {
                foreach (string dependencyId in profile.requiredProfileIds ?? Array.Empty<string>())
                {
                    Assert.That(ids.Contains(dependencyId), Is.True,
                        $"Profile '{profile.id}' references missing dependency '{dependencyId}'.");
                }
            }
        }

        [Test]
        public void RequiredUpmPackagesHavePublisherInstallSources()
        {
            CatalogDocument catalog = ReadCatalog();
            string[] packageIds = catalog.profiles
                .SelectMany(profile => profile.requiredUpm ?? Array.Empty<string>())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();

            Assert.That(packageIds, Is.Not.Empty);
            foreach (string packageId in packageIds)
            {
                Assert.That(InvokePublisherBool("HasUpmPackageSource", packageId), Is.True,
                    $"No Publisher install source configured for {packageId}.");
            }
        }

        [Test]
        public void ProfileMaturityNeverOutranksDependencyMaturity()
        {
            CatalogDocument catalog = ReadCatalog();
            var byId = catalog.profiles.ToDictionary(profile => profile.id, StringComparer.Ordinal);

            foreach (ProfileDocument profile in catalog.profiles)
            {
                foreach (string dependencyId in profile.requiredProfileIds ?? Array.Empty<string>())
                {
                    ProfileDocument dependency = byId[dependencyId];
                    Assert.That(MaturityRank(profile.maturity), Is.GreaterThanOrEqualTo(MaturityRank(dependency.maturity)),
                        $"Profile '{profile.id}' ({profile.maturity}) outranks dependency '{dependency.id}' ({dependency.maturity}).");
                }
            }
        }

        [Test]
        public void HumanReadableRequiredKitsMatchMachineDependencyIds()
        {
            CatalogDocument catalog = ReadCatalog();
            var byId = catalog.profiles.ToDictionary(profile => profile.id, StringComparer.Ordinal);

            foreach (ProfileDocument profile in catalog.profiles)
            {
                string[] expected = (profile.requiredProfileIds ?? Array.Empty<string>())
                    .Select(id => byId[id].displayName)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                string[] actual = (profile.requiredKits ?? Array.Empty<string>())
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();

                Assert.That(actual, Is.EqualTo(expected),
                    $"Profile '{profile.id}' has requiredKits that drifted from requiredProfileIds.");
            }
        }

        [Test]
        public void RuntimeAndToolingProfilesHaveDocumentationInTheirClosure()
        {
            CatalogDocument catalog = ReadCatalog();
            var byId = catalog.profiles.ToDictionary(profile => profile.id, StringComparer.Ordinal);

            foreach (ProfileDocument profile in catalog.profiles.Where(profile =>
                         IsRuntimeProfile(profile) || profile.kind == "tooling"))
            {
                HashSet<string> closureIds = ResolveProfileClosure(profile.id, byId);
                bool hasDocumentation = closureIds
                    .Select(id => byId[id])
                    .Any(item => item.documentationPaths != null && item.documentationPaths.Length > 0);

                Assert.That(hasDocumentation, Is.True,
                    $"Profile '{profile.id}' has no formal documentation anywhere in its dependency closure.");
            }
        }

        [Test]
        public void LegacyAliasesRemainResolvableButAreHiddenFromNormalPicker()
        {
            CatalogDocument catalog = ReadCatalog();
            string[] legacyIds = catalog.profiles
                .Where(profile => (profile.optionalCapabilities ?? Array.Empty<string>())
                    .Contains("LegacyDistributionAlias"))
                .Select(profile => profile.id)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();

            Assert.That(legacyIds, Is.EqualTo(new[] { "uikit.adaptation", "uikit.adaptation.tools" }));

            string[] visibleIds = InvokePublisherProfileIds("GetSourceProjectExportProfiles");
            foreach (string legacyId in legacyIds)
            {
                Assert.That(visibleIds, Does.Not.Contain(legacyId));
            }
        }

        [Test]
        public void CatalogDependencyClosureCoversInternalAsmdefReferences()
        {
            CatalogDocument catalog = ReadCatalog();
            var profilesById = catalog.profiles.ToDictionary(profile => profile.id, StringComparer.Ordinal);
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;

            string[] asmdefFiles = Directory.GetFiles(
                Path.Combine(projectRoot, "Assets", "StellarFramework"),
                "*.asmdef",
                SearchOption.AllDirectories);
            var asmdefByName = new Dictionary<string, AsmdefEntry>(StringComparer.Ordinal);
            var asmdefNameByGuid = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (string file in asmdefFiles)
            {
                AsmdefDocument document = JsonUtility.FromJson<AsmdefDocument>(File.ReadAllText(file));
                Assert.That(document, Is.Not.Null);
                Assert.That(document.name, Is.Not.Null.And.Not.Empty, file);

                string assetPath = ToAssetPath(projectRoot, file);
                asmdefByName.Add(document.name, new AsmdefEntry(assetPath, document));

                string metaPath = file + ".meta";
                if (!File.Exists(metaPath))
                {
                    continue;
                }

                string guidLine = File.ReadLines(metaPath)
                    .FirstOrDefault(line => line.StartsWith("guid: ", StringComparison.Ordinal));
                if (!string.IsNullOrEmpty(guidLine))
                {
                    asmdefNameByGuid[guidLine.Substring("guid: ".Length).Trim()] = document.name;
                }
            }

            var ownedAssemblies = catalog.profiles.ToDictionary(
                profile => profile.id,
                profile => asmdefByName
                    .Where(pair => IsIncludedInProfile(pair.Value.AssetPath, profile))
                    .Select(pair => pair.Key)
                    .ToHashSet(StringComparer.Ordinal),
                StringComparer.Ordinal);

            foreach (ProfileDocument profile in catalog.profiles)
            {
                HashSet<string> closureIds = ResolveProfileClosure(profile.id, profilesById);
                var availableAssemblies = new HashSet<string>(StringComparer.Ordinal);
                foreach (string closureId in closureIds)
                {
                    availableAssemblies.UnionWith(ownedAssemblies[closureId]);
                }

                foreach (string assemblyName in ownedAssemblies[profile.id])
                {
                    AsmdefEntry entry = asmdefByName[assemblyName];
                    foreach (string reference in entry.Document.references ?? Array.Empty<string>())
                    {
                        string referencedAssembly = ResolveAsmdefReference(reference, asmdefNameByGuid);
                        if (string.IsNullOrEmpty(referencedAssembly) ||
                            !asmdefByName.ContainsKey(referencedAssembly))
                        {
                            continue;
                        }

                        Assert.That(availableAssemblies.Contains(referencedAssembly), Is.True,
                            $"Profile '{profile.id}' packages assembly '{assemblyName}', which references internal " +
                            $"assembly '{referencedAssembly}', but that assembly is missing from its Catalog dependency closure.");
                    }
                }
            }
        }

        private static void AssertPathsExist(string projectRoot, string profileId, string pathKind, string[] paths)
        {
            foreach (string assetPath in paths ?? Array.Empty<string>())
            {
                string absolutePath = Path.Combine(projectRoot,
                    assetPath.Replace('/', Path.DirectorySeparatorChar));
                Assert.That(File.Exists(absolutePath) || Directory.Exists(absolutePath), Is.True,
                    $"Profile '{profileId}' has missing {pathKind} path '{assetPath}'.");
            }
        }

        private static bool IsRuntimeProfile(ProfileDocument profile)
        {
            return profile.kind == "kit" || profile.kind == "kit-with-dependencies";
        }

        private static HashSet<string> ResolveProfileClosure(
            string profileId,
            IReadOnlyDictionary<string, ProfileDocument> profilesById)
        {
            var resolved = new HashSet<string>(StringComparer.Ordinal);
            var visiting = new HashSet<string>(StringComparer.Ordinal);

            void Visit(string currentId)
            {
                Assert.That(visiting.Add(currentId), Is.True,
                    $"Circular Catalog dependency while auditing '{profileId}': {currentId}");
                Assert.That(profilesById.TryGetValue(currentId, out ProfileDocument current), Is.True,
                    $"Unknown Catalog dependency '{currentId}'.");

                foreach (string dependencyId in current.requiredProfileIds ?? Array.Empty<string>())
                {
                    if (!resolved.Contains(dependencyId))
                    {
                        Visit(dependencyId);
                    }
                }

                visiting.Remove(currentId);
                resolved.Add(currentId);
            }

            Visit(profileId);
            return resolved;
        }

        private static bool IsIncludedInProfile(string assetPath, ProfileDocument profile)
        {
            bool insideSource = (profile.sourcePaths ?? Array.Empty<string>())
                .Any(sourcePath => IsPathInside(assetPath, sourcePath));
            bool excluded = (profile.excludedSourcePaths ?? Array.Empty<string>())
                .Any(excludedPath => IsPathInside(assetPath, excludedPath));
            return insideSource && !excluded;
        }

        private static bool IsPathInside(string assetPath, string rootPath)
        {
            string path = assetPath.Replace('\\', '/').TrimEnd('/');
            string root = rootPath.Replace('\\', '/').TrimEnd('/');
            return path.Equals(root, StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsEditorSourcePath(string assetPath)
        {
            string path = assetPath.Replace('\\', '/').TrimEnd('/');
            return path.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.EndsWith("/Editor", StringComparison.OrdinalIgnoreCase);
        }

        private static string ToAssetPath(string projectRoot, string absolutePath)
        {
            string normalizedRoot = projectRoot.Replace('\\', '/').TrimEnd('/');
            string normalizedPath = absolutePath.Replace('\\', '/');
            Assert.That(normalizedPath.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase), Is.True,
                absolutePath);
            return normalizedPath.Substring(normalizedRoot.Length + 1);
        }

        private static string ResolveAsmdefReference(
            string reference,
            IReadOnlyDictionary<string, string> asmdefNameByGuid)
        {
            const string guidPrefix = "GUID:";
            if (reference != null && reference.StartsWith(guidPrefix, StringComparison.Ordinal))
            {
                string guid = reference.Substring(guidPrefix.Length);
                return asmdefNameByGuid.TryGetValue(guid, out string name) ? name : null;
            }

            return reference;
        }

        private static int MaturityRank(string maturity)
        {
            switch (maturity)
            {
                case "stable": return 0;
                case "rc": return 1;
                case "experimental": return 2;
                default: Assert.Fail($"Unknown maturity '{maturity}'."); return int.MaxValue;
            }
        }

        private static CatalogDocument ReadCatalog()
        {
            return JsonUtility.FromJson<CatalogDocument>(ReadAssetText(
                "Assets/StellarFramework/KitCatalog/KitDistributionCatalog.json"));
        }

        private static string ReadAssetText(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return File.ReadAllText(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static Type GetPublisherType()
        {
            Type publisherType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(
                    "StellarFramework.Editor.Modules.StellarFrameworkPackagePublisher", false))
                .FirstOrDefault(type => type != null);
            Assert.That(publisherType, Is.Not.Null);
            return publisherType;
        }

        private static bool InvokePublisherBool(string methodName, string argument)
        {
            MethodInfo method = GetPublisherType().GetMethod(methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            return (bool)method.Invoke(null, new object[] { argument });
        }

        private static string[] InvokePublisherProfileIds(string methodName)
        {
            MethodInfo method = GetPublisherType().GetMethod(methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);

            var result = method.Invoke(null, null) as IEnumerable;
            Assert.That(result, Is.Not.Null);
            var ids = new List<string>();
            foreach (object profile in result)
            {
                FieldInfo field = profile.GetType().GetField("id",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null);
                ids.Add((string)field.GetValue(profile));
            }

            return ids.ToArray();
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
            public string displayName;
            public string kind;
            public string maturity;
            public string output;
            public string[] sourcePaths;
            public string[] documentationPaths;
            public string[] excludedSourcePaths;
            public string[] requiredProfileIds;
            public string[] requiredKits;
            public string[] requiredUpm;
            public string[] optionalCapabilities;
            public string[] excludedCapabilities;
        }

        [Serializable]
        private sealed class AsmdefDocument
        {
            public string name;
            public string[] references;
        }

        private sealed class AsmdefEntry
        {
            public AsmdefEntry(string assetPath, AsmdefDocument document)
            {
                AssetPath = assetPath;
                Document = document;
            }

            public string AssetPath { get; }
            public AsmdefDocument Document { get; }
        }
    }
}
