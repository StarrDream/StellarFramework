using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using StellarFramework.Editor.Modules;
using UnityEngine;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class DocumentationHubPolicyTests
    {
        private const string LegacyToolsHubDoc = "Editor/StellarToolsHub/StellarToolsHub-工具中心-Guide.md";

        [Test]
        public void DocumentationHubUsesUiToolkitCreateView()
        {
            string source = ReadAssetText("Assets/StellarFramework/Editor/StellarToolsHub/Modules/DocumentationHubModule.cs");

            Assert.That(source, Does.Contain("public override VisualElement CreateView()"));
            Assert.That(source, Does.Contain("TwoPaneSplitView"));
            Assert.That(source, Does.Contain("ScrollView"));
        }

        [Test]
        public void DocumentationHubSkipsLegacyCompatibilityDocs()
        {
            DocumentationHubModule module = new DocumentationHubModule();
            module.OnEnable();

            List<string> relativePaths = ReadStringFieldValues(module, "RelativePath");

            Assert.That(relativePaths, Does.Not.Contain(LegacyToolsHubDoc));
        }

        [Test]
        public void DocumentationHubEntriesAlwaysPointToExistingFiles()
        {
            DocumentationHubModule module = new DocumentationHubModule();
            module.OnEnable();

            foreach (string path in ReadStringFieldValues(module, "Path"))
            {
                Assert.That(File.Exists(path), Is.True, path);
            }
        }

        private static List<string> ReadStringFieldValues(DocumentationHubModule module, string fieldName)
        {
            List<string> values = new List<string>();
            foreach (object doc in ReadDocs(module))
            {
                FieldInfo field = doc.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null, fieldName);

                string value = field.GetValue(doc) as string;
                if (!string.IsNullOrEmpty(value))
                {
                    values.Add(value);
                }
            }

            return values;
        }

        private static IEnumerable ReadDocs(DocumentationHubModule module)
        {
            FieldInfo docsField = typeof(DocumentationHubModule).GetField("_docs", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(docsField, Is.Not.Null);

            return (IEnumerable)docsField.GetValue(module);
        }

        private static string ReadAssetText(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return File.ReadAllText(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }
    }
}
